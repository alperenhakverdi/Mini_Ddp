namespace MiniDpp.Data;

public sealed class ProductModel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal CottonPercentage { get; set; }
    public decimal ElastanePercentage { get; set; }
}

public sealed class Batch
{
    public int Id { get; set; }
    public int ModelId { get; set; }
    public required ProductModel Model { get; set; }
    public required string Number { get; set; }
    public required string ProductionLocation { get; set; }
}

public sealed class Product
{
    public int Id { get; set; }
    public required string Gtin { get; set; }
    public int BatchId { get; set; }
    public required Batch Batch { get; set; }
    public decimal CarbonKgCo2e { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
