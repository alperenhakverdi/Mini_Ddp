using System.Globalization;
using System.Text;
using MiniDpp.Importing;

namespace MiniDpp.Tests;

public sealed class CsvReaderTests
{
    private const string Header = "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e";
    private const string Valid = "08690000000012,Denim,B01,98,2,Denizli,12.4";
    private static async Task<CsvDocument> Read(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return await new CsvProductReader().ReadAsync(stream);
    }

    [Fact]
    public async Task SuppliedCsv_HasThreeValidRowsAndExpectedPhysicalErrors()
    {
        await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "urunler_duzeltilmis_test.csv"));
        var document = await new CsvProductReader().ReadAsync(stream);
        Assert.True(document.Completed);
        Assert.Equal(new[] { 2, 3, 6 }, document.Rows.Where(row => row.Product is not null).Select(row => row.Satir));
        Assert.Equal("gtin", Assert.Single(document.Rows.Single(row => row.Satir == 4).Errors).Alan);
        Assert.Equal("elyaf_toplami", Assert.Single(document.Rows.Single(row => row.Satir == 5).Errors).Alan);
    }

    [Theory]
    [InlineData("", 0, "gtin")]
    [InlineData("8690000000012", 0, "gtin")]
    [InlineData("08690000000013", 0, "gtin")]
    [InlineData("0869000000001A", 0, "gtin")]
    [InlineData("０8690000000012", 0, "gtin")]
    [InlineData(" ", 1, "model_adi")]
    [InlineData("", 2, "batch_no")]
    [InlineData("", 5, "uretim_yeri")]
    [InlineData("abc", 3, "elyaf_pamuk_yuzde")]
    [InlineData("101", 3, "elyaf_pamuk_yuzde")]
    [InlineData("-1", 4, "elyaf_elastan_yuzde")]
    [InlineData("3", 4, "elyaf_toplami")]
    [InlineData("NaN", 6, "karbon_kg_co2e")]
    [InlineData("-0.1", 6, "karbon_kg_co2e")]
    public async Task InvalidValues_AreRejected(string value, int index, string errorField)
    {
        var fields = Valid.Split(',');
        fields[index] = value;
        var result = await Read(Header + "\n" + string.Join(',', fields));
        var row = Assert.Single(result.Rows);
        Assert.Null(row.Product);
        Assert.Contains(row.Errors, error => error.Alan == errorField);
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("en-US")]
    public async Task DecimalParsing_IsIndependentOfCurrentCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var document = await Read(Header + "\n08690000000012,Denim,B01,97.5,2.5,Denizli,0");
            var product = Assert.Single(document.Rows).Product;
            Assert.NotNull(product);
            Assert.Equal(97.5m, product.CottonPercentage);
            Assert.Equal(0, product.CarbonKgCo2e);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("100,0")]
    [InlineData("0,100")]
    public async Task InclusiveFibreBounds_AreAccepted(string percentages)
    {
        var document = await Read(Header + "\n08690000000012,Denim,B01," + percentages + ",Denizli,0");
        Assert.NotNull(Assert.Single(document.Rows).Product);
    }

    [Fact]
    public async Task BomQuotesMultilineAndBlankLines_PreservePhysicalRowNumbers()
    {
        var document = await Read("\uFEFF" + Header + "\r\n\r\n08690000000012,\"Denim,\r\nModel\",B01,98,2,Denizli,12.4\r\n" + Valid);
        Assert.True(document.Completed);
        Assert.Equal(new[] { 3, 5 }, document.Rows.Select(row => row.Satir));
        Assert.Equal("Denim,\r\nModel", document.Rows[0].Product!.ModelName);
    }

    [Fact]
    public async Task WrongColumnCount_IsRejectedAndFollowingRowIsRead()
    {
        var document = await Read(Header + "\ninvalid,row\n" + Valid);
        Assert.Null(document.Rows[0].Product);
        Assert.NotNull(document.Rows[1].Product);
        Assert.Equal(3, document.Rows[1].Satir);
    }

    [Fact]
    public async Task UnclosedQuote_StopsReadingWithExplicitError()
    {
        var document = await Read(Header + "\n" + Valid + "\n08690000000029,\"unfinished,B01,99,1,Denizli,0");
        Assert.False(document.Completed);
        Assert.NotNull(document.Rows[0].Product);
        Assert.Equal(3, document.Rows[1].Satir);
        Assert.Null(document.Rows[1].Product);
    }

    [Theory]
    [InlineData("")]
    [InlineData("gtin,model_adi")]
    [InlineData("gtin,gtin,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e")]
    public async Task InvalidHeader_IsFileError(string header)
    {
        var document = await Read(header);
        Assert.NotNull(document.FileError);
        Assert.Empty(document.Rows);
    }

    [Fact]
    public async Task ReorderedHeaders_AreMappedByName()
    {
        var document = await Read(string.Join(',', Header.Split(',').Reverse()) + "\n" + string.Join(',', Valid.Split(',').Reverse()));
        Assert.Equal("08690000000012", Assert.Single(document.Rows).Product!.Gtin);
    }

    [Fact]
    public async Task MultipleIndependentErrors_AreReportedTogether()
    {
        var document = await Read(Header + "\n,,,98,2,,-1");
        Assert.Equal(5, Assert.Single(document.Rows).Errors.Count);
    }

    [Fact]
    public async Task InvalidUtf8_IsFileError()
    {
        using var stream = new MemoryStream([0xff, 0xff, 0xff]);
        Assert.NotNull((await new CsvProductReader().ReadAsync(stream)).FileError);
    }
}
