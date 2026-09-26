using AgenticGateway.Host.Mcp;
using AgenticGateway.Host.Security;
using AgenticGateway.Host.Endpoints;
using AgenticGateway.Infrastructure.Memory;
using AgenticGateway.Providers.OpenAICompatible;
using AgenticGateway.Core.Responses;
using AgenticGateway.Core.Routing;
using AgenticGateway.Core.Memory;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<GatewayApiKeys>();
builder.Services.AddSqliteMemory(builder.Configuration);
builder.Services.Configure<ResponsesUpstreamOptions>(builder.Configuration.GetSection(ResponsesUpstreamOptions.SectionName));
builder.Services.Configure<DreamingProviderOptions>(builder.Configuration.GetSection(DreamingProviderOptions.SectionName));
builder.Services.AddSingleton<IDreamingProvider, OpenAIResponsesDreamingProvider>();
builder.Services.AddSingleton<DreamingService>();
builder.Services.PostConfigure<ResponsesUpstreamOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        options.BaseUrl = Environment.GetEnvironmentVariable("AGENTIC_GATEWAY_OPENAI_BASE_URL");
    }

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        options.ApiKey = Environment.GetEnvironmentVariable("AGENTIC_GATEWAY_OPENAI_API_KEY");
    }

    var defaultModel = Environment.GetEnvironmentVariable("AGENTIC_GATEWAY_DEFAULT_UPSTREAM_MODEL");
    if (!string.IsNullOrWhiteSpace(defaultModel) && !options.ModelAliases.ContainsKey("coding-default"))
    {
        options.ModelAliases["coding-default"] = defaultModel;
    }
});
builder.Services.AddSingleton<IModelRouter, ConfigurationModelRouter>();
builder.Services.AddSingleton<IResponsesUpstream, OpenAICompatibleResponsesUpstream>();
builder.Services.AddHttpClient("responses-upstream", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    });
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<MemoryTools>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();
_ = app.Services.GetRequiredService<GatewayApiKeys>();
await app.Services.GetRequiredService<MemoryDatabaseInitializer>().InitializeAsync();
app.UseMiddleware<GatewayAuthenticationMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (IDbContextFactory<MemoryDbContext> dbFactory, CancellationToken cancellationToken) =>
{
    await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
    return await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapMcp("/mcp");
app.MapResponsesApi();
app.Run();

public partial class Program;
