using Microsoft.EntityFrameworkCore;
using MiniDpp.Domain;

namespace MiniDpp.Data;

public enum SaveOutcome { Created, Updated, Unchanged }

public sealed class ProductConflictException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public sealed class ProductStore(DppDbContext db)
{
    public async Task<SaveOutcome> SaveAsync(ProductData data, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await db.Products.Include(product => product.Batch).ThenInclude(batch => batch.Model)
                .SingleOrDefaultAsync(product => product.Gtin == data.Gtin, cancellationToken);
            if (existing is not null && ToData(existing) == data) return SaveOutcome.Unchanged;

            var model = await db.Models.SingleOrDefaultAsync(model => model.Name == data.ModelName, cancellationToken);
            if (model is not null && (model.CottonPercentage != data.CottonPercentage
                || model.ElastanePercentage != data.ElastanePercentage))
                throw new ProductConflictException("model_adi", "Bu model farklı elyaf oranlarıyla zaten kayıtlıdır.");

            model ??= new ProductModel
            {
                Name = data.ModelName, CottonPercentage = data.CottonPercentage,
                ElastanePercentage = data.ElastanePercentage
            };
            var batch = model.Id == 0 ? null : await db.Batches.SingleOrDefaultAsync(
                batch => batch.ModelId == model.Id && batch.Number == data.BatchNumber, cancellationToken);
            if (batch is not null && batch.ProductionLocation != data.ProductionLocation)
                throw new ProductConflictException("uretim_yeri", "Bu model/batch farklı üretim yeriyle zaten kayıtlıdır.");

            batch ??= new Batch { Model = model, Number = data.BatchNumber, ProductionLocation = data.ProductionLocation };
            var now = DateTimeOffset.UtcNow;
            if (existing is null)
            {
                db.Products.Add(new Product
                {
                    Gtin = data.Gtin, Batch = batch, CarbonKgCo2e = data.CarbonKgCo2e,
                    Version = 1, CreatedAt = now, UpdatedAt = now
                });
            }
            else
            {
                db.ProductVersions.Add(new ProductVersion
                {
                    ProductId = existing.Id, Version = existing.Version, Gtin = existing.Gtin,
                    ModelName = existing.Batch.Model.Name, BatchNumber = existing.Batch.Number,
                    CottonPercentage = existing.Batch.Model.CottonPercentage,
                    ElastanePercentage = existing.Batch.Model.ElastanePercentage,
                    ProductionLocation = existing.Batch.ProductionLocation, CarbonKgCo2e = existing.CarbonKgCo2e,
                    CreatedAt = existing.CreatedAt, UpdatedAt = existing.UpdatedAt, ArchivedAt = now
                });
                existing.Batch = batch;
                existing.CarbonKgCo2e = data.CarbonKgCo2e;
                existing.Version = checked(existing.Version + 1);
                existing.UpdatedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return existing is null ? SaveOutcome.Created : SaveOutcome.Updated;
        }
        finally
        {
            // Başarısız bir satırın izlenen entity'leri sonraki satıra taşınmamalı.
            db.ChangeTracker.Clear();
        }
    }

    public static ProductData ToData(Product product) => new(product.Gtin, product.Batch.Model.Name,
        product.Batch.Number, product.Batch.Model.CottonPercentage, product.Batch.Model.ElastanePercentage,
        product.Batch.ProductionLocation, product.CarbonKgCo2e);
}
