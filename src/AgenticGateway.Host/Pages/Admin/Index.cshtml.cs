using AgenticGateway.Core.Routing;
using AgenticGateway.Infrastructure.Memory;
using AgenticGateway.Providers.OpenAICompatible;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AgenticGateway.Host.Pages.Admin;

public sealed class IndexModel(IUpstreamSettings upstream, IModelRouter router, IDbContextFactory<MemoryDbContext> dbFactory) : PageModel
{
    public bool DatabaseReady { get; private set; }
    public bool UpstreamConfigured { get; private set; }
    public bool HasModel { get; private set; }
    public string ModelId { get; private set; } = "Chưa chọn";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        DatabaseReady = await db.Database.CanConnectAsync(cancellationToken);
        var current = upstream.GetCurrent();
        UpstreamConfigured = !string.IsNullOrWhiteSpace(current.BaseUrl) && !string.IsNullOrWhiteSpace(current.ApiKey);
        HasModel = router.TryResolve("coding-default", out var route);
        if (HasModel) ModelId = route.UpstreamModel;
    }
}
