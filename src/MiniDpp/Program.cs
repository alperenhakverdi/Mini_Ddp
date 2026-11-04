using MiniDpp.Configuration;

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

var app = builder.Build();
app.UseExceptionHandler();
app.UseStaticFiles();
app.MapRazorPages();
app.Run();

public partial class Program;
