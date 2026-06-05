using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Xunit;

namespace McpServerFactory.Testing.Xunit;

/// <summary>
/// An xUnit fixture that boots an in-memory MCP server once and shares the connected client across a test class.
/// </summary>
/// <remarks>
/// Derive a fixture, override the configuration hooks, and consume it via <c>IClassFixture&lt;TFixture&gt;</c>:
/// <code>
/// public sealed class CalculatorFixture : McpServerFixture
/// {
///     protected override void ConfigureMcpServer(IMcpServerBuilder builder) => builder.WithTools&lt;CalculatorTools&gt;();
/// }
///
/// public sealed class CalculatorTests(CalculatorFixture fixture) : IClassFixture&lt;CalculatorFixture&gt;
/// {
///     [Fact]
///     public async Task Adds() =>
///         Assert.Equal("3", await fixture.TestClient.CallToolForTextAsync("add", new Dictionary&lt;string, object?&gt; { ["a"] = 1, ["b"] = 2 }));
/// }
/// </code>
/// </remarks>
public abstract class McpServerFixture : IAsyncLifetime
{
    private Testing.McpServerFactory? factory;
    private McpTestClient? testClient;

    /// <summary>
    /// Gets the running factory. Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public Testing.McpServerFactory Factory =>
        factory ?? throw new InvalidOperationException("The fixture has not been initialized yet.");

    /// <summary>
    /// Gets the factory-owned test client. Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public McpTestClient TestClient =>
        testClient ?? throw new InvalidOperationException("The fixture has not been initialized yet.");

    /// <summary>
    /// Gets the underlying connected client. Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public McpClient Client => TestClient.Inner;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        factory = new Testing.McpServerFactory(
            configureServices: ConfigureServices,
            configureMcpServer: ConfigureMcpServer,
            options: CreateOptions());

        testClient = await factory.CreateTestClientAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Registers MCP tools, resources, and prompts for the server under test.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    protected virtual void ConfigureMcpServer(IMcpServerBuilder builder)
    {
    }

    /// <summary>
    /// Overrides dependency injection registrations (for example to substitute test doubles).
    /// </summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Creates the factory options (override to set timeouts, server info, or client handlers such as sampling).
    /// </summary>
    /// <returns>The options used to construct the factory.</returns>
    protected virtual McpServerFactoryOptions CreateOptions()
    {
        return new McpServerFactoryOptions();
    }
}