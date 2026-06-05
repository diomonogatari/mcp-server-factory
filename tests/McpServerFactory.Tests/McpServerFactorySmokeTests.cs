using McpServerFactory.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpServerFactory.Tests;

public class McpServerFactorySmokeTests
{
    [Fact]
    public async Task CreateClientAsync_WithRegisteredTool_CanListAndInvokeTool()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<EchoTools>());

        McpClient client = await factory.CreateClientAsync();

        IList<McpClientTool> tools = await client.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "echo");

        CallToolResult result = await client.CallToolAsync(
            "echo",
            arguments: new Dictionary<string, object?>
            {
                ["message"] = "hello",
            });

        string? text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

        Assert.Equal("hello", text);
    }

    [Fact]
    public async Task CreateClientAsync_WithDependencyOverride_UsesTestService()
    {
        FixedMessageProvider provider = new("from-test-service");

        await using McpServerIntegrationFactory factory = new(
            configureServices: services => services.AddSingleton<IMessageProvider>(provider),
            configureMcpServer: builder => builder.WithTools<GreetingTools>());

        McpTestClient testClient = await factory.CreateTestClientAsync();

        string text = await testClient.CallToolForTextAsync("greet");

        Assert.Equal("from-test-service", text);
        Assert.Same(provider, factory.Services.GetRequiredService<IMessageProvider>());
    }
}