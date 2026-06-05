using ModelContextProtocol.Protocol;

namespace McpServerFactory.Testing;

/// <summary>
/// Thrown by <see cref="McpAssert"/> when an MCP assertion fails.
/// </summary>
/// <param name="message">The failure message.</param>
public sealed class McpAssertionException(string message) : Exception(message);

/// <summary>
/// Framework-agnostic assertions for MCP integration tests. These add no dependency on any test framework, so they
/// work equally under xUnit, NUnit, and MSTest.
/// </summary>
public static class McpAssert
{
    /// <summary>
    /// Asserts that a tool with the given name is registered.
    /// </summary>
    /// <param name="client">The test client.</param>
    /// <param name="toolName">The expected tool name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ToolExistsAsync(McpTestClient client, string toolName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        string[] names = await client.GetToolNamesAsync(cancellationToken).ConfigureAwait(false);
        if (!names.Contains(toolName))
        {
            throw new McpAssertionException(
                $"Expected a tool named '{toolName}', but the server reported: {Join(names)}.");
        }
    }

    /// <summary>
    /// Asserts that no tool with the given name is registered.
    /// </summary>
    /// <param name="client">The test client.</param>
    /// <param name="toolName">The tool name expected to be absent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ToolDoesNotExistAsync(McpTestClient client, string toolName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        string[] names = await client.GetToolNamesAsync(cancellationToken).ConfigureAwait(false);
        if (names.Contains(toolName))
        {
            throw new McpAssertionException($"Expected no tool named '{toolName}', but it was registered.");
        }
    }

    /// <summary>
    /// Asserts that a tool result represents success (not an error).
    /// </summary>
    /// <param name="result">The tool result.</param>
    public static void Succeeded(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsError == true)
        {
            throw new McpAssertionException($"Expected a successful tool result, but it was an error: {DescribeText(result)}.");
        }
    }

    /// <summary>
    /// Asserts that a tool result represents an error.
    /// </summary>
    /// <param name="result">The tool result.</param>
    public static void IsError(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsError != true)
        {
            throw new McpAssertionException("Expected an error tool result, but it succeeded.");
        }
    }

    /// <summary>
    /// Asserts that a tool result's first text content block equals the expected text.
    /// </summary>
    /// <param name="result">The tool result.</param>
    /// <param name="expected">The expected text.</param>
    public static void TextEquals(CallToolResult result, string expected)
    {
        ArgumentNullException.ThrowIfNull(result);

        string? actual = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new McpAssertionException($"Expected tool text '{expected}', but got '{actual ?? "(no text block)"}'.");
        }
    }

    private static string DescribeText(CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "(no text block)";
    }

    private static string Join(IEnumerable<string> values)
    {
        string joined = string.Join(", ", values);
        return joined.Length == 0 ? "(none)" : joined;
    }
}