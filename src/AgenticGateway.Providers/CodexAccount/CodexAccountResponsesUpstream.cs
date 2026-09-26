using System.Net.Http.Headers;
using System.Text.Json;
using AgenticGateway.Core.Responses;

namespace AgenticGateway.Providers.CodexAccount;

/// <summary>
/// Forwards an already authenticated Codex client's Responses stream without storing its ChatGPT session.
/// The destination is fixed so an account token can never be sent to a user-configured upstream URL.
/// </summary>
public sealed class CodexAccountResponsesUpstream(IHttpClientFactory httpClientFactory)
{
    private static readonly Uri ResponsesUri = new("https://chatgpt.com/backend-api/codex/responses");

    public Task<UpstreamResponseLease> GetModelsAsync(
        string accountToken,
        string? clientVersion,
        CancellationToken cancellationToken)
    {
        if (clientVersion is not null && (clientVersion.Length is < 1 or > 40
            || clientVersion.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-'))))
            throw new ArgumentException("Invalid Codex client version.", nameof(clientVersion));

        var accountId = GetAccountId(accountToken)
            ?? throw new UnauthorizedAccessException("A valid Codex ChatGPT session is required.");
        var uri = clientVersion is null
            ? new Uri("https://chatgpt.com/backend-api/codex/models")
            : new Uri($"https://chatgpt.com/backend-api/codex/models?client_version={Uri.EscapeDataString(clientVersion)}");
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        AddAccountHeaders(request, accountToken, accountId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return SendAsync(httpClientFactory.CreateClient("codex-account-upstream"), request, cancellationToken);
    }

    public Task<UpstreamResponseLease> ForwardAsync(
        ReadOnlyMemory<byte> requestBody,
        string accountToken,
        CancellationToken cancellationToken)
    {
        if (requestBody.IsEmpty || requestBody.Length > 16 * 1024 * 1024)
            throw new ArgumentException("The Codex request body is invalid.", nameof(requestBody));

        var accountId = GetAccountId(accountToken)
            ?? throw new UnauthorizedAccessException("A valid Codex ChatGPT session is required.");

        var request = new HttpRequestMessage(HttpMethod.Post, ResponsesUri)
        {
            Content = new ByteArrayContent(requestBody.ToArray())
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        AddAccountHeaders(request, accountToken, accountId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("X-Client-Request-Id", Guid.NewGuid().ToString("N"));
        return SendAsync(httpClientFactory.CreateClient("codex-account-upstream"), request, cancellationToken);
    }

    private static void AddAccountHeaders(HttpRequestMessage request, string token, string accountId)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("chatgpt-account-id", accountId);
        request.Headers.TryAddWithoutValidation("OpenAI-Beta", "responses=experimental");
        request.Headers.TryAddWithoutValidation("originator", "agentic-gateway");
    }

    private static async Task<UpstreamResponseLease> SendAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                return new UpstreamResponseLease(client, response);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
    }

    private static string? GetAccountId(string token)
    {
        if (token.Length is < 100 or > 16_384 || token.Any(char.IsWhiteSpace)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[1].Length > 12_000) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload),
                new JsonDocumentOptions { MaxDepth = 16 });
            if (!document.RootElement.TryGetProperty("https://api.openai.com/auth", out var auth)
                || !auth.TryGetProperty("chatgpt_account_id", out var value)
                || value.ValueKind != JsonValueKind.String)
                return null;
            var accountId = value.GetString();
            return accountId is { Length: > 0 and <= 128 }
                && accountId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                ? accountId : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
