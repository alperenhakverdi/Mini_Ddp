using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Data;

namespace MiniDpp.Passports;

public sealed record PassportData(string Gtin, string ModelName, string BatchNumber,
    decimal CottonPercentage, decimal ElastanePercentage, string ProductionLocation, decimal? CarbonKgCo2e,
    int Version, DateTimeOffset UpdatedAt)
{
    public string Material => $"%{CottonPercentage.ToString(CultureInfo.InvariantCulture)} pamuk, %{ElastanePercentage.ToString(CultureInfo.InvariantCulture)} elastan";
}

public sealed record ArchivedPassport(PassportData Data, DateTimeOffset ArchivedAt);

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
                product.Batch.ProductionLocation, includeCarbon ? (decimal?)product.CarbonKgCo2e : null,
                product.Version, product.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<List<ArchivedPassport>> HistoryAsync(string gtin, bool includeCarbon, CancellationToken cancellationToken) =>
        db.ProductVersions.AsNoTracking().Where(version => version.Gtin == gtin).OrderByDescending(version => version.Version)
            .Select(version => new ArchivedPassport(new PassportData(version.Gtin, version.ModelName, version.BatchNumber,
                version.CottonPercentage, version.ElastanePercentage, version.ProductionLocation,
                includeCarbon ? (decimal?)version.CarbonKgCo2e : null, version.Version, version.UpdatedAt), version.ArchivedAt))
            .ToListAsync(cancellationToken);
}
