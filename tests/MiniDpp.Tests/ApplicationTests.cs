using Microsoft.AspNetCore.Mvc.Testing;

namespace MiniDpp.Tests;

public sealed class ApplicationTests
{
    [Fact]
    public async Task HomePage_IsAvailableInTurkish()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        Assert.Contains("lang=\"tr\"", await response.Content.ReadAsStringAsync());
    }
}
