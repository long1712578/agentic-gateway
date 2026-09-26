using System.Text.Json;
using AgenticGateway.Core.Responses;
using AgenticGateway.Core.Routing;
using AgenticGateway.Providers.CodexAccount;

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
        endpoints.MapGet("/codex/v1/models", GetCodexModelsAsync);
        endpoints.MapPost("/codex/v1/responses", ForwardAsync);
        return endpoints;
    }

    private static async Task<IResult> GetCodexModelsAsync(
        HttpContext context, CodexAccountResponsesUpstream codexAccount)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var clientVersion = context.Request.Query.TryGetValue("client_version", out var values)
                ? values.ToString() : null;
            using var lease = await codexAccount.GetModelsAsync(
                ReadAccountToken(context.Request), clientVersion, timeout.Token);
            var response = lease.Response;
            const int maxBytes = 2 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > maxBytes)
                return Error(StatusCodes.Status502BadGateway, "The upstream model catalog is too large.", "upstream_response_too_large");
            var bytes = await ReadLimitedAsync(await response.Content.ReadAsStreamAsync(timeout.Token), maxBytes, timeout.Token);
            if (bytes is null)
                return Error(StatusCodes.Status502BadGateway, "The upstream model catalog is too large.", "upstream_response_too_large");
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.Body.WriteAsync(bytes, timeout.Token);
            return Results.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return Error(StatusCodes.Status401Unauthorized, "Sign in to Codex with ChatGPT.", "codex_auth_required");
        }
        catch (ArgumentException)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid Codex model catalog request.", "invalid_codex_request");
        }
        catch (HttpRequestException)
        {
            return Error(StatusCodes.Status502BadGateway, "The Codex model catalog is unavailable.", "upstream_unavailable");
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            return Error(StatusCodes.Status504GatewayTimeout, "The Codex model catalog timed out.", "upstream_timeout");
        }
    }

    private static async Task<IResult> ForwardAsync(
        HttpContext context,
        IModelRouter router,
        IResponsesUpstream upstream,
        CodexAccountResponsesUpstream codexAccount,
        ILoggerFactory loggerFactory)
    {
        using var totalTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        totalTimeout.CancelAfter(TimeSpan.FromMinutes(20));
        try
        {
            return await ForwardCoreAsync(context, router, upstream, codexAccount, loggerFactory, totalTimeout.Token);
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
        CodexAccountResponsesUpstream codexAccount,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var accountMode = context.Request.Path.StartsWithSegments("/codex/v1");
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
            if (accountMode && (publicModel.Length > 160
                || publicModel.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.'))))
            {
                return Error(StatusCodes.Status400BadRequest, "The model field contains invalid characters.", "invalid_model");
            }
            ModelRoute route;
            if (accountMode)
            {
                route = new ModelRoute(publicModel, publicModel, "codex-account");
            }
            else if (!router.TryResolve(publicModel, out route))
            {
                return Error(StatusCodes.Status400BadRequest, $"Model '{publicModel}' is not configured.", "model_not_found");
            }

            var streaming = document.RootElement.TryGetProperty("stream", out var streamElement)
                && streamElement.ValueKind == JsonValueKind.True;
            if (accountMode && !streaming)
            {
                return Error(StatusCodes.Status400BadRequest,
                    "Codex account mode requires stream=true.", "stream_required");
            }

            UpstreamResponseLease upstreamResponse;
            try
            {
                if (accountMode)
                {
                    upstreamResponse = await codexAccount.ForwardAsync(body, ReadAccountToken(context.Request), cancellationToken);
                }
                else
                {
                    upstreamResponse = await upstream.ForwardAsync(route, body, streaming, cancellationToken);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Error(StatusCodes.Status401Unauthorized,
                    "Sign in to Codex with ChatGPT before using account mode.", "codex_auth_required");
            }
            catch (ArgumentException)
            {
                return Error(StatusCodes.Status400BadRequest, "The Codex account request is invalid.", "invalid_codex_request");
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
                if (accountMode) context.Response.Headers.CacheControl = "no-store";
                if (message.Content.Headers.ContentType is { } contentType)
                {
                    context.Response.ContentType = contentType.ToString();
                }

                if (message.Headers.TryGetValues("x-request-id", out var requestIds))
                {
                    context.Response.Headers["X-Upstream-Request-Id"] = requestIds.FirstOrDefault();
                }

                if (message.Headers.RetryAfter is { } retryAfter)
                    context.Response.Headers.RetryAfter = retryAfter.ToString();

                if (!accountMode && message.Content.Headers.TryGetValues("cache-control", out var cacheControl))
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

    private static string ReadAccountToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim() : "";
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
