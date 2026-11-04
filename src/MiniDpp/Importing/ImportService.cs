using MiniDpp.Data;
using MiniDpp.Passports;

namespace MiniDpp.Importing;

public sealed record ImportedProduct(int Satir, string Gtin, string Durum, string Html, string Jsonld, string Qr);

public sealed class ImportReport
{
    public int Toplam { get; set; }
    public int Kaydedilen { get; set; }
    public int Guncellenen { get; set; }
    public int Degismeyen { get; set; }
    public int Reddedilen { get; set; }
    public bool Tamamlandi { get; set; }
    public string? DosyaHatasi { get; set; }
    public List<RowError> Hatalar { get; init; } = [];
    public List<ImportedProduct> Urunler { get; init; } = [];
}

public sealed class ImportService(CsvProductReader reader, ProductStore store, DppLinks links)
{
    public async Task<ImportReport> ImportAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var document = await reader.ReadAsync(stream, cancellationToken);
        var report = new ImportReport
        {
            Toplam = document.Rows.Count, Tamamlandi = document.Completed, DosyaHatasi = document.FileError
        };
        foreach (var row in document.Rows)
        {
            if (row.Product is null)
            {
                report.Hatalar.AddRange(row.Errors);
                report.Reddedilen++;
                continue;
            }
            try
            {
                var outcome = await store.SaveAsync(row.Product, cancellationToken);
                if (outcome == SaveOutcome.Created) report.Kaydedilen++;
                else if (outcome == SaveOutcome.Updated) report.Guncellenen++;
                else report.Degismeyen++;
                report.Urunler.Add(new(row.Satir, row.Product.Gtin,
                    outcome switch { SaveOutcome.Created => "kaydedildi", SaveOutcome.Updated => "guncellendi", _ => "degismedi" },
                    links.Html(row.Product.Gtin), links.JsonLd(row.Product.Gtin), links.Qr(row.Product.Gtin)));
            }
            catch (ProductConflictException exception)
            {
                report.Hatalar.Add(new(row.Satir, exception.Field, exception.Message));
                report.Reddedilen++;
            }
        }
        return report;
    }
}
