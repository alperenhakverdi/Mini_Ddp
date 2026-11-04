using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniDpp.Data;
using MiniDpp.Importing;

namespace MiniDpp.Tests;

public sealed class ImportApiTests
{
    public const string Header = "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e";
    public const string Valid = "08690000000012,Denim,B01,98,2,Denizli,12.4";

    public static async Task<HttpResponseMessage> Upload(HttpClient client, string? csv = null)
    {
        using var form = new MultipartFormDataContent();
        var bytes = csv is null ? await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "urunler_duzeltilmis_test.csv")) : Encoding.UTF8.GetBytes(csv);
        form.Add(new ByteArrayContent(bytes), "file", "urunler.csv");
        return await client.PostAsync("/api/imports", form);
    }

    [Fact]
    public async Task Import_PartialSuccessThenRepeat_ReportsRowsAndKeepsRelationsClean()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var first = await Upload(client);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var report = (await first.Content.ReadFromJsonAsync<ImportReport>())!;
        Assert.Equal(5, report.Toplam);
        Assert.Equal(3, report.Kaydedilen);
        Assert.Equal(2, report.Reddedilen);
        Assert.True(report.Tamamlandi);
        Assert.Equal(new[] { 4, 5 }, report.Hatalar.Select(error => error.Satir));
        Assert.Equal("https://dpp.example.test/dpp/08690000000012", report.Urunler[0].Html);
        using var second = await Upload(client);
        var repeated = (await second.Content.ReadFromJsonAsync<ImportReport>())!;
        Assert.Equal(0, repeated.Kaydedilen);
        Assert.Equal(3, repeated.Degismeyen);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
        Assert.Equal(3, await db.Products.CountAsync());
        Assert.Equal(3, await db.Models.CountAsync());
        Assert.False(await db.Batches.AnyAsync(batch => batch.Number == "B2027-04"));
        Assert.True(await db.Products.AnyAsync(product => product.Gtin == "08690000000050"));
    }

    [Fact]
    public async Task Conflict_RejectsOnlyThatRowAndReportsUnchangedDuplicate()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var response = await Upload(client, Header + "\n" + Valid + "\n" + Valid.Replace("Denizli", "Bursa") + "\n" + Valid
            + "\n08690000000029,Denim,B02,98,2,Bursa,0");
        var report = (await response.Content.ReadFromJsonAsync<ImportReport>())!;
        Assert.Equal(2, report.Kaydedilen);
        Assert.Equal(1, report.Degismeyen);
        Assert.Equal(1, report.Reddedilen);
    }

    [Fact]
    public async Task MalformedTail_ReportsIncompleteImportAndPreservesValidPrefix()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var response = await Upload(client, Header + "\n" + Valid + "\n\"unfinished");
        var report = (await response.Content.ReadFromJsonAsync<ImportReport>())!;
        Assert.False(report.Tamamlandi);
        Assert.Equal(1, report.Kaydedilen);
        Assert.Equal(1, report.Reddedilen);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong,header\n" + Valid)]
    public async Task InvalidFile_IsBadRequestAndWritesNothing(string csv)
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var response = await Upload(client, csv);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DppDbContext>().Products.CountAsync());
    }

    [Fact]
    public async Task MissingFile_AndWrongContentType_AreBadRequest()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("x"), "other");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/imports", form)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/imports", new StringContent("csv"))).StatusCode);
    }

    [Fact]
    public async Task OversizedFile_IsPayloadTooLarge()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[ImportEndpoints.MaxFileBytes + 1]), "file", "big.csv");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/imports", form)).StatusCode);
    }
}
