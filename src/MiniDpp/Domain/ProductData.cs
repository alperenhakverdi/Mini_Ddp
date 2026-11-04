namespace MiniDpp.Domain;

public sealed record ProductData(
    string Gtin, string ModelName, string BatchNumber,
    decimal CottonPercentage, decimal ElastanePercentage,
    string ProductionLocation, decimal CarbonKgCo2e);
