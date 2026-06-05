using McpServerFactory.Testing.Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace McpServerFactory.Tests;

public sealed class EchoFixture : McpServerFixture
{
    protected override void ConfigureMcpServer(IMcpServerBuilder builder)
    {
        builder.WithTools<EchoTools>();
    }
}

public sealed class McpServerFixtureTests(EchoFixture fixture) : IClassFixture<EchoFixture>
{
    [Fact]
    public async Task SharedFixture_BootsServerOnce_AndServesTools()
    {
        string text = await fixture.TestClient.CallToolForTextAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "via-fixture" });

        Assert.Equal("via-fixture", text);
    }

    [Fact]
    public async Task SharedFixture_ExposesServerInfo()
    {
        Assert.Contains("echo", await fixture.TestClient.GetToolNamesAsync());
    }
}