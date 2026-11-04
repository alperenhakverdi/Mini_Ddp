using MiniDpp.Configuration;
using MiniDpp.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddOptions<DppOptions>()
    .BindConfiguration("Dpp")
    .Validate(options => Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == "http" || uri.Scheme == "https")
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo), "Dpp:PublicBaseUrl geçerli bir HTTP/HTTPS temel adresi olmalıdır.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.DatabasePath), "Veritabanı yolu boş olamaz.")
    .ValidateOnStart();
builder.Services.AddDbContext<DppDbContext>((services, options) =>
{
    var settings = services.GetRequiredService<IOptions<DppOptions>>().Value;
    var path = Path.GetFullPath(settings.DatabasePath, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    options.UseSqlite(new SqliteConnectionStringBuilder
    {
        DataSource = path, ForeignKeys = true, Pooling = false
    }.ToString());
});
builder.Services.AddScoped<ProductStore>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DppDbContext>().Database.Migrate();
}
app.UseExceptionHandler();
app.UseStaticFiles();
app.MapRazorPages();
app.Run();

public partial class Program;
