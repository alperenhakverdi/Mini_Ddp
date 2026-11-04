namespace MiniDpp.Configuration;

public sealed class DppOptions
{
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";
    public string DatabasePath { get; set; } = "App_Data/minidpp.db";
}
