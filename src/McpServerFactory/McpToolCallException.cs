using ModelContextProtocol.Protocol;

namespace McpServerFactory.Testing;

/// <summary>
/// Thrown when an MCP tool call returns an error result (<see cref="CallToolResult.IsError"/> is <see langword="true"/>)
/// from a helper that expects success.
/// </summary>
/// <remarks>
/// MCP signals tool failures in-band: the call completes successfully at the protocol level but sets
/// <see cref="CallToolResult.IsError"/> and returns the error text as content. Helpers that assert success surface
/// that as this exception so a failed tool call cannot quietly pass a test.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="McpToolCallException"/> class.
/// </remarks>
/// <param name="toolName">The name of the tool that returned an error result.</param>
/// <param name="message">The exception message.</param>
/// <param name="result">The full tool result, for inspection of content and structured output.</param>
public sealed class McpToolCallException(string toolName, string message, CallToolResult result) : Exception(message)
{

    /// <summary>
    /// Gets the name of the tool that returned an error result.
    /// </summary>
    public string ToolName { get; } = toolName;

    /// <summary>
    /// Gets the full tool result, including any error content and structured output.
    /// </summary>
    public CallToolResult Result { get; } = result;
}