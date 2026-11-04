using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MiniDpp.Data;
using MiniDpp.Domain;
using MiniDpp.Importing;

namespace MiniDpp.Tests;

public sealed class HistoryTests
{
    private static ProductData Sample => new("08690000000012", "Denim", "B01", 98, 2, "Denizli", 12.4m);

    [Fact]
    public async Task CsvUpdates_ArchiveFullSnapshots_AndUnchangedUploadDoesNotAddVersion()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var first = await ImportApiTests.Upload(client, ImportApiTests.Header + "\n" + ImportApiTests.Valid);
        var secondCsv = ImportApiTests.Header + "\n" + ImportApiTests.Valid.Replace("12.4", "10.7");
        using var second = await ImportApiTests.Upload(client, secondCsv);
        var updated = (await second.Content.ReadFromJsonAsync<ImportReport>())!;
        Assert.Equal(1, updated.Guncellenen);
        Assert.Equal(0, updated.Kaydedilen);
        Assert.Equal("guncellendi", Assert.Single(updated.Urunler).Durum);
        DateTimeOffset updatedAt;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
            var current = await db.Products.SingleAsync();
            updatedAt = current.UpdatedAt;
            Assert.Equal(2, current.Version);
            Assert.Equal(10.7m, current.CarbonKgCo2e);
            var archived = await db.ProductVersions.SingleAsync();
            Assert.Equal(1, archived.Version);
            Assert.Equal(current.CreatedAt, archived.CreatedAt);
            Assert.Equal(archived.CreatedAt, archived.UpdatedAt);
            Assert.Equal(current.UpdatedAt, archived.ArchivedAt);
        }
        using var repeat = await ImportApiTests.Upload(client, secondCsv);
        var unchanged = (await repeat.Content.ReadFromJsonAsync<ImportReport>())!;
        Assert.Equal(1, unchanged.Degismeyen);
        Assert.Equal(0, unchanged.Guncellenen);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
            Assert.Equal(updatedAt, (await db.Products.SingleAsync()).UpdatedAt);
            Assert.Equal(1, await db.ProductVersions.CountAsync());
        }
        using var third = await ImportApiTests.Upload(client, ImportApiTests.Header + "\n08690000000012,Denim V3,B03,97,3,Bursa,7");
        Assert.Equal(1, (await third.Content.ReadFromJsonAsync<ImportReport>())!.Guncellenen);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
            var history = await db.ProductVersions.OrderBy(version => version.Version).ToListAsync();
            Assert.Equal(new[] { 1, 2 }, history.Select(version => version.Version));
            Assert.All(history, version =>
            {
                Assert.Equal(Sample.Gtin, version.Gtin);
                Assert.Equal(Sample.ModelName, version.ModelName);
                Assert.Equal(Sample.BatchNumber, version.BatchNumber);
                Assert.Equal(98, version.CottonPercentage);
                Assert.Equal(2, version.ElastanePercentage);
                Assert.Equal("Denizli", version.ProductionLocation);
                Assert.Equal(TimeSpan.Zero, version.UpdatedAt.Offset);
            });
            Assert.Equal(12.4m, history[0].CarbonKgCo2e);
            Assert.Equal(10.7m, history[1].CarbonKgCo2e);
            var current = await db.Products.Include(product => product.Batch).ThenInclude(batch => batch.Model).SingleAsync();
            Assert.Equal(3, current.Version);
            Assert.Equal("Denim V3", current.Batch.Model.Name);
            Assert.Equal("Bursa", current.Batch.ProductionLocation);
        }
        using var json = JsonDocument.Parse(await client.GetStringAsync("/dpp/08690000000012.jsonld?rol=yetkili"));
        var properties = json.RootElement.GetProperty("additionalProperty").EnumerateArray().ToArray();
        Assert.Equal(3, properties.Single(property => property.GetProperty("propertyID").GetString() == "surum").GetProperty("value").GetInt32());
        Assert.Equal(7, properties.Single(property => property.GetProperty("propertyID").GetString() == "karbon_kg_co2e").GetProperty("value").GetDecimal());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?rol=unknown")]
    [InlineData("?rol=yetkili")]
    public async Task HistoryPage_UsesSameCarbonVisibilityAndEncodesArchivedText(string query)
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var first = await ImportApiTests.Upload(client, ImportApiTests.Header + "\n" + ImportApiTests.Valid.Replace("Denim", "<script>old</script>"));
        using var update = await ImportApiTests.Upload(client, ImportApiTests.Header + "\n" + ImportApiTests.Valid);
        using var response = await client.GetAsync("/dpp/08690000000012/history" + query);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl!.NoStore);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;old&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("08690000000012", html);
        if (query == "?rol=yetkili") Assert.Contains("12.4 kg CO2e", html);
        else
        {
            Assert.DoesNotContain("kg CO2e", html);
            Assert.DoesNotContain("karbon", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task HistoryPage_HandlesEmptyHistoryAndUnknownProduct()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dpp/08690000000012/history")).StatusCode);
        using var first = await ImportApiTests.Upload(client, ImportApiTests.Header + "\n" + ImportApiTests.Valid);
        Assert.Contains("henüz önceki sürüm bulunmuyor", WebUtility.HtmlDecode(
            await client.GetStringAsync("/dpp/08690000000012/history")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SharedDefinitionConflictAndInvalidRow_DoNotUpdateOrArchiveAnyProduct()
    {
        await using var factory = new DppFactory();
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ProductStore>();
        var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
        await store.SaveAsync(Sample);
        await store.SaveAsync(Sample with { Gtin = "08690000000029" });
        await Assert.ThrowsAsync<ProductConflictException>(() => store.SaveAsync(Sample with { CottonPercentage = 99, ElastanePercentage = 1 }));
        await Assert.ThrowsAsync<ProductConflictException>(() => store.SaveAsync(Sample with { ProductionLocation = "Bursa" }));
        using var client = factory.CreateClient();
        using var invalid = await ImportApiTests.Upload(client, ImportApiTests.Header + "\n" + ImportApiTests.Valid.Replace(",98,2,", ",95,2,"));
        var report = (await invalid.Content.ReadFromJsonAsync<ImportReport>())!;
        Assert.Equal(1, report.Reddedilen);
        Assert.Equal(0, report.Guncellenen);
        Assert.Empty(await db.ProductVersions.ToListAsync());
        Assert.All(await db.Products.ToListAsync(), product => Assert.Equal(1, product.Version));
    }

    [Fact]
    public async Task FailedArchive_RollsBackUpdateAndNewRelations_ThenNextUpdateSucceeds()
    {
        await using var factory = new DppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<ProductStore>();
        await store.SaveAsync(Sample);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectArchive BEFORE INSERT ON ProductVersions
            BEGIN SELECT RAISE(ABORT, 'Simulated archive failure'); END;
            """);
        var replacement = Sample with { ModelName = "Yeni model", BatchNumber = "NEW", ProductionLocation = "Bursa", CarbonKgCo2e = 0 };
        await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(replacement));
        Assert.Equal(1, await db.Models.CountAsync());
        Assert.Equal(1, await db.Batches.CountAsync());
        Assert.Equal(0, await db.ProductVersions.CountAsync());
        var unchanged = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(1, unchanged.Version);
        Assert.Equal(Sample.CarbonKgCo2e, unchanged.CarbonKgCo2e);
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectArchive");
        Assert.Equal(SaveOutcome.Updated, await store.SaveAsync(replacement));
        Assert.Equal(1, await db.ProductVersions.CountAsync());
        Assert.Equal(2, (await db.Products.AsNoTracking().SingleAsync()).Version);
    }

    [Fact]
    public async Task Migration_UpgradesExistingDatabaseWithoutLosingProducts()
    {
        await using var factory = new DppFactory();
        Directory.CreateDirectory(factory.DirectoryPath);
        await using var db = new DppDbContext(new DbContextOptionsBuilder<DppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = factory.DatabasePath, Pooling = false }.ToString()).Options);
        await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Models (Id, Name, CottonPercentage, ElastanePercentage) VALUES (1, 'Legacy', '98.0', '2.0');
            INSERT INTO Batches (Id, ModelId, Number, ProductionLocation) VALUES (1, 1, 'LEGACY-01', 'Denizli');
            INSERT INTO Products (Gtin, BatchId, CarbonKgCo2e, CreatedAt, UpdatedAt)
            VALUES ('08690000000012', 1, '12.4', '2026-01-01 00:00:00+00:00', '2026-01-01 00:00:00+00:00');
            """);
        await db.Database.MigrateAsync();
        Assert.Equal(1, (await db.Products.SingleAsync()).Version);
        Assert.Empty(await db.ProductVersions.ToListAsync());
        var store = new ProductStore(db);
        Assert.Equal(SaveOutcome.Updated, await store.SaveAsync(Sample with { ModelName = "Legacy", BatchNumber = "LEGACY-01", CarbonKgCo2e = 10 }));
        var archived = await db.ProductVersions.SingleAsync();
        Assert.Equal(1, archived.Version);
        Assert.Equal(12.4m, archived.CarbonKgCo2e);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO ProductVersions
            (ProductId, Version, Gtin, ModelName, BatchNumber, CottonPercentage, ElastanePercentage,
             ProductionLocation, CarbonKgCo2e, CreatedAt, UpdatedAt, ArchivedAt)
            SELECT ProductId, Version, Gtin, ModelName, BatchNumber, CottonPercentage, ElastanePercentage,
                   ProductionLocation, CarbonKgCo2e, CreatedAt, UpdatedAt, ArchivedAt FROM ProductVersions;
            """));
    }
}
