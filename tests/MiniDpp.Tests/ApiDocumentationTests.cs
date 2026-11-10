using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniDpp.Data;

namespace MiniDpp.Tests;

public sealed class ApiDocumentationTests
{
    [Fact]
    public async Task OpenApi_DescribesActualUploadFieldAndImportResponse_WithoutWritingProducts()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.StartsWith("3.0", root.GetProperty("openapi").GetString());
        Assert.Equal("Mini DPP API", root.GetProperty("info").GetProperty("title").GetString());
        var import = root.GetProperty("paths").GetProperty("/api/imports").GetProperty("post");
        var body = import.GetProperty("requestBody");
        Assert.True(body.GetProperty("required").GetBoolean());
        var schema = body.GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Contains("file", schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var file = schema.GetProperty("properties").GetProperty("file");
        Assert.Equal("string", file.GetProperty("type").GetString());
        Assert.Equal("binary", file.GetProperty("format").GetString());
        foreach (var code in new[] { "200", "400", "413" })
            Assert.True(import.GetProperty("responses").TryGetProperty(code, out _));
        var report = root.GetProperty("components").GetProperty("schemas").GetProperty("ImportReport").GetProperty("properties");
        Assert.True(report.TryGetProperty("hatalar", out _));
        Assert.True(report.TryGetProperty("guncellenen", out _));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DppDbContext>().Products.CountAsync());
    }

    [Fact]
    public async Task OpenApi_DescribesJsonLdRoleAndBinaryQr()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var jsonLd = paths.GetProperty("/dpp/{gtin}.jsonld").GetProperty("get");
        var parameters = jsonLd.GetProperty("parameters").EnumerateArray().ToArray();
        var gtin = parameters.Single(parameter => parameter.GetProperty("name").GetString() == "gtin");
        Assert.True(gtin.GetProperty("required").GetBoolean());
        Assert.Equal("08690000000012", gtin.GetProperty("example").GetString());
        var role = parameters.Single(parameter => parameter.GetProperty("name").GetString() == "rol");
        Assert.Equal("query", role.GetProperty("in").GetString());
        Assert.False(role.TryGetProperty("required", out var required) && required.GetBoolean());
        Assert.Equal("tuketici", role.GetProperty("example").GetString());
        Assert.True(jsonLd.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("application/ld+json", out _));
        var qr = paths.GetProperty("/dpp/{gtin}/qr.png").GetProperty("get");
        Assert.Equal("binary", qr.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("image/png").GetProperty("schema").GetProperty("format").GetString());
        Assert.True(qr.GetProperty("responses").TryGetProperty("404", out _));
    }

    [Fact]
    public async Task SwaggerUi_AndLocalAssets_AreAvailableInProductionDemo()
    {
        await using var factory = new DppFactory();
        await using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = production.CreateClient();
        var html = await client.GetStringAsync("/swagger");
        Assert.Contains("Swagger UI", html);
        Assert.Contains("/swagger-ui/swagger-ui-bundle.js", html);
        Assert.Contains("/swagger-ui/swagger-ui.css", html);
        Assert.DoesNotContain("cdn.jsdelivr.net", html);
        foreach (var path in new[] { "/openapi/v1.json", "/swagger-ui/swagger-ui-bundle.js", "/swagger-ui/swagger-ui.css", "/swagger-init.js" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Contains("/swagger", await client.GetStringAsync("/"));
    }
}
