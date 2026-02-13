using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using FactoryHost = McpServerFactory.Testing.McpServerIntegrationFactory;

namespace McpServerFactory.Tests;

public class McpServerFactoryLifecycleTests
{
    [Fact]
    public async Task CreateClientAsync_CalledTwice_ReturnsSameInstance()
    {
        await using var factory = new FactoryHost(
            configureMcpServer: builder => builder.WithTools<LifecycleTools>());

        var first = await factory.CreateClientAsync();
        var second = await factory.CreateClientAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task CreateClientAsync_CalledConcurrently_ReturnsSingleClientInstance()
    {
        await using var factory = new FactoryHost(
            configureMcpServer: builder => builder.WithTools<LifecycleTools>());

        var clients = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => factory.CreateClientAsync()));

        var first = clients[0];
        Assert.All(clients, client => Assert.Same(first, client));
    }

    [Fact]
    public async Task CreateClientAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        await using var factory = new FactoryHost(
            configureMcpServer: builder => builder.WithTools<LifecycleTools>());

        await factory.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.CreateClientAsync());
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        await using var factory = new FactoryHost(
            configureMcpServer: builder => builder.WithTools<LifecycleTools>());

        await factory.CreateClientAsync();

        await factory.DisposeAsync();
        await factory.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.CreateClientAsync());
    }

    [Fact]
    public async Task CreateClientAsync_AfterCanceledAttempt_CanRetrySuccessfully()
    {
        await using var factory = new FactoryHost(
            configureMcpServer: builder => builder.WithTools<LifecycleTools>());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.CreateClientAsync(cts.Token));

        var client = await factory.CreateClientAsync();
        var tools = await client.ListToolsAsync();

        Assert.Contains(tools, tool => tool.Name == "ping");
    }

    [Fact]
    public async Task CreateClientAsync_WhenStartupFails_CleansUpAndCanRetry()
    {
        var hostedService = new ThrowFirstStartHostedService();

        await using var factory = new FactoryHost(
            configureServices: services => services.AddSingleton<IHostedService>(hostedService),
            configureMcpServer: builder => builder.WithTools<LifecycleTools>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateClientAsync());

        var client = await factory.CreateClientAsync();
        var tools = await client.ListToolsAsync();

        Assert.Contains(tools, tool => tool.Name == "ping");
    }

    [McpServerToolType]
    private sealed class LifecycleTools
    {
        [McpServerTool(Name = "ping")]
        public string Ping()
        {
            return "pong";
        }
    }

    private sealed class ThrowFirstStartHostedService : IHostedService
    {
        private int starts;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref starts) == 1)
            {
                throw new InvalidOperationException("Intentional startup failure for lifecycle testing.");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}