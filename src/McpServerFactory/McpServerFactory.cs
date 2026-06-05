using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.IO.Pipelines;

namespace McpServerFactory.Testing;

/// <summary>
/// Creates an in-memory MCP server and a connected <see cref="McpClient"/> for integration testing.
/// </summary>
/// <remarks>
/// This type provides a workflow similar in spirit to <c>WebApplicationFactory&lt;T&gt;</c>,
/// but for MCP servers using stream-based transports.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="McpServerFactory"/> class.
/// </remarks>
/// <param name="configureServices">
/// Optional dependency injection overrides used for test doubles and service substitutions.
/// </param>
/// <param name="configureMcpServer">
/// Optional MCP tool registration callback.
/// </param>
/// <param name="options">
/// Optional factory behavior configuration.
/// </param>
public class McpServerFactory(
    Action<IServiceCollection>? configureServices = null,
    Action<IMcpServerBuilder>? configureMcpServer = null,
    McpServerFactoryOptions? options = null) : IAsyncDisposable
{
    private readonly Action<IServiceCollection>? configureServicesAction = configureServices;
    private readonly Action<IMcpServerBuilder>? configureMcpServerAction = configureMcpServer;
    private readonly McpServerFactoryOptions factoryOptions = options ?? new McpServerFactoryOptions();
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);

    private IHost? host;
    private McpClient? client;
    private McpTestClient? testClient;
    private Pipe? clientToServerPipe;
    private Pipe? serverToClientPipe;
    private bool disposed;
    private bool pipesCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerFactory"/> class for subclass-based customization.
    /// </summary>
    /// <param name="options">Optional factory behavior configuration.</param>
    protected McpServerFactory(McpServerFactoryOptions? options = null)
        : this(null, null, options)
    {
    }

    /// <summary>
    /// Gets the server service provider after initialization.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="CreateClientAsync(CancellationToken)"/> has not been called yet.</exception>
    public IServiceProvider Services =>
        host?.Services ?? throw new InvalidOperationException("Call CreateClientAsync before accessing Services.");

    /// <summary>
    /// Allows subclasses to register MCP tools.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    protected virtual void ConfigureMcpServer(IMcpServerBuilder builder)
    {
    }

    /// <summary>
    /// Allows subclasses to override dependency injection registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Allows subclasses to configure the host (configuration, environment, services, hosted services) before the
    /// MCP server is registered. The default implementation invokes <see cref="McpServerFactoryOptions.ConfigureHost"/>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    protected virtual void ConfigureHost(IHostApplicationBuilder builder)
    {
        factoryOptions.ConfigureHost?.Invoke(builder);
    }

    /// <summary>
    /// Allows subclasses to customize host logging.
    /// </summary>
    /// <param name="logging">The host logging builder.</param>
    protected virtual void ConfigureLogging(ILoggingBuilder logging)
    {
        if (factoryOptions.SuppressHostLogging)
        {
            logging.ClearProviders();
        }

        factoryOptions.ConfigureLogging?.Invoke(logging);
    }

    /// <summary>
    /// Creates default options for the underlying MCP client.
    /// </summary>
    /// <returns>A configured <see cref="McpClientOptions"/> instance.</returns>
    protected virtual McpClientOptions CreateClientOptions()
    {
        McpClientOptions clientOptions = new()
        {
            ClientInfo = new Implementation
            {
                Name = "McpServerFactoryClient",
                Version = "1.0.0",
            },
            InitializationTimeout = factoryOptions.InitializationTimeout,
        };

        factoryOptions.ConfigureClient?.Invoke(clientOptions);
        return clientOptions;
    }

    /// <summary>
    /// Builds and starts the in-memory MCP server, then returns a connected client.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for server and client initialization.</param>
    /// <returns>A connected <see cref="McpClient"/> instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the factory has already been disposed.</exception>
    public async Task<McpClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (client is not null)
            {
                return client;
            }

            var builder = Host.CreateApplicationBuilder();
            ConfigureLogging(builder.Logging);
            ConfigureHost(builder);

            var createdClientToServerPipe = new Pipe();
            var createdServerToClientPipe = new Pipe();

            var mcpServerBuilder = builder.Services.AddMcpServer(serverOptions =>
            {
                serverOptions.ServerInfo = factoryOptions.ServerInfo;
                serverOptions.ServerInstructions = factoryOptions.ServerInstructions;
                serverOptions.InitializationTimeout = factoryOptions.InitializationTimeout;
            });

            mcpServerBuilder.WithStreamServerTransport(
                createdClientToServerPipe.Reader.AsStream(),
                createdServerToClientPipe.Writer.AsStream());

            configureMcpServerAction?.Invoke(mcpServerBuilder);
            ConfigureMcpServer(mcpServerBuilder);

            configureServicesAction?.Invoke(builder.Services);
            ConfigureServices(builder.Services);

            IHost? builtHost = null;
            McpClient? createdClient = null;

            try
            {
                builtHost = builder.Build();
                await builtHost.StartAsync(cancellationToken).ConfigureAwait(false);

                var transport = new StreamClientTransport(
                    serverInput: createdClientToServerPipe.Writer.AsStream(),
                    serverOutput: createdServerToClientPipe.Reader.AsStream());

                createdClient = await McpClient
                    .CreateAsync(
                        transport,
                        clientOptions: CreateClientOptions(),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                if (createdClient is not null)
                {
                    await createdClient.DisposeAsync().ConfigureAwait(false);
                }

                if (builtHost is not null)
                {
                    await TryStopAndDisposeHostAsync(builtHost).ConfigureAwait(false);
                }

                // The freshly created pipes are still local at this point (the instance fields
                // are only assigned on success below), so complete them here or they would leak.
                await CompletePipesAsync(createdClientToServerPipe, createdServerToClientPipe).ConfigureAwait(false);

                throw;
            }

            host = builtHost;
            client = createdClient;
            clientToServerPipe = createdClientToServerPipe;
            serverToClientPipe = createdServerToClientPipe;
            pipesCompleted = false;
            return createdClient;
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Builds and starts the in-memory MCP server (if needed) and returns a factory-owned
    /// <see cref="McpTestClient"/> wrapper over the connected client.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for server and client initialization.</param>
    /// <returns>A factory-owned <see cref="McpTestClient"/>. The factory disposes the underlying client; you do not need to.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the factory has already been disposed.</exception>
    public async Task<McpTestClient> CreateTestClientAsync(CancellationToken cancellationToken = default)
    {
        McpClient created = await CreateClientAsync(cancellationToken).ConfigureAwait(false);
        return testClient ??= new McpTestClient(created, ownsClient: false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The lifecycle lock acquisition is bounded by <see cref="McpServerFactoryOptions.ShutdownTimeout"/>.
    /// If the lock cannot be acquired in time (for example, a concurrent <see cref="CreateClientAsync(CancellationToken)"/>
    /// is stuck), disposal proceeds on a best-effort basis without the lock rather than hanging teardown indefinitely.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        bool lockAcquired = await lifecycleLock.WaitAsync(factoryOptions.ShutdownTimeout).ConfigureAwait(false);

        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
                client = null;
                testClient = null;
            }

            if (host is not null)
            {
                await StopAndDisposeHostAsync(host, factoryOptions.ShutdownTimeout).ConfigureAwait(false);
                host = null;
            }

            if (!pipesCompleted)
            {
                await CompletePipesAsync(clientToServerPipe, serverToClientPipe).ConfigureAwait(false);
                clientToServerPipe = null;
                serverToClientPipe = null;
                pipesCompleted = true;
            }
        }
        finally
        {
            if (lockAcquired)
            {
                lifecycleLock.Release();
            }
        }
    }

    /// <summary>
    /// Completes both endpoints of the two in-memory pipes, releasing their buffers.
    /// </summary>
    /// <remarks>Safe to call with <see langword="null"/> pipes (no-op).</remarks>
    private static async Task CompletePipesAsync(Pipe? clientToServer, Pipe? serverToClient)
    {
        if (clientToServer is null || serverToClient is null)
        {
            return;
        }

        await clientToServer.Writer.CompleteAsync().ConfigureAwait(false);
        await serverToClient.Writer.CompleteAsync().ConfigureAwait(false);
        await clientToServer.Reader.CompleteAsync().ConfigureAwait(false);
        await serverToClient.Reader.CompleteAsync().ConfigureAwait(false);
    }

    private static async Task TryStopAndDisposeHostAsync(IHost hostToDispose)
    {
        try
        {
            await hostToDispose.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Preserve original startup failure.
        }
        finally
        {
            hostToDispose.Dispose();
        }
    }

    private static async Task StopAndDisposeHostAsync(IHost hostToDispose, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            await hostToDispose.StopAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            hostToDispose.Dispose();
        }
    }
}

/// <summary>
/// Preferred alias for <see cref="McpServerFactory"/> to improve readability in consuming test projects.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="McpServerIntegrationFactory"/> class.
/// </remarks>
/// <param name="configureServices">Optional dependency injection overrides.</param>
/// <param name="configureMcpServer">Optional MCP tool registration callback.</param>
/// <param name="options">Optional factory behavior configuration.</param>
public class McpServerIntegrationFactory(
    Action<IServiceCollection>? configureServices = null,
    Action<IMcpServerBuilder>? configureMcpServer = null,
    McpServerFactoryOptions? options = null) : McpServerFactory(configureServices, configureMcpServer, options)
{
}