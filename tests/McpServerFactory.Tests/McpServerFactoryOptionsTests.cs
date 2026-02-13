using McpServerFactory.Testing;
using Microsoft.Extensions.Logging;

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
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
        Assert.True(options.SuppressHostLogging);
        Assert.Null(options.ConfigureLogging);
        Assert.Null(options.ServerInstructions);
    }

    [Fact]
    public void InitOnlyProperties_CanBeOverridden()
    {
        Action<ILoggingBuilder> configureLogging = _ => { };

        var options = new McpServerFactoryOptions
        {
            ServerInstructions = "Use terse responses.",
            InitializationTimeout = TimeSpan.FromSeconds(30),
            ShutdownTimeout = TimeSpan.FromSeconds(15),
            SuppressHostLogging = false,
            ConfigureLogging = configureLogging,
        };

        Assert.Equal("Use terse responses.", options.ServerInstructions);
        Assert.Equal(TimeSpan.FromSeconds(30), options.InitializationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(15), options.ShutdownTimeout);
        Assert.False(options.SuppressHostLogging);
        Assert.Same(configureLogging, options.ConfigureLogging);
    }
}