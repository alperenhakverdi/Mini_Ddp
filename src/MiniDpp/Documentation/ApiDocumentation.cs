using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace MiniDpp.Documentation;

public static class ApiDocumentation
{
    public static void AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "Mini DPP API";
                document.Info.Version = "v1";
                document.Info.Description = "CSV yükleme, ürün JSON-LD ve PNG QR çıktıları. "
                    + "Önce POST /api/imports ile örnek CSV dosyasını yükleyin. "
                    + "HTML pasaportu /dpp/{gtin}, sürüm geçmişi /dpp/{gtin}/history adresindedir. "
                    + "Try it out gerçek veritabanında işlem yapar; aynı GTIN'in farklı verisi ürünü günceller.";
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parameter.Name != "gtin") continue;
                    parameter.Description = "Baştaki sıfırları koruyan 14 haneli GTIN. Önce bu ürünü CSV ile yükleyin.";
                    parameter.Example = JsonValue.Create("08690000000012");
                    parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^[0-9]{14}$" };
                }

                if (context.Description.RelativePath == "api/imports")
                {
                    // HttpRequest ile okunan formu açıkça tanımlayarak Swagger'da dosya seçimini sağla.
                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["multipart/form-data"] = new()
                            {
                                Schema = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Object,
                                    Required = new HashSet<string> { "file" },
                                    Properties = new Dictionary<string, IOpenApiSchema>
                                    {
                                        ["file"] = new OpenApiSchema
                                        {
                                            Type = JsonSchemaType.String, Format = "binary",
                                            Description = "UTF-8 CSV dosyası, en fazla 5 MiB."
                                        }
                                    }
                                }
                            }
                        }
                    };
                }
                else if (context.Description.RelativePath == "dpp/{gtin}.jsonld")
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "rol", In = ParameterLocation.Query, Required = false,
                        Description = "Boş bırakıldığında tüketici görünümü. 'yetkili' karbonu ekler; demo parametresidir, kimlik doğrulama sağlamaz.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                        Example = JsonValue.Create("tuketici")
                    });
                }
                else if (context.Description.RelativePath == "dpp/{gtin}/qr.png"
                    && operation.Responses?.TryGetValue("200", out var response) == true
                    && response.Content?.TryGetValue("image/png", out var png) == true)
                {
                    png.Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String, Format = "binary"
                    };
                }
                return Task.CompletedTask;
            });
        });
    }
}
