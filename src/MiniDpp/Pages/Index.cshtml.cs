using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Data;

namespace MiniDpp.Pages;

public sealed record ProductSummary(string Gtin, string ModelName, string BatchNumber);

public sealed class IndexModel(DppDbContext db) : PageModel
{
    public List<ProductSummary> Products { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Products = await db.Products.AsNoTracking().OrderBy(product => product.Gtin)
            .Select(product => new ProductSummary(product.Gtin, product.Batch.Model.Name, product.Batch.Number))
            .ToListAsync(cancellationToken);
    }
}
