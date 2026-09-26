using System.Text.Json;
using AgenticGateway.Core.Responses;
using AgenticGateway.Core.Routing;

namespace AgenticGateway.Host.Endpoints;

public static class ResponsesEndpoints
{
    private const int MaximumRequestBytes = 16 * 1024 * 1024;
    private const int MaximumNonStreamingResponseBytes = 16 * 1024 * 1024;

    public static IEndpointRouteBuilder MapResponsesApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/models", (IModelRouter router) =>
        {
            var data = router.GetAvailableModels().Values
                .OrderBy(route => route.PublicModel, StringComparer.Ordinal)
                .Select(route => new { id = route.PublicModel, @object = "model", owned_by = route.ConnectionId });
            return Results.Ok(new { @object = "list", data });
        });

        endpoints.MapPost("/v1/responses", ForwardAsync);
        return endpoints;
    }

    private static async Task<IResult> ForwardAsync(
        HttpContext context,
        IModelRouter router,
        IResponsesUpstream upstream,
        ILoggerFactory loggerFactory)
    {
        using var totalTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        totalTimeout.CancelAfter(TimeSpan.FromMinutes(20));
        try
        {
            return await ForwardCoreAsync(context, router, upstream, loggerFactory, totalTimeout.Token);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return Results.Empty;
        }
        catch (OperationCanceledException)
        {
            if (context.Response.HasStarted)
            {
                context.Abort();
                return Results.Empty;
            }

            return Error(StatusCodes.Status504GatewayTimeout, "The upstream request exceeded 20 minutes.", "upstream_timeout");
        }
    }

    private static async Task<IResult> ForwardCoreAsync(
        HttpContext context,
        IModelRouter router,
        IResponsesUpstream upstream,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var body = await ReadRequestBodyAsync(context.Request, cancellationToken);
        if (body is null)
        {
            return Error(StatusCodes.Status413PayloadTooLarge, "The request body exceeds 16 MiB.", "request_too_large");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException)
        {
            return Error(StatusCodes.Status400BadRequest, "The request body must contain valid JSON.", "invalid_json");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("model", out var modelElement)
                || modelElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(modelElement.GetString()))
            {
                return Error(StatusCodes.Status400BadRequest, "The model field is required.", "invalid_model");
            }

            var publicModel = modelElement.GetString()!;
            if (!router.TryResolve(publicModel, out var route))
            {
                return Error(StatusCodes.Status400BadRequest, $"Model '{publicModel}' is not configured.", "model_not_found");
            }

            var streaming = document.RootElement.TryGetProperty("stream", out var streamElement)
                && streamElement.ValueKind == JsonValueKind.True;

            UpstreamResponseLease upstreamResponse;
            try
            {
                upstreamResponse = await upstream.ForwardAsync(route, body, streaming, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                loggerFactory.CreateLogger("ResponsesEndpoint").LogWarning("Responses upstream is not configured: {Message}", exception.Message);
                return Error(StatusCodes.Status503ServiceUnavailable, "The selected upstream is not configured.", "upstream_not_configured");
            }
            catch (HttpRequestException exception)
            {
                loggerFactory.CreateLogger("ResponsesEndpoint").LogWarning(exception, "Responses upstream request failed for {ConnectionId}.", route.ConnectionId);
                return Error(StatusCodes.Status502BadGateway, "The upstream request failed.", "upstream_unavailable");
            }

            using (upstreamResponse)
            {
                var message = upstreamResponse.Response;
                context.Response.StatusCode = (int)message.StatusCode;
                if (message.Content.Headers.ContentType is { } contentType)
                {
                    context.Response.ContentType = contentType.ToString();
                }

                if (message.Headers.TryGetValues("x-request-id", out var requestIds))
                {
                    context.Response.Headers["X-Upstream-Request-Id"] = requestIds.FirstOrDefault();
                }

                if (message.Content.Headers.TryGetValues("cache-control", out var cacheControl))
                {
                    context.Response.Headers.CacheControl = string.Join(",", cacheControl);
                }

                if (!streaming)
                {
                    if (message.Content.Headers.ContentLength > MaximumNonStreamingResponseBytes)
                    {
                        return Error(StatusCodes.Status502BadGateway, "The upstream response exceeded 16 MiB.", "upstream_response_too_large");
                    }

                    var responseBytes = await ReadLimitedAsync(
                        await message.Content.ReadAsStreamAsync(cancellationToken),
                        MaximumNonStreamingResponseBytes,
                        cancellationToken);
                    if (responseBytes is null)
                    {
                        return Error(StatusCodes.Status502BadGateway, "The upstream response exceeded 16 MiB.", "upstream_response_too_large");
                    }

                    context.Response.ContentLength = responseBytes.Length;
                    await context.Response.Body.WriteAsync(responseBytes, cancellationToken);
                    return Results.Empty;
                }

                if (message.Content.Headers.ContentLength is long length)
                {
                    context.Response.ContentLength = length;
                }

                await message.Content.CopyToAsync(context.Response.Body, cancellationToken);
                return Results.Empty;
            }
        }
    }

    private static async Task<byte[]?> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumRequestBytes)
        {
            return null;
        }

        return await ReadLimitedAsync(request.Body, MaximumRequestBytes, cancellationToken);
    }

    private static async Task<byte[]?> ReadLimitedAsync(Stream source, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var output = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return output.ToArray();
            }

            if (output.Length + read > maxBytes)
            {
                return null;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static IResult Error(int statusCode, string message, string code) => Results.Json(
        new { error = new { message, type = "gateway_error", code } },
        statusCode: statusCode);
}
