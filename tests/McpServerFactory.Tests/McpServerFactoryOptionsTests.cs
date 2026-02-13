using McpServerFactory.Testing;

namespace McpServerFactory.Tests;

public class McpServerFactoryOptionsTests
{
    [Fact]
    public void Defaults_AreConfiguredForTestServer()
    {
        var options = new McpServerFactoryOptions();

        Assert.Equal("TestMcpServer", options.ServerInfo.Name);
        Assert.Equal("1.0.0", options.ServerInfo.Version);
        Assert.Equal(TimeSpan.FromSeconds(10), options.InitializationTimeout);
        Assert.Null(options.ServerInstructions);
    }

    [Fact]
    public void InitOnlyProperties_CanBeOverridden()
    {
        var options = new McpServerFactoryOptions
        {
            ServerInstructions = "Use terse responses.",
            InitializationTimeout = TimeSpan.FromSeconds(30),
        };

        Assert.Equal("Use terse responses.", options.ServerInstructions);
        Assert.Equal(TimeSpan.FromSeconds(30), options.InitializationTimeout);
    }
}