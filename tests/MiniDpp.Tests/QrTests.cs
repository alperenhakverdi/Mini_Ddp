using System.Net;
using StbImageSharp;
using ZXing;

namespace MiniDpp.Tests;

public sealed class QrTests
{
    [Fact]
    public async Task Png_DecodesToCanonicalConsumerUrl_EvenWhenRoleIsProvided()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var uploaded = await ImportApiTests.Upload(client);
        foreach (var gtin in new[] { "08690000000012", "08690000000029", "08690000000050" })
        {
            using var response = await client.GetAsync($"/dpp/{gtin}/qr.png?rol=yetkili");
            response.EnsureSuccessStatusCode();
            Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8));
            var image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
            var decoded = new BarcodeReaderGeneric().Decode(new RGBLuminanceSource(image.Data, image.Width, image.Height,
                RGBLuminanceSource.BitmapFormat.RGBA32));
            Assert.NotNull(decoded);
            Assert.Equal($"https://dpp.example.test/dpp/{gtin}", decoded.Text);
        }
    }

    [Fact]
    public async Task MissingProduct_HasNoQrCode()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dpp/00000000000000/qr.png")).StatusCode);
    }

    [Fact]
    public async Task HomeAndPassport_LinkToAllOutputsWithoutExposingCarbon()
    {
        await using var factory = new DppFactory();
        using var client = factory.CreateClient();
        using var uploaded = await ImportApiTests.Upload(client);
        var home = await client.GetStringAsync("/");
        Assert.Contains("/dpp/08690000000012.jsonld", home);
        Assert.Contains("/dpp/08690000000012/qr.png", home);
        Assert.Contains("name=\"file\"", home);
        Assert.DoesNotContain("12.4", home);
        var passport = await client.GetStringAsync("/dpp/08690000000012");
        Assert.Contains("src=\"/dpp/08690000000012/qr.png\"", passport);
    }
}
