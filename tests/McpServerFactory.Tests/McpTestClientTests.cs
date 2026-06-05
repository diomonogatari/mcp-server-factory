using McpServerFactory.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace McpServerFactory.Tests;

public class McpTestClientTests
{
    [Fact]
    public async Task GetToolNamesAsync_ReturnsRegisteredTools()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<EchoTools>());
        McpTestClient client = await factory.CreateTestClientAsync();

        string[] names = await client.GetToolNamesAsync();

        Assert.Contains("echo", names);
    }

    [Fact]
    public async Task CreateTestClientAsync_CalledTwice_ReturnsSameInstance()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<EchoTools>());

        McpTestClient first = await factory.CreateTestClientAsync();
        McpTestClient second = await factory.CreateTestClientAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task DisposingFactoryOwnedTestClientThenFactory_DoesNotThrow()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<EchoTools>());

        McpTestClient client = await factory.CreateTestClientAsync();

        // The factory owns the client; disposing the wrapper must be a no-op so the factory's
        // own disposal is the single owner (no double-dispose of the underlying client).
        await client.DisposeAsync();

        // The client still works because the factory has not been disposed yet.
        Assert.Equal("hello", await client.CallToolForTextAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "hello" }));
    }

    [Fact]
    public async Task CallToolForTextAsync_WhenToolErrors_ThrowsMcpToolCallException()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<ThrowingTools>());
        McpTestClient client = await factory.CreateTestClientAsync();

        McpToolCallException exception = await Assert.ThrowsAsync<McpToolCallException>(
            () => client.CallToolForTextAsync("boom"));

        Assert.Equal("boom", exception.ToolName);
        Assert.True(exception.Result.IsError);
    }

    [Fact]
    public async Task CallToolExpectingErrorAsync_WhenToolErrors_ReturnsErrorResult()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<ThrowingTools>());
        McpTestClient client = await factory.CreateTestClientAsync();

        CallToolResult result = await client.CallToolExpectingErrorAsync("boom");

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task CallToolForJsonAsync_ReturnsStructuredContent()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<StructuredTools>());
        McpTestClient client = await factory.CreateTestClientAsync();

        PointDto? point = await client.CallToolForJsonAsync<PointDto>(
            "point",
            new Dictionary<string, object?> { ["x"] = 3, ["y"] = 4 });

        Assert.Equal(new PointDto(3, 4), point);
    }

    [Fact]
    public async Task ServerInstructions_RoundTripFromOptions()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<EchoTools>(),
            options: new McpServerFactoryOptions { ServerInstructions = "be helpful" });
        McpTestClient client = await factory.CreateTestClientAsync();

        Assert.Equal("be helpful", client.ServerInstructions);
    }
}