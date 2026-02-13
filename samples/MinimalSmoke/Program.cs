using McpServerFactory.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MinimalSmoke;

internal static class Program
{
    private static async Task Main()
    {
        await using var factory = new McpServerIntegrationFactory(
            configureMcpServer: builder => builder.WithToolsFromAssembly(typeof(EchoTools).Assembly));

        await using var client = await factory.CreateClientAsync();

        var result = await client.CallToolAsync(
            "echo",
            arguments: new Dictionary<string, object?>
            {
                ["message"] = "hello from MinimalSmoke",
            });

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        Console.WriteLine($"Tool response: {text}");
    }
}

[McpServerToolType]
internal sealed class EchoTools
{
    [McpServerTool(Name = "echo")]
    public string Echo(string message)
    {
        return message;
    }
}