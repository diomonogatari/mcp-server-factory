using Microsoft.Extensions.Logging;
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
}