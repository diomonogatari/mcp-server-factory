using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using FactoryClient = McpServerFactory.Testing.McpTestClient;
using FactoryHost = McpServerFactory.Testing.McpServerFactory;

namespace McpServerFactory.Tests;

public class McpServerFactorySmokeTests
{
    [Fact]
    public async Task CreateClientAsync_WithRegisteredTool_CanListAndInvokeTool()
    {
        await using var factory = new FactoryHost(
            configureMcpServer: builder => builder.WithTools<EchoTools>());

        await using var client = await factory.CreateClientAsync();

        var tools = await client.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "echo");

        var result = await client.CallToolAsync(
            "echo",
            arguments: new Dictionary<string, object?>
            {
                ["message"] = "hello",
            });

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

        Assert.Equal("hello", text);
    }

    [Fact]
    public async Task CreateClientAsync_WithDependencyOverride_UsesTestService()
    {
        var provider = new FixedMessageProvider("from-test-service");

        await using var factory = new FactoryHost(
            configureServices: services => services.AddSingleton<IMessageProvider>(provider),
            configureMcpServer: builder => builder.WithTools<GreetingTools>());

        await using var testClient = new FactoryClient(await factory.CreateClientAsync());

        var text = await testClient.CallToolForTextAsync("greet");

        Assert.Equal("from-test-service", text);
        Assert.Same(provider, factory.Services.GetRequiredService<IMessageProvider>());
    }

    [McpServerToolType]
    private sealed class EchoTools
    {
        [McpServerTool(Name = "echo")]
        public string Echo(string message)
        {
            return message;
        }
    }

    [McpServerToolType]
    private sealed class GreetingTools(IMessageProvider messageProvider)
    {
        [McpServerTool(Name = "greet")]
        public string Greet()
        {
            return messageProvider.GetMessage();
        }
    }

    private interface IMessageProvider
    {
        string GetMessage();
    }

    private sealed class FixedMessageProvider(string message) : IMessageProvider
    {
        public string GetMessage()
        {
            return message;
        }
    }
}