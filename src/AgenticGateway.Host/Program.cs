using AgenticGateway.Host.Mcp;
using AgenticGateway.Host.Security;
using AgenticGateway.Host.Endpoints;
using AgenticGateway.Host.Admin;
using AgenticGateway.Infrastructure.Memory;
using AgenticGateway.Providers.OpenAICompatible;
using AgenticGateway.Providers.CodexAccount;
using AgenticGateway.Core.Responses;
using AgenticGateway.Core.Routing;
using AgenticGateway.Core.Memory;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}
builder.Services.AddSingleton<GatewayApiKeys>();
builder.Services.AddSingleton<AdminCredential>();
builder.Services.AddSqliteMemory(builder.Configuration);
builder.Services.Configure<ResponsesUpstreamOptions>(builder.Configuration.GetSection(ResponsesUpstreamOptions.SectionName));
builder.Services.AddSingleton<LocalUpstreamSettings>();
builder.Services.AddSingleton<IUpstreamSettings>(services => services.GetRequiredService<LocalUpstreamSettings>());
builder.Services.Configure<DreamingProviderOptions>(builder.Configuration.GetSection(DreamingProviderOptions.SectionName));
builder.Services.AddSingleton<IDreamingProvider, OpenAIResponsesDreamingProvider>();
builder.Services.AddSingleton<DreamingService>();
builder.Services.AddSingleton<IModelRouter, ConfigurationModelRouter>();
builder.Services.AddSingleton<IResponsesUpstream, OpenAICompatibleResponsesUpstream>();
builder.Services.AddSingleton<CodexAccountResponsesUpstream>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.Cookie.Name = "agentic_gateway_admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Path = "/admin";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin");
    options.Conventions.AllowAnonymousToPage("/Admin/Login");
});
builder.Services.AddHttpClient("responses-upstream", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    });
builder.Services.AddHttpClient("codex-account-upstream", client => client.Timeout = Timeout.InfiniteTimeSpan)
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
_ = app.Services.GetRequiredService<AdminCredential>();
await app.Services.GetRequiredService<MemoryDatabaseInitializer>().InitializeAsync();
app.UseMiddleware<GatewayAuthenticationMiddleware>();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

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
app.MapRazorPages();
app.Run();

public partial class Program;
