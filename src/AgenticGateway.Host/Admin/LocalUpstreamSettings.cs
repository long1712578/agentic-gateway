using System.Text.Json;
using AgenticGateway.Providers.OpenAICompatible;
using Microsoft.Extensions.Options;

namespace AgenticGateway.Host.Admin;

public sealed class LocalUpstreamSettings : IUpstreamSettings
{
    private readonly IOptionsMonitor<ResponsesUpstreamOptions> _defaults;
    private readonly string _path;
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly object _sync = new();
    private SavedUpstreamSettings? _saved;

    public LocalUpstreamSettings(IOptionsMonitor<ResponsesUpstreamOptions> defaults, IConfiguration configuration)
    {
        _defaults = defaults;
        _path = Path.GetFullPath(configuration["Admin:UpstreamSettingsPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "upstream-settings.json"));
        if (File.Exists(_path))
        {
            _saved = JsonSerializer.Deserialize<SavedUpstreamSettings>(File.ReadAllText(_path))
                ?? throw new InvalidOperationException("The saved upstream settings file is invalid.");
        }
    }

    public bool IsEnvironmentManaged => new[]
    {
        "AGENTIC_GATEWAY_OPENAI_BASE_URL", "AGENTIC_GATEWAY_OPENAI_API_KEY",
        "AGENTIC_GATEWAY_DEFAULT_UPSTREAM_MODEL", "ResponsesUpstream__BaseUrl",
        "ResponsesUpstream__ApiKey", "ResponsesUpstream__ModelAliases__coding-default"
    }.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));

    public ResponsesUpstreamOptions GetCurrent()
    {
        var baseline = _defaults.CurrentValue;
        SavedUpstreamSettings? saved;
        lock (_sync)
        {
            saved = _saved;
        }

        var result = new ResponsesUpstreamOptions
        {
            BaseUrl = saved?.BaseUrl ?? baseline.BaseUrl,
            ApiKey = saved?.ApiKey ?? baseline.ApiKey,
            ModelAliases = new Dictionary<string, string>(baseline.ModelAliases, StringComparer.Ordinal)
        };
        if (!string.IsNullOrWhiteSpace(saved?.ModelId))
        {
            result.ModelAliases["coding-default"] = saved.ModelId;
        }

        result.BaseUrl = FirstConfiguredEnvironmentValue("AGENTIC_GATEWAY_OPENAI_BASE_URL", "ResponsesUpstream__BaseUrl")
            ?? result.BaseUrl;
        result.ApiKey = FirstConfiguredEnvironmentValue("AGENTIC_GATEWAY_OPENAI_API_KEY", "ResponsesUpstream__ApiKey")
            ?? result.ApiKey;
        var environmentModel = FirstConfiguredEnvironmentValue(
            "AGENTIC_GATEWAY_DEFAULT_UPSTREAM_MODEL", "ResponsesUpstream__ModelAliases__coding-default");
        if (!string.IsNullOrWhiteSpace(environmentModel))
        {
            result.ModelAliases["coding-default"] = environmentModel;
        }

        return result;
    }

    private static string? FirstConfiguredEnvironmentValue(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    public async Task SaveAsync(string baseUrl, string modelId, string? newApiKey, CancellationToken cancellationToken)
    {
        if (IsEnvironmentManaged)
        {
            throw new InvalidOperationException("Upstream settings are managed by environment variables. Edit those variables and restart the gateway.");
        }

        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || (uri.Scheme == "http"
                && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                && !uri.Host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase)
                && (!System.Net.IPAddress.TryParse(uri.Host, out var address)
                    || !System.Net.IPAddress.IsLoopback(address)))
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !uri.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Base URL must end in /v1. Use HTTPS for remote hosts; HTTP is allowed for loopback and host.docker.internal.", nameof(baseUrl));
        }
        if (string.IsNullOrWhiteSpace(modelId) || modelId.Length > 160 || modelId.Any(char.IsControl))
        {
            throw new ArgumentException("Model ID must contain between 1 and 160 characters.", nameof(modelId));
        }

        await _writer.WaitAsync(cancellationToken);
        try
        {
            var current = GetCurrent();
            var replacementKey = string.IsNullOrWhiteSpace(newApiKey) ? null : newApiKey.Trim();
            var effectiveKey = replacementKey ?? current.ApiKey;
            if (string.IsNullOrWhiteSpace(effectiveKey) || effectiveKey.Length > 4_096 || effectiveKey.Any(char.IsControl))
            {
                throw new ArgumentException("Enter an upstream API key before saving.", nameof(newApiKey));
            }

            string? previouslySavedKey;
            lock (_sync)
            {
                previouslySavedKey = _saved?.ApiKey;
            }

            var next = new SavedUpstreamSettings(uri.ToString().TrimEnd('/'), modelId.Trim(), replacementKey ?? previouslySavedKey);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(next), cancellationToken);
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                File.Move(temporaryPath, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }

            lock (_sync)
            {
                _saved = next;
            }
        }
        finally
        {
            _writer.Release();
        }
    }

    private sealed record SavedUpstreamSettings(string BaseUrl, string ModelId, string? ApiKey);
}
