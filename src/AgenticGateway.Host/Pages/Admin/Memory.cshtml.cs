using AgenticGateway.Core.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgenticGateway.Host.Pages.Admin;

public sealed class MemoryModel(MemoryService memory, DreamingService dreaming) : PageModel
{
    [BindProperty] public string ProjectId { get; set; } = "agent-1";
    [BindProperty] public string MemoryContent { get; set; } = "";
    [BindProperty] public string Kind { get; set; } = "Decision";
    [BindProperty] public string? SourceReference { get; set; }
    [BindProperty] public bool Confirmed { get; set; }
    [TempData] public string? Notice { get; set; }
    [TempData] public string? Error { get; set; }
    public string Query { get; private set; } = "";
    public IReadOnlyList<MemorySearchResult> Results { get; private set; } = [];

    public async Task OnGetAsync(string? projectId, string? query, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(projectId)) ProjectId = projectId.Trim();
        Query = query?.Trim() ?? "";
        if (Query.Length == 0) return;
        try
        {
            Results = await memory.RecallAsync(ProjectId, Query, cancellationToken: cancellationToken);
        }
        catch (ArgumentException exception)
        {
            Error = exception.Message;
        }
    }

    public async Task<IActionResult> OnPostRememberAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!Enum.TryParse<MemoryKind>(Kind, ignoreCase: true, out var parsed)
                || parsed == MemoryKind.Summary)
            {
                throw new ArgumentException("Chọn loại memory hợp lệ.");
            }
            await memory.RememberAsync(new RememberMemory(ProjectId, parsed, MemoryContent, "admin-ui",
                SourceReference: SourceReference), cancellationToken);
            Notice = "Đã lưu memory; các agent dùng cùng projectId có thể recall.";
        }
        catch (ArgumentException exception)
        {
            Error = exception.Message;
        }
        return RedirectToPage(new { projectId = ProjectId });
    }

    public async Task<IActionResult> OnPostDreamAsync(CancellationToken cancellationToken)
    {
        if (!Confirmed)
        {
            Error = "Xác nhận trước khi gửi memory tới model để tổng hợp.";
            return RedirectToPage(new { projectId = ProjectId });
        }
        try
        {
            var result = await dreaming.DreamAsync(ProjectId, cancellationToken);
            Notice = result.Status == "summarized"
                ? $"Đã tạo summary từ {result.SourceCount} memory có nguồn."
                : "Cần ít nhất hai memory chưa tổng hợp để chạy dreaming.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            Error = $"Dreaming chưa hoàn tất: {exception.Message}";
        }
        return RedirectToPage(new { projectId = ProjectId });
    }
}
