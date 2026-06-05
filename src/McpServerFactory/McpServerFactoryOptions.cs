using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpServerFactory.Testing;

/// <summary>
/// Configures behavior for <see cref="McpServerFactory"/> and <see cref="McpServerIntegrationFactory"/>.
/// </summary>
public sealed record McpServerFactoryOptions
{
    /// <summary>
    /// Gets the server identity surfaced during MCP initialization.
    /// </summary>
    public Implementation ServerInfo { get; init; } = new()
    {
        Name = "TestMcpServer",
        Version = "1.0.0",
    };

    /// <summary>
    /// Gets the timeout used while initializing the MCP client and server handshake.
    /// </summary>
    public TimeSpan InitializationTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets optional server instructions returned during MCP initialization.
    /// </summary>
    public string? ServerInstructions { get; init; }

    /// <summary>
    /// Gets a value indicating whether default host logging providers should be removed.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> to reduce log noise in test output.
    /// </remarks>
    public bool SuppressHostLogging { get; init; } = true;

    /// <summary>
    /// Gets the timeout used when stopping the in-memory host during disposal.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets an optional callback to customize host logging for advanced scenarios.
    /// </summary>
    public Action<ILoggingBuilder>? ConfigureLogging { get; init; }

    /// <summary>
    /// Gets an optional callback to customize the test <see cref="McpClientOptions"/> after the defaults are applied.
    /// </summary>
    /// <remarks>
    /// Use this to declare client <c>Capabilities</c> and wire <c>Handlers</c> (for example a sampling, elicitation,
    /// or roots handler) so the test client can answer server-initiated requests. See
    /// <see cref="FakeSamplingHandler"/> for a ready-made sampling responder.
    /// </remarks>
    public Action<McpClientOptions>? ConfigureClient { get; init; }

    /// <summary>
    /// Gets an optional callback to configure the underlying host (configuration, environment, services, and hosted
    /// services) before the MCP server is registered.
    /// </summary>
    /// <remarks>
    /// This is the "real composition root" hook: run the same registration your server's <c>Program.cs</c> uses so the
    /// test exercises your real configuration binding, options, and hosted services. The factory always owns the MCP
    /// server registration and the in-memory transport, so your callback should register everything <b>except</b> the
    /// MCP transport; register tools/resources/prompts via <c>configureMcpServer</c> instead. See
    /// <c>docs/ARCHITECTURE.md</c> for the recommended convention.
    /// </remarks>
    public Action<IHostApplicationBuilder>? ConfigureHost { get; init; }
}