using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Data;

namespace MiniDpp.Passports;

public sealed record PassportData(string Gtin, string ModelName, string BatchNumber,
    decimal CottonPercentage, decimal ElastanePercentage, string ProductionLocation, decimal? CarbonKgCo2e)
{
    public string Material => $"%{CottonPercentage.ToString(CultureInfo.InvariantCulture)} pamuk, %{ElastanePercentage.ToString(CultureInfo.InvariantCulture)} elastan";
}

public static class DemoVisibility
{
    public static bool IsAuthorized(IQueryCollection query) => query.TryGetValue("rol", out var role)
        && role.Count == 1 && string.Equals(role[0], "yetkili", StringComparison.Ordinal);
}

public sealed class PassportService(DppDbContext db)
{
    public Task<PassportData?> FindAsync(string gtin, bool includeCarbon, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking().Where(product => product.Gtin == gtin)
            .Select(product => new PassportData(product.Gtin, product.Batch.Model.Name, product.Batch.Number,
                product.Batch.Model.CottonPercentage, product.Batch.Model.ElastanePercentage,
                product.Batch.ProductionLocation, includeCarbon ? (decimal?)product.CarbonKgCo2e : null))
            .SingleOrDefaultAsync(cancellationToken);
}
