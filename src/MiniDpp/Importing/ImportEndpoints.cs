namespace MiniDpp.Importing;

public static class ImportEndpoints
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 64 * 1024;

    public static void MapImportEndpoints(this WebApplication app) =>
        app.MapPost("/api/imports", ImportAsync)
            .WithName("ImportProducts")
            .WithTags("CSV içe aktarma")
            .WithSummary("CSV dosyasını doğrula ve ürünleri kaydet")
            .WithDescription("multipart/form-data içindeki file alanında UTF-8 CSV gönderin (en fazla 5 MiB). "
                + "Hatalı satırlar kaydedilmez, geçerli satırlar işlenir. Aynı GTIN ve aynı veride değişiklik yapılmaz; "
                + "farklı geçerli veri ürünü günceller ve önceki sürümü saklar. HTTP 200 yanıtında da hatalar bulunabilir; "
                + "reddedilen, hatalar ve tamamlandi alanlarını kontrol edin. Başlık hatasında 400, boyut aşımında 413 döner.")
            .Produces<ImportReport>()
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

    private static async Task<IResult> ImportAsync(HttpRequest request, ImportService service, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxRequestBytes)
            return TooLarge();
        if (!request.HasFormContentType || !request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: 400, title: "CSV dosyasını multipart/form-data içindeki file alanıyla gönderin.");

        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");
            if (file is null || form.Files.Count != 1 || file.Length == 0)
                return Results.Problem(statusCode: 400, title: "file alanında boş olmayan tek bir CSV dosyası gönderilmelidir.");
            if (file.Length > MaxFileBytes) return TooLarge();

            await using var stream = file.OpenReadStream();
            var report = await service.ImportAsync(stream, cancellationToken);
            return Results.Json(report, statusCode: report.DosyaHatasi is null ? 200 : 400);
        }
        catch (InvalidDataException exception)
        {
            return exception.Message.Contains("length limit", StringComparison.OrdinalIgnoreCase)
                ? TooLarge() : Results.Problem(statusCode: 400, title: "Dosya yükleme biçimi geçersiz.");
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == 413 ? TooLarge()
                : Results.Problem(statusCode: 400, title: "Dosya isteği okunamadı.");
        }
    }

    private static IResult TooLarge() => Results.Problem(statusCode: 413, title: "CSV dosyası en fazla 5 MiB olabilir.");
}
