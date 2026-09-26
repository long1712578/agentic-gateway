using System.Text.Json;
using AgenticGateway.Core.Responses;
using AgenticGateway.Core.Routing;
using AgenticGateway.Host.Admin;
using AgenticGateway.Providers.OpenAICompatible;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgenticGateway.Host.Pages.Admin;

public sealed class UpstreamModel(LocalUpstreamSettings settings, IModelRouter router, IResponsesUpstream upstream) : PageModel
{
    [BindProperty] public string BaseUrl { get; set; } = "";
    [BindProperty] public string ModelId { get; set; } = "";
    [BindProperty] public string? ApiKey { get; set; }
    [TempData] public string? Notice { get; set; }
    [TempData] public string? Error { get; set; }
    public bool HasApiKey { get; private set; }
    public bool EnvironmentManaged => settings.IsEnvironmentManaged;

    public void OnGet() => Load();

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await settings.SaveAsync(BaseUrl, ModelId, ApiKey, cancellationToken);
            Notice = "Đã lưu upstream. Model alias coding-default có hiệu lực cho request mới.";
            return RedirectToPage();
        }
        catch (ArgumentException exception)
        {
            Error = exception.Message;
            HasApiKey = !string.IsNullOrWhiteSpace(settings.GetCurrent().ApiKey);
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            Error = exception.Message;
            Load();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostProbeAsync(CancellationToken cancellationToken)
    {
        if (!router.TryResolve("coding-default", out var route))
        {
            Error = "Hãy cấu hình model alias coding-default trước khi kiểm tra.";
            return RedirectToPage();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var request = JsonSerializer.SerializeToUtf8Bytes(new
            {
                model = "coding-default", input = "Reply with OK.", max_output_tokens = 16, store = false
            });
            using var lease = await upstream.ForwardAsync(route, request, streaming: false, timeout.Token);
            if (lease.Response.IsSuccessStatusCode)
            {
                Notice = "Upstream đã trả lời yêu cầu Responses thử nghiệm.";
            }
            else
            {
                Error = await DescribeFailureAsync(lease.Response, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Error = "Hết thời gian kiểm tra upstream sau 30 giây.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or UriFormatException)
        {
            Error = $"Không kết nối được upstream: {exception.Message}";
        }
        return RedirectToPage();
    }

    private void Load()
    {
        var current = settings.GetCurrent();
        BaseUrl = current.BaseUrl ?? "";
        ModelId = current.ModelAliases.GetValueOrDefault("coding-default") ?? "";
        HasApiKey = !string.IsNullOrWhiteSpace(current.ApiKey);
    }

    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status != 429)
            return $"Upstream trả HTTP {status}. Kiểm tra key, model ID và quyền truy cập model.";

        string? code = null;
        string? type = null;
        try
        {
            // A probe needs only the structured error identifiers, never the full upstream message.
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[8193];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
                if (read == 0) break;
                length += read;
            }
            if (length < buffer.Length)
            {
                using var document = JsonDocument.Parse(buffer.AsMemory(0, length));
                if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    code = SafeIdentifier(error, "code");
                    type = SafeIdentifier(error, "type");
                }
            }
        }
        catch (JsonException) { }
        catch (IOException) { }

        var detail = code is not null ? $" (error.code: {code})" : type is not null ? $" (error.type: {type})" : "";
        var reason = code switch
        {
            "credit_balance_exhausted" or "insufficient_quota" or "organization_spend_limit_exceeded"
                or "project_spend_limit_exceeded" or "organization_usage_limit_exceeded"
                => "Kiểm tra credits, Billing và Limits của project trên OpenAI Platform; thử lại sau sẽ không giải quyết được lỗi hạn mức.",
            "rate_limit_exceeded" or "slow_down" => "Đây là giới hạn tốc độ tạm thời; giảm số request và chờ trước khi thử lại.",
            _ when type == "insufficient_quota" => "Kiểm tra credits, Billing và Limits của project trên OpenAI Platform.",
            _ when type == "rate_limit_error" => "Có thể là giới hạn tốc độ tạm thời; giảm số request và chờ trước khi thử lại.",
            _ => "Kiểm tra mã lỗi trên OpenAI Platform để phân biệt giới hạn tốc độ với hạn mức thanh toán."
        };
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is not null && (code is "rate_limit_exceeded" or "slow_down" || type == "rate_limit_error"))
        {
            var delay = retryAfter.Delta ?? retryAfter.Date - DateTimeOffset.UtcNow;
            if (delay.HasValue && delay.Value > TimeSpan.Zero)
                reason += $" Retry-After: chờ ít nhất {Math.Ceiling(delay.Value.TotalSeconds)} giây.";
        }
        return $"OpenAI trả HTTP 429{detail}. {reason}";
    }

    private static string? SafeIdentifier(JsonElement error, string property)
    {
        if (!error.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var identifier = value.GetString();
        return identifier is { Length: > 0 and <= 80 }
            && identifier.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')
            ? identifier : null;
    }
}
