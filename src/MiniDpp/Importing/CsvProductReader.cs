using System.Globalization;
using System.Text;
using CsvHelper;
using MiniDpp.Domain;

namespace MiniDpp.Importing;

public sealed record RowError(int Satir, string Alan, string Mesaj);
public sealed record CsvProductRow(int Satir, ProductData? Product, IReadOnlyList<RowError> Errors);
public sealed record CsvDocument(IReadOnlyList<CsvProductRow> Rows, bool Completed, string? FileError = null);

public sealed class CsvProductReader
{
    public static readonly string[] Headers =
    [
        "gtin", "model_adi", "batch_no", "elyaf_pamuk_yuzde", "elyaf_elastan_yuzde", "uretim_yeri", "karbon_kg_co2e"
    ];

    public async Task<CsvDocument> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var rows = new List<CsvProductRow>();
        CsvMalformedRecord? malformed = null;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096, leaveOpen: true);
        using var csv = CsvReader.Create(reader, new CsvReaderOptions
        {
            HasHeader = false, SkipEmptyLines = false, Separator = ',', TrackLineNumbers = true,
            AllowLineBreaksInQuotes = true, MalformedData = CsvMalformedDataHandling.Recover,
            FieldCountMismatch = CsvFieldCountHandling.Allow, IncludeValuesInExceptions = false,
            OnMalformedRecord = record => malformed = record
        });

        var startLine = 1;
        var headerRead = false;
        try
        {
            if (!await csv.ReadAsync(cancellationToken)) return new(rows, false, "CSV dosyası boş.");
            if (malformed is not null) return new([], false, "CSV başlığında tırnak yapısı bozuk.");
            var header = Enumerable.Range(0, csv.FieldCount).Select(index => csv.GetString(index).Trim()).ToArray();
            header[0] = header[0].TrimStart('\uFEFF');
            if (header.Length != Headers.Length || header.Distinct(StringComparer.Ordinal).Count() != Headers.Length
                || Headers.Any(required => !header.Contains(required, StringComparer.Ordinal)))
                return new(rows, false, "CSV başlığı tam olarak yedi beklenen sütunu, tekrar etmeden içermelidir: " + string.Join(", ", Headers));
            headerRead = true;
            var indices = Headers.Select(required => Array.IndexOf(header, required)).ToArray();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                malformed = null;
                if (!await csv.ReadAsync(cancellationToken)) break;
                startLine = checked((int)csv.LineNumber);
                if (malformed is { } badRecord)
                {
                    var unrecoverable = badRecord.Reason == CsvMalformedReason.UnterminatedQuote;
                    rows.Add(new(startLine, null, [new(startLine, "csv", unrecoverable
                        ? "CSV tırnak yapısı bozuk. Sonraki kayıt sınırları belirlenemediği için okuma durduruldu."
                        : "CSV tırnak yapısı bozuk. Satır kaydedilmedi.")]));
                    if (unrecoverable) return new(rows, false);
                    continue;
                }
                var fields = Enumerable.Range(0, csv.FieldCount).Select(csv.GetString).ToArray();
                if (fields.Length == 1 && string.IsNullOrWhiteSpace(fields[0])) continue;
                if (fields.Length != Headers.Length)
                {
                    rows.Add(new(startLine, null, [new(startLine, "csv", $"Satırda 7 sütun olmalıdır. Mevcut: {fields.Length}.")]));
                    continue;
                }
                var values = indices.Select(index => fields[index].Trim()).ToArray();
                rows.Add(Validate(startLine, values));
            }
            return new(rows, true);
        }
        catch (CsvFormatException exception)
        {
            if (!headerRead) return new([], false, "CSV başlığı ayrıştırılamadı; tırnak kullanımını kontrol edin.");
            startLine = checked((int)exception.LineNumber);
            rows.Add(new(startLine, null, [new(startLine, "csv", "CSV tırnak yapısı bozuk. Sonraki kayıt sınırları güvenilir biçimde belirlenemediği için okuma durduruldu.")]));
            return new(rows, false);
        }
        catch (DecoderFallbackException)
        {
            return new([], false, "Dosya UTF-8 olarak kodlanmalıdır.");
        }
    }

    private static CsvProductRow Validate(int line, string[] values)
    {
        var errors = new List<RowError>();
        void Error(string field, string message) => errors.Add(new(line, field, message));
        if (values[0].Length == 0) Error("gtin", "GTIN boş olamaz.");
        else if (values[0].Length != 14 || values[0].Any(character => character is < '0' or > '9'))
            Error("gtin", "GTIN tam 14 haneli sayısal metin olmalıdır.");
        else if (!HasValidCheckDigit(values[0])) Error("gtin", "GTIN GS1 kontrol basamağı geçersizdir.");

        foreach (var index in new[] { 1, 2, 5 })
            if (values[index].Length == 0) Error(Headers[index], "Bu alan boş olamaz.");

        bool Percentage(int index, out decimal number)
        {
            if (!TryNumber(values[index], out number)) { Error(Headers[index], "Sayısal bir değer girilmelidir; ondalık ayracı nokta olmalıdır."); return false; }
            if (number is < 0 or > 100) { Error(Headers[index], "Elyaf oranı 0–100 aralığında olmalıdır."); return false; }
            return true;
        }
        var cottonValid = Percentage(3, out var cotton);
        var elastaneValid = Percentage(4, out var elastane);
        if (cottonValid && elastaneValid && cotton + elastane != 100)
            Error("elyaf_toplami", $"Pamuk ve elastan toplamı %100 olmalıdır. Mevcut toplam: %{(cotton + elastane).ToString(CultureInfo.InvariantCulture)}.");

        if (!TryNumber(values[6], out var carbon)) Error("karbon_kg_co2e", "Karbon değeri sayısal olmalıdır; ondalık ayracı nokta olmalıdır.");
        else if (carbon < 0) Error("karbon_kg_co2e", "Karbon değeri negatif olamaz.");

        return new(line, errors.Count == 0
            ? new ProductData(values[0], values[1], values[2], cotton, elastane, values[5], carbon) : null, errors);
    }

    private static bool TryNumber(string value, out decimal number) => decimal.TryParse(value,
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);

    private static bool HasValidCheckDigit(string gtin)
    {
        var sum = 0;
        for (var index = 0; index < 13; index++) sum += (gtin[index] - '0') * (index % 2 == 0 ? 3 : 1);
        return (10 - sum % 10) % 10 == gtin[13] - '0';
    }
}
