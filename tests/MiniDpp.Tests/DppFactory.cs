using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MiniDpp.Tests;

public sealed class DppFactory : WebApplicationFactory<Program>
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "minidpp-tests-" + Guid.NewGuid().ToString("N"));
    public string DatabasePath => Path.Combine(DirectoryPath, "test.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Dpp:DatabasePath", DatabasePath);
        builder.UseSetting("Dpp:PublicBaseUrl", "https://dpp.example.test");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
    }
}
