using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpServerFactory.Testing;

/// <summary>
/// Records server-sent notifications (logging, progress, list-changed, resource-updated) on a connected
/// <see cref="McpClient"/> so tests can assert on them.
/// </summary>
/// <remarks>
/// Note: <see cref="McpServerFactoryOptions.SuppressHostLogging"/> silences the host's <c>ILogger</c> output, not
/// the protocol-level <c>notifications/message</c> logging notifications captured here.
/// </remarks>
public sealed class NotificationRecorder : IAsyncDisposable
{
    private static readonly string[] DefaultMethods =
    [
        NotificationMethods.LoggingMessageNotification,
        NotificationMethods.ProgressNotification,
        NotificationMethods.ResourceUpdatedNotification,
        NotificationMethods.ResourceListChangedNotification,
        NotificationMethods.ToolListChangedNotification,
        NotificationMethods.PromptListChangedNotification,
    ];

    private readonly List<JsonRpcNotification> notifications = [];
    private readonly List<IAsyncDisposable> registrations = [];
    private readonly List<(Func<JsonRpcNotification, bool> Predicate, TaskCompletionSource<JsonRpcNotification> Completion)> waiters = [];
    private readonly object gate = new();

    private NotificationRecorder()
    {
    }

    /// <summary>
    /// Gets a snapshot of all notifications recorded so far, in arrival order.
    /// </summary>
    public IReadOnlyList<JsonRpcNotification> Notifications
    {
        get
        {
            lock (gate)
            {
                return [.. notifications];
            }
        }
    }

    /// <summary>
    /// Attaches a recorder to the client for the given notification methods (or a sensible default set).
    /// </summary>
    /// <param name="client">The connected client to observe.</param>
    /// <param name="methods">Notification method names to record; defaults to the common server-sent set.</param>
    /// <returns>A recorder; dispose it to unsubscribe.</returns>
    public static NotificationRecorder Attach(McpClient client, params string[] methods)
    {
        ArgumentNullException.ThrowIfNull(client);

        NotificationRecorder recorder = new();
        string[] selected = methods is { Length: > 0 } ? methods : DefaultMethods;
        foreach (string method in selected)
        {
            recorder.registrations.Add(client.RegisterNotificationHandler(method, recorder.OnNotificationAsync));
        }

        return recorder;
    }

    /// <summary>
    /// Waits for a notification matching <paramref name="predicate"/>, returning immediately if one already arrived.
    /// </summary>
    /// <param name="predicate">The match predicate.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching notification.</returns>
    /// <exception cref="TimeoutException">Thrown when no matching notification arrives in time.</exception>
    public async Task<JsonRpcNotification> WaitForAsync(
        Func<JsonRpcNotification, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        TaskCompletionSource<JsonRpcNotification> completion;
        lock (gate)
        {
            JsonRpcNotification? existing = notifications.FirstOrDefault(predicate);
            if (existing is not null)
            {
                return existing;
            }

            completion = new TaskCompletionSource<JsonRpcNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add((predicate, completion));
        }

        Task delay = Task.Delay(timeout, cancellationToken);
        Task winner = await Task.WhenAny(completion.Task, delay).ConfigureAwait(false);
        if (winner == completion.Task)
        {
            return await completion.Task.ConfigureAwait(false);
        }

        lock (gate)
        {
            waiters.RemoveAll(waiter => ReferenceEquals(waiter.Completion, completion));
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException($"No matching notification arrived within {timeout.TotalSeconds:0.##}s.");
    }

    /// <summary>
    /// Waits for a notification with the given method name.
    /// </summary>
    /// <param name="method">The notification method name (see <see cref="NotificationMethods"/>).</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching notification.</returns>
    public Task<JsonRpcNotification> WaitForMethodAsync(
        string method,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return WaitForAsync(
            notification => string.Equals(notification.Method, method, StringComparison.Ordinal),
            timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IAsyncDisposable[] toDispose;
        lock (gate)
        {
            toDispose = [.. registrations];
            registrations.Clear();
        }

        foreach (IAsyncDisposable registration in toDispose)
        {
            await registration.DisposeAsync().ConfigureAwait(false);
        }
    }

    private ValueTask OnNotificationAsync(JsonRpcNotification notification, CancellationToken cancellationToken)
    {
        List<TaskCompletionSource<JsonRpcNotification>> toComplete = [];
        lock (gate)
        {
            notifications.Add(notification);
            for (int index = waiters.Count - 1; index >= 0; index--)
            {
                if (waiters[index].Predicate(notification))
                {
                    toComplete.Add(waiters[index].Completion);
                    waiters.RemoveAt(index);
                }
            }
        }

        foreach (TaskCompletionSource<JsonRpcNotification> completion in toComplete)
        {
            completion.TrySetResult(notification);
        }

        return default;
    }
}