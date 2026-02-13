using McpServerFactory.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace McpServerFactoryTemplate;

public class McpServerFactorySmokeTests
{
    [Fact]
    public async Task EchoTool_ReturnsExpectedValue()
    {
        await using var factory = new McpServerIntegrationFactory(
            configureMcpServer: builder => builder.WithTools<EchoTools>());

        await using var client = new McpTestClient(await factory.CreateClientAsync());

        var response = await client.CallToolForTextAsync(
            "echo",
            new Dictionary<string, object?>
            {
                ["message"] = "hello",
            });

        Assert.Equal("hello", response);
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
}
