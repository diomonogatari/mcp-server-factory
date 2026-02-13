using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpServerFactory.Testing;

/// <summary>
/// Convenience wrapper for common integration-test interactions with <see cref="McpClient"/>.
/// </summary>
/// <param name="client">The connected MCP client.</param>
public sealed class McpTestClient(McpClient client) : IAsyncDisposable
{
    /// <summary>
    /// Gets the underlying MCP client for advanced usage.
    /// </summary>
    public McpClient Inner { get; } = client;

    /// <summary>
    /// Lists registered tool names.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All tool names currently reported by the server.</returns>
    public async Task<string[]> GetToolNamesAsync(CancellationToken cancellationToken = default)
    {
        var tools = await Inner
            .ListToolsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return tools.Select(tool => tool.Name).ToArray();
    }

    /// <summary>
    /// Invokes a tool and returns the first text content block.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="arguments">Optional tool arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first text content block returned by the tool.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tool result does not include a text content block.
    /// </exception>
    public async Task<string> CallToolForTextAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Inner
            .CallToolAsync(toolName, arguments: arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var textBlock = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        if (textBlock is null)
        {
            throw new InvalidOperationException($"Tool '{toolName}' did not return a text content block.");
        }

        return textBlock.Text;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return Inner.DisposeAsync();
    }
}