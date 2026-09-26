using System.Net;
using System.Text;
using System.Text.Json;
using AgenticGateway.Providers.CodexAccount;

var handler = new CaptureHandler();
var provider = new CodexAccountResponsesUpstream(new StubClientFactory(handler));
var payload = JsonSerializer.SerializeToUtf8Bytes(new { model = "gpt-5.5", stream = true, input = "test" });
var claims = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
{
    ["https://api.openai.com/auth"] = new { chatgpt_account_id = "account-test_1" }
});
var token = $"{Base64Url("{}"u8.ToArray())}.{Base64Url(claims)}.signature";

using (var lease = await provider.ForwardAsync(payload, token, CancellationToken.None))
{
    Assert(lease.Response.StatusCode == HttpStatusCode.OK, "response status");
}
Assert(handler.Uri?.ToString() == "https://chatgpt.com/backend-api/codex/responses", "fixed HTTPS destination");
Assert(handler.AccountId == "account-test_1", "account ID claim");
Assert(handler.Authorization == $"Bearer {token}", "session forwarded to fixed destination");
Assert(!handler.GatewayKeyForwarded, "gateway key never sent upstream");
Assert(handler.Body?.SequenceEqual(payload) == true, "request preserved");

using (var lease = await provider.GetModelsAsync(token, "0.156.1", CancellationToken.None))
{
    Assert(lease.Response.StatusCode == HttpStatusCode.OK, "models status");
}
Assert(handler.Uri?.ToString() == "https://chatgpt.com/backend-api/codex/models?client_version=0.156.1", "fixed models destination");
Assert(handler.AccountId == "account-test_1", "models use same account ID");

try
{
    using var _ = await provider.ForwardAsync(payload, "not-a-codex-token", CancellationToken.None);
    throw new Exception("malformed session accepted");
}
catch (UnauthorizedAccessException) { }
Assert(handler.RequestCount == 2, "malformed session never leaves the gateway");
Console.WriteLine("Codex account security checks passed.");

static string Base64Url(byte[] input) => Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception($"Failed: {name}");
}

sealed class StubClientFactory(CaptureHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

sealed class CaptureHandler : HttpMessageHandler
{
    public Uri? Uri { get; private set; }
    public string? AccountId { get; private set; }
    public string? Authorization { get; private set; }
    public byte[]? Body { get; private set; }
    public int RequestCount { get; private set; }
    public bool GatewayKeyForwarded { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        Uri = request.RequestUri;
        AccountId = request.Headers.GetValues("chatgpt-account-id").Single();
        Authorization = request.Headers.Authorization?.ToString();
        GatewayKeyForwarded = request.Headers.Contains("X-Agentic-Gateway-Key");
        Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("data: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
        };
    }
}
