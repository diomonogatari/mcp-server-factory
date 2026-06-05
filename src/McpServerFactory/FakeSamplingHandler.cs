using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpServerFactory.Testing;

/// <summary>
/// A deterministic, recording sampling handler for tests. Lets a test client satisfy server-initiated
/// sampling (LLM) requests without a real model, and capture what the server asked for.
/// </summary>
/// <remarks>
/// Wire it up through <see cref="McpServerFactoryOptions.ConfigureClient"/>, for example:
/// <code>
/// FakeSamplingHandler sampling = FakeSamplingHandler.Returning("ok");
/// McpServerFactoryOptions options = new()
/// {
///     ConfigureClient = client => client.UseSamplingHandler(sampling),
/// };
/// </code>
/// </remarks>
public sealed class FakeSamplingHandler
{
    private readonly Func<CreateMessageRequestParams?, CreateMessageResult> responder;
    private readonly List<CreateMessageRequestParams> receivedRequests = [];
    private readonly object gate = new();

    private FakeSamplingHandler(Func<CreateMessageRequestParams?, CreateMessageResult> responder)
    {
        this.responder = responder;
    }

    /// <summary>
    /// Gets the sampling requests the server sent to this handler, in arrival order.
    /// </summary>
    public IReadOnlyList<CreateMessageRequestParams> ReceivedRequests
    {
        get
        {
            lock (gate)
            {
                return [.. receivedRequests];
            }
        }
    }

    /// <summary>
    /// Creates a handler that records every request and always returns a fixed assistant text reply.
    /// </summary>
    /// <param name="text">The assistant text to return.</param>
    /// <returns>A configured <see cref="FakeSamplingHandler"/>.</returns>
    public static FakeSamplingHandler Returning(string text)
    {
        return new FakeSamplingHandler(_ => CreateTextResult(text));
    }

    /// <summary>
    /// Creates a handler that records every request and computes a reply from it.
    /// </summary>
    /// <param name="responder">A function producing the sampling result for a given request.</param>
    /// <returns>A configured <see cref="FakeSamplingHandler"/>.</returns>
    public static FakeSamplingHandler From(Func<CreateMessageRequestParams?, CreateMessageResult> responder)
    {
        ArgumentNullException.ThrowIfNull(responder);
        return new FakeSamplingHandler(responder);
    }

    /// <summary>
    /// Builds an assistant text sampling result, for use when implementing a custom <see cref="From"/> responder.
    /// </summary>
    /// <param name="text">The assistant text.</param>
    /// <returns>A <see cref="CreateMessageResult"/> carrying the text.</returns>
    public static CreateMessageResult CreateTextResult(string text)
    {
        return new CreateMessageResult
        {
            Content = [new TextContentBlock { Text = text }],
            Model = "fake-model",
            Role = Role.Assistant,
            StopReason = "endTurn",
        };
    }

    /// <summary>
    /// The delegate to assign to <c>McpClientHandlers.SamplingHandler</c>. Records the request and returns the reply.
    /// </summary>
    /// <param name="request">The sampling request from the server.</param>
    /// <param name="progress">Progress reporter (unused).</param>
    /// <param name="cancellationToken">Cancellation token (unused).</param>
    /// <returns>The sampling result.</returns>
    public ValueTask<CreateMessageResult> HandleAsync(
        CreateMessageRequestParams? request,
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (request is not null)
            {
                receivedRequests.Add(request);
            }
        }

        return new ValueTask<CreateMessageResult>(responder(request));
    }
}

/// <summary>
/// Extension helpers for wiring test client handlers onto <see cref="McpClientOptions"/>.
/// </summary>
public static class McpClientOptionsTestingExtensions
{
    /// <summary>
    /// Advertises the sampling capability and wires the supplied <see cref="FakeSamplingHandler"/> as the handler.
    /// </summary>
    /// <param name="options">The client options to configure.</param>
    /// <param name="handler">The fake sampling handler.</param>
    /// <returns>The same <paramref name="options"/> for chaining.</returns>
    public static McpClientOptions UseSamplingHandler(this McpClientOptions options, FakeSamplingHandler handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);

        options.Capabilities ??= new ClientCapabilities();
        options.Capabilities.Sampling ??= new SamplingCapability();
        options.Handlers ??= new McpClientHandlers();
        options.Handlers.SamplingHandler = handler.HandleAsync;
        return options;
    }
}