using System.Security.Cryptography;

namespace AgenticGateway.Host.Security;

public sealed class GatewayAuthenticationMiddleware(
    RequestDelegate next,
    GatewayApiKeys apiKeys)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        if (!IsLoopbackHost(context.Request.Host.Host))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var expectedKey = context.Request.Path.StartsWithSegments("/mcp") ? apiKeys.Memory : apiKeys.Inference;
        var supplied = ReadBearerToken(context.Request.Headers.Authorization);
        if (supplied is null || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(supplied), expectedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        await next(context);
    }

    private static string? ReadBearerToken(string? authorization)
    {
        const string prefix = "Bearer ";
        return authorization is not null && authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.Ordinal)
        || host.Equals("::1", StringComparison.Ordinal)
        || host.Equals("[::1]", StringComparison.Ordinal);
}
