using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgenticGateway.Core.Responses;
using AgenticGateway.Core.Routing;
using Microsoft.Extensions.Options;

namespace AgenticGateway.Providers.OpenAICompatible;

public sealed class OpenAICompatibleResponsesUpstream(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ResponsesUpstreamOptions> options) : IResponsesUpstream
{
    public Task<UpstreamResponseLease> ForwardAsync(
        ModelRoute route,
        ReadOnlyMemory<byte> requestBody,
        bool streaming,
        CancellationToken cancellationToken = default)
    {
        var current = options.CurrentValue;
        if (!Uri.TryCreate(current.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException("Configure ResponsesUpstream:BaseUrl with an http(s) URL ending in /v1.");
        }

        if (string.IsNullOrWhiteSpace(current.ApiKey))
        {
            throw new InvalidOperationException("Configure AGENTIC_GATEWAY_OPENAI_API_KEY for the Responses upstream.");
        }

        var endpoint = new Uri($"{baseUri.ToString().TrimEnd('/')}/responses", UriKind.Absolute);
        var payload = ReplaceModel(requestBody, route.UpstreamModel);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(payload)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(streaming ? "text/event-stream" : "application/json"));
        request.Headers.TryAddWithoutValidation("X-Client-Request-Id", Guid.NewGuid().ToString("N"));

        return SendAndReleaseRequestAsync(httpClientFactory.CreateClient("responses-upstream"), request, cancellationToken);
    }

    private static async Task<UpstreamResponseLease> SendAndReleaseRequestAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
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

    private static byte[] ReplaceModel(ReadOnlyMemory<byte> body, string upstreamModel)
    {
        var node = JsonNode.Parse(body.Span) as JsonObject
            ?? throw new JsonException("The Responses request must be a JSON object.");
        node["model"] = upstreamModel;
        return JsonSerializer.SerializeToUtf8Bytes(node);
    }
}
