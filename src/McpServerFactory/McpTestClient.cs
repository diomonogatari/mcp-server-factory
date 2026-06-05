using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpServerFactory.Testing;

/// <summary>
/// Convenience wrapper for common integration-test interactions with <see cref="McpClient"/>.
/// </summary>
/// <remarks>
/// By default the wrapper does <b>not</b> own the underlying client: disposing the wrapper does not dispose the
/// client. This matches the common case where the client is owned by an <see cref="McpServerFactory"/> (which
/// disposes it). Prefer <see cref="McpServerFactory.CreateTestClientAsync(System.Threading.CancellationToken)"/>
/// to obtain a factory-owned wrapper. Pass <paramref name="ownsClient"/> as <see langword="true"/> only when
/// wrapping a client whose lifetime you own directly and want disposed alongside the wrapper.
/// </remarks>
/// <param name="client">The connected MCP client.</param>
/// <param name="ownsClient">Whether disposing the wrapper should also dispose the underlying client.</param>
public sealed class McpTestClient(McpClient client, bool ownsClient = false) : IAsyncDisposable
{
    private readonly bool ownsClient = ownsClient;

    /// <summary>
    /// Gets the underlying MCP client for advanced usage.
    /// </summary>
    public McpClient Inner { get; } = client;

    /// <summary>
    /// Gets the server identity negotiated during initialization.
    /// </summary>
    public Implementation? ServerInfo => Inner.ServerInfo;

    /// <summary>
    /// Gets the server capabilities negotiated during initialization.
    /// </summary>
    public ServerCapabilities? ServerCapabilities => Inner.ServerCapabilities;

    /// <summary>
    /// Gets the optional instructions the server returned during initialization.
    /// </summary>
    public string? ServerInstructions => Inner.ServerInstructions;

    /// <summary>
    /// Lists registered tool names, draining pagination so every page is included.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All tool names currently reported by the server.</returns>
    public async Task<string[]> GetToolNamesAsync(CancellationToken cancellationToken = default)
    {
        List<string> names = [];
        await foreach (McpClientTool tool in Inner.EnumerateToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            names.Add(tool.Name);
        }

        return [.. names];
    }

    /// <summary>
    /// Gets a single tool by name, or <see langword="null"/> when no tool with that name is registered.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="McpClientTool"/> (useful for asserting on its input schema), or <see langword="null"/>.</returns>
    public async Task<McpClientTool?> GetToolAsync(string toolName, CancellationToken cancellationToken = default)
    {
        await foreach (McpClientTool tool in Inner.EnumerateToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(tool.Name, toolName, StringComparison.Ordinal))
            {
                return tool;
            }
        }

        return null;
    }

    /// <summary>
    /// Invokes a tool and returns its result, throwing <see cref="McpToolCallException"/> if the tool reports an error.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="arguments">Optional tool arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The successful tool result.</returns>
    /// <exception cref="McpToolCallException">Thrown when the tool returns an error result.</exception>
    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        CallToolResult result = await Inner
            .CallToolAsync(toolName, arguments: arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsError == true)
        {
            throw new McpToolCallException(
                toolName,
                $"Tool '{toolName}' returned an error result: {DescribeError(result)}",
                result);
        }

        return result;
    }

    /// <summary>
    /// Invokes a tool and returns the first text content block.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="arguments">Optional tool arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first text content block returned by the tool.</returns>
    /// <exception cref="McpToolCallException">Thrown when the tool returns an error result.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the (successful) tool result does not include a text content block.
    /// </exception>
    public async Task<string> CallToolForTextAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        CallToolResult result = await CallToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);

        TextContentBlock? textBlock = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        if (textBlock is null)
        {
            throw new InvalidOperationException(
                $"Tool '{toolName}' did not return a text content block. Content kinds: {DescribeContentKinds(result)}.");
        }

        return textBlock.Text;
    }

    /// <summary>
    /// Invokes a tool and deserializes its structured (JSON object) result into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The structured-output type.</typeparam>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="arguments">Optional tool arguments.</param>
    /// <param name="serializerOptions">Optional serializer options; defaults to the MCP defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized structured content.</returns>
    /// <exception cref="McpToolCallException">Thrown when the tool returns an error result.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the tool did not return structured content.</exception>
    public async Task<T?> CallToolForJsonAsync<T>(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        CallToolResult result = await CallToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);
        JsonSerializerOptions options = serializerOptions ?? McpJsonUtilities.DefaultOptions;

        JsonNode? structured = result.StructuredContent;
        if (structured is not null)
        {
            return structured.Deserialize<T>(options);
        }

        // Many tools return JSON as a text content block without opting into structured content;
        // fall back to deserializing that so the helper works in both cases.
        string? text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (text is null)
        {
            throw new InvalidOperationException(
                $"Tool '{toolName}' returned neither structured content nor a JSON text content block.");
        }

        return JsonSerializer.Deserialize<T>(text, options);
    }

    /// <summary>
    /// Invokes a tool that is expected to fail and returns its error result.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="arguments">Optional tool arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The error result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the tool unexpectedly succeeded.</exception>
    public async Task<CallToolResult> CallToolExpectingErrorAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        CallToolResult result = await Inner
            .CallToolAsync(toolName, arguments: arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsError != true)
        {
            throw new InvalidOperationException($"Tool '{toolName}' was expected to return an error result but succeeded.");
        }

        return result;
    }

    /// <summary>
    /// Lists registered resource URIs, draining pagination so every page is included.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All resource URIs currently reported by the server.</returns>
    public async Task<string[]> GetResourceUrisAsync(CancellationToken cancellationToken = default)
    {
        List<string> uris = [];
        await foreach (McpClientResource resource in Inner.EnumerateResourcesAsync(cancellationToken).ConfigureAwait(false))
        {
            uris.Add(resource.Uri);
        }

        return [.. uris];
    }

    /// <summary>
    /// Reads a resource and returns the text of its first text content block.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first text resource content.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource has no text content.</exception>
    public async Task<string> ReadResourceTextAsync(string uri, CancellationToken cancellationToken = default)
    {
        ReadResourceResult result = await Inner.ReadResourceAsync(uri, cancellationToken).ConfigureAwait(false);

        TextResourceContents? textContents = result.Contents.OfType<TextResourceContents>().FirstOrDefault();
        if (textContents is null)
        {
            throw new InvalidOperationException($"Resource '{uri}' did not return text contents.");
        }

        return textContents.Text;
    }

    /// <summary>
    /// Lists registered prompt names, draining pagination so every page is included.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All prompt names currently reported by the server.</returns>
    public async Task<string[]> GetPromptNamesAsync(CancellationToken cancellationToken = default)
    {
        List<string> names = [];
        await foreach (McpClientPrompt prompt in Inner.EnumeratePromptsAsync(cancellationToken).ConfigureAwait(false))
        {
            names.Add(prompt.Name);
        }

        return [.. names];
    }

    /// <summary>
    /// Gets a prompt and returns its rendered result (messages and description).
    /// </summary>
    /// <param name="promptName">The MCP prompt name.</param>
    /// <param name="arguments">Optional prompt arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered prompt result.</returns>
    public async Task<GetPromptResult> GetPromptAsync(
        string promptName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        return await Inner
            .GetPromptAsync(promptName, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ownsClient ? Inner.DisposeAsync() : ValueTask.CompletedTask;
    }

    private static string DescribeError(CallToolResult result)
    {
        string? text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        return text ?? DescribeContentKinds(result);
    }

    private static string DescribeContentKinds(CallToolResult result)
    {
        string[] kinds = [.. result.Content.Select(block => block.Type)];
        return kinds.Length == 0 ? "(no content)" : string.Join(", ", kinds);
    }
}