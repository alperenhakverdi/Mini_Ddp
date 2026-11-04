using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniDpp.Data;
using MiniDpp.Domain;

namespace MiniDpp.Tests;

public sealed class PersistenceTests
{
    private static ProductData Sample => new("08690000000012", "Denim", "B01", 98, 2, "Denizli", 12.4m);

    [Fact]
    public async Task Store_PersistsRelationsAndLeadingZeros_WhenDatabaseIsReopened()
    {
        await using var factory = new DppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ProductStore>();
            Assert.Equal(SaveOutcome.Created, await store.SaveAsync(Sample));
            Assert.Equal(SaveOutcome.Unchanged, await store.SaveAsync(Sample));
            Assert.Equal(SaveOutcome.Created, await store.SaveAsync(Sample with { Gtin = "08690000000029" }));
        }
        await using var reopened = new DppDbContext(new DbContextOptionsBuilder<DppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = factory.DatabasePath, Pooling = false }.ToString()).Options);
        Assert.Equal(2, await reopened.Products.CountAsync());
        Assert.Equal(1, await reopened.Models.CountAsync());
        Assert.Equal(1, await reopened.Batches.CountAsync());
        var product = await reopened.Products.Include(product => product.Batch).ThenInclude(batch => batch.Model)
            .SingleAsync(product => product.Gtin == Sample.Gtin);
        Assert.Equal(Sample, ProductStore.ToData(product));
    }

    [Fact]
    public async Task Conflicts_DoNotLeaveEntitiesOrContaminateNextRow()
    {
        await using var factory = new DppFactory();
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ProductStore>();
        await store.SaveAsync(Sample);
        await Assert.ThrowsAsync<ProductConflictException>(() => store.SaveAsync(Sample with { ProductionLocation = "Bursa" }));
        await Assert.ThrowsAsync<ProductConflictException>(() => store.SaveAsync(Sample with
            { Gtin = "08690000000029", CottonPercentage = 97, ElastanePercentage = 3 }));
        await Assert.ThrowsAsync<ProductConflictException>(() => store.SaveAsync(Sample with
            { Gtin = "08690000000029", ProductionLocation = "Bursa" }));
        // GTIN check constraint başarısızlığı, yeni model ve batch'i de geri almalı.
        await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(Sample with
            { Gtin = "invalid", ModelName = "Başarısız", BatchNumber = "BAD" }));
        await store.SaveAsync(Sample with { Gtin = "08690000000029", BatchNumber = "B02" });
        var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
        Assert.Equal(1, await db.Models.CountAsync());
        Assert.Equal(2, await db.Batches.CountAsync());
        Assert.Equal(2, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Database_EnforcesUniqueGtinAndForeignKeys()
    {
        await using var factory = new DppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
        await scope.ServiceProvider.GetRequiredService<ProductStore>().SaveAsync(Sample);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Products (Gtin, BatchId, CarbonKgCo2e, CreatedAt, UpdatedAt) SELECT Gtin, BatchId, CarbonKgCo2e, CreatedAt, UpdatedAt FROM Products"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE Products SET BatchId = 99999"));
    }
}
