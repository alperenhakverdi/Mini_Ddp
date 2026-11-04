using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniDpp.Passports;

namespace MiniDpp.Pages;

public sealed class PassportModel(PassportService service) : PageModel
{
    public PassportData Passport { get; private set; } = default!;
    public string JsonLdPath => $"/dpp/{Passport.Gtin}.jsonld" + (Passport.CarbonKgCo2e.HasValue ? "?rol=yetkili" : "");

    public async Task<IActionResult> OnGetAsync(string gtin, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var passport = await service.FindAsync(gtin, DemoVisibility.IsAuthorized(Request.Query), cancellationToken);
        if (passport is null) return NotFound();
        Passport = passport;
        return Page();
    }
}
