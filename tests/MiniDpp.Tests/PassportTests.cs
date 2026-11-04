using System.Net;
using System.Text.Json;

namespace MiniDpp.Tests;

public sealed class PassportTests
{
    [Theory]
    [InlineData("")]
    [InlineData("?rol=tuketici")]
    [InlineData("?rol=unknown")]
    [InlineData("?rol=YETKILI")]
    [InlineData("?rol=yetkili&rol=tuketici")]
    public async Task ConsumerViews_ContainNoCarbonFieldOrValue(string query)
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var uploaded = await ImportApiTests.Upload(client);
        foreach (var suffix in new[] { "", ".jsonld" })
        {
            var response = await client.GetAsync("/dpp/08690000000012" + suffix + query);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("karbon", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Carbon", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("kg CO2e", text);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }

    [Fact]
    public async Task AuthorizedViews_IncludeNumericCarbonAndUnit_WithoutAffectingNextConsumerRequest()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var uploaded = await ImportApiTests.Upload(client);
        var html = await client.GetStringAsync("/dpp/08690000000012?rol=yetkili");
        Assert.Contains("12.4 kg CO2e", html);
        Assert.Contains("?rol=yetkili", html);
        using var jsonResponse = await client.GetAsync("/dpp/08690000000012.jsonld?rol=yetkili");
        Assert.Equal("application/ld+json", jsonResponse.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await jsonResponse.Content.ReadAsStringAsync());
        var carbon = json.RootElement.GetProperty("additionalProperty").EnumerateArray()
            .Single(property => property.GetProperty("propertyID").GetString() == "karbon_kg_co2e");
        Assert.Equal(12.4m, carbon.GetProperty("value").GetDecimal());
        Assert.Equal("kg CO2e", carbon.GetProperty("unitText").GetString());
        Assert.DoesNotContain("kg CO2e", await client.GetStringAsync("/dpp/08690000000012"));
    }

    [Fact]
    public async Task JsonLd_UsesProductSchemaAndPreservesGtinAsString()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var uploaded = await ImportApiTests.Upload(client);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/dpp/08690000000012.jsonld"));
        var product = json.RootElement;
        Assert.Equal("https://schema.org", product.GetProperty("@context").GetString());
        Assert.Equal("Product", product.GetProperty("@type").GetString());
        Assert.Equal("08690000000012", product.GetProperty("gtin14").GetString());
        Assert.Equal("https://dpp.example.test/dpp/08690000000012", product.GetProperty("url").GetString());
        Assert.Equal(product.GetProperty("url").GetString(), product.GetProperty("@id").GetString());
        Assert.Equal("Slim Denim 501", product.GetProperty("name").GetString());
        Assert.Equal(6, product.GetProperty("additionalProperty").GetArrayLength());
    }

    [Fact]
    public async Task Html_EncodesCsvTextAndAgreesWithJsonLd()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var uploaded = await ImportApiTests.Upload(client, ImportApiTests.Header + "\n" + ImportApiTests.Valid.Replace("Denim", "<script>alert(1)</script>"));
        var html = await client.GetStringAsync("/dpp/08690000000012");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("08690000000012", html);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/dpp/08690000000012.jsonld"));
        Assert.Equal("<script>alert(1)</script>", json.RootElement.GetProperty("name").GetString());
        Assert.Contains(json.RootElement.GetProperty("material").GetString()!, html);
    }

    [Theory]
    [InlineData("/dpp/00000000000000")]
    [InlineData("/dpp/00000000000000.jsonld")]
    public async Task MissingProduct_ReturnsNotFound(string url)
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }
}
