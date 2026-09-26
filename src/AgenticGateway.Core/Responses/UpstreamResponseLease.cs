namespace AgenticGateway.Core.Responses;

public sealed class UpstreamResponseLease(HttpClient client, HttpResponseMessage response) : IDisposable
{
    public HttpResponseMessage Response { get; } = response;

    public void Dispose()
    {
        Response.Dispose();
        client.Dispose();
    }
}
