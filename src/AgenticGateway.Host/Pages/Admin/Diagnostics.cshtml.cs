using AgenticGateway.Core.Routing;
using AgenticGateway.Host.Admin;
using AgenticGateway.Infrastructure.Memory;
using AgenticGateway.Providers.OpenAICompatible;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AgenticGateway.Host.Pages.Admin;

public sealed class DiagnosticsModel(
    IUpstreamSettings upstream, IModelRouter router, IDbContextFactory<MemoryDbContext> dbFactory,
    IConfiguration configuration, AdminCredential admin) : PageModel
{
    public bool DatabaseReady { get; private set; }
    public bool HasApiKey { get; private set; }
    public bool HasBaseUrl { get; private set; }
    public bool HasModel { get; private set; }
    public bool EnvironmentManaged => upstream.IsEnvironmentManaged;
    public string MemoryPath => configuration["Memory:DatabasePath"]
        ?? Environment.GetEnvironmentVariable("AGENTIC_GATEWAY_MEMORY_DB")
        ?? Path.Combine(AppContext.BaseDirectory, "data", "memory.db");
    public string AdminKeySource => admin.IsFileBacked ? admin.KeyFilePath : "Environment / User Secrets";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        DatabaseReady = await db.Database.CanConnectAsync(cancellationToken);
        var current = upstream.GetCurrent();
        HasApiKey = !string.IsNullOrWhiteSpace(current.ApiKey);
        HasBaseUrl = !string.IsNullOrWhiteSpace(current.BaseUrl);
        HasModel = router.TryResolve("coding-default", out _);
    }
}
