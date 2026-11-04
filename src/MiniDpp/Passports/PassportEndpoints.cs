namespace MiniDpp.Passports;

public static class PassportEndpoints
{
    public static void MapPassportEndpoints(this WebApplication app)
    {
        app.MapGet("/dpp/{gtin}.jsonld", JsonLdAsync);
        app.MapGet("/dpp/{gtin}/qr.png", QrAsync);
    }

    private static async Task<IResult> QrAsync(string gtin, PassportService service, DppLinks links,
        CancellationToken cancellationToken)
    {
        if (await service.FindAsync(gtin, false, cancellationToken) is null) return Results.NotFound();
        using var generator = new QRCoder.QRCodeGenerator();
        using var data = generator.CreateQrCode(links.Html(gtin), QRCoder.QRCodeGenerator.ECCLevel.M);
        using var png = new QRCoder.PngByteQRCode(data);
        return Results.File(png.GetGraphic(8), "image/png");
    }

    private static async Task<IResult> JsonLdAsync(string gtin, HttpContext context,
        PassportService service, DppLinks links, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var passport = await service.FindAsync(gtin, DemoVisibility.IsAuthorized(context.Request.Query), cancellationToken);
        if (passport is null) return Results.NotFound();
        var properties = new List<Dictionary<string, object>>
        {
            Property("batch_no", "Batch numarası", passport.BatchNumber),
            Property("uretim_yeri", "Üretim yeri", passport.ProductionLocation),
            Property("elyaf_pamuk_yuzde", "Pamuk oranı", passport.CottonPercentage, "%"),
            Property("elyaf_elastan_yuzde", "Elastan oranı", passport.ElastanePercentage, "%")
        };
        if (passport.CarbonKgCo2e is { } carbon)
            properties.Add(Property("karbon_kg_co2e", "Karbon ayak izi", carbon, "kg CO2e"));
        return Results.Json(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org", ["@type"] = "Product", ["@id"] = links.Html(gtin),
            ["url"] = links.Html(gtin), ["gtin14"] = passport.Gtin, ["name"] = passport.ModelName,
            ["model"] = passport.ModelName, ["material"] = passport.Material, ["additionalProperty"] = properties
        }, contentType: "application/ld+json");
    }

    private static Dictionary<string, object> Property(string id, string name, object value, string? unit = null)
    {
        var property = new Dictionary<string, object>
        {
            ["@type"] = "PropertyValue", ["propertyID"] = id, ["name"] = name, ["value"] = value
        };
        if (unit is not null) property["unitText"] = unit;
        return property;
    }
}
