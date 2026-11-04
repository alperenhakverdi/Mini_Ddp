using Microsoft.Extensions.Options;
using MiniDpp.Configuration;

namespace MiniDpp.Passports;

public sealed class DppLinks(IOptions<DppOptions> options)
{
    public string Html(string gtin) => $"{options.Value.PublicBaseUrl.TrimEnd('/')}/dpp/{gtin}";
    public string JsonLd(string gtin) => Html(gtin) + ".jsonld";
    public string Qr(string gtin) => Html(gtin) + "/qr.png";
}
