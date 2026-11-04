using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniDpp.Passports;

namespace MiniDpp.Pages;

public sealed class HistoryModel(PassportService service) : PageModel
{
    public PassportData Current { get; private set; } = default!;
    public List<ArchivedPassport> History { get; private set; } = [];
    public string CurrentPath => $"/dpp/{Current.Gtin}" + (Current.CarbonKgCo2e.HasValue ? "?rol=yetkili" : "");

    public async Task<IActionResult> OnGetAsync(string gtin, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var includeCarbon = DemoVisibility.IsAuthorized(Request.Query);
        var current = await service.FindAsync(gtin, includeCarbon, cancellationToken);
        if (current is null) return NotFound();
        Current = current;
        History = await service.HistoryAsync(gtin, includeCarbon, cancellationToken);
        return Page();
    }
}
