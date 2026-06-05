# McpServerFactory.Xunit

xUnit fixtures for [`McpServerFactory`](https://www.nuget.org/packages/McpServerFactory) — boot an
in-memory MCP server **once per test class** and share the connected client through
`IClassFixture<T>`, the same pattern you would use with `WebApplicationFactory<T>` in ASP.NET Core
integration tests.

```bash
dotnet add package McpServerFactory.Xunit
```

## Usage

```csharp
using McpServerFactory.Testing;
using McpServerFactory.Testing.Xunit;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

public sealed class CalculatorFixture : McpServerFixture
{
    protected override void ConfigureMcpServer(IMcpServerBuilder builder) =>
        builder.WithTools<CalculatorTools>();
}

public sealed class CalculatorTests(CalculatorFixture fixture) : IClassFixture<CalculatorFixture>
{
    [Fact]
    public async Task Add_ReturnsSum()
    {
        string result = await fixture.TestClient.CallToolForTextAsync(
            "add",
            new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 });

        Assert.Equal("3", result);
    }
}
```

The server boots once in `InitializeAsync` and is disposed in `DisposeAsync`. Override
`ConfigureServices`, `ConfigureMcpServer`, or `CreateOptions` to substitute dependencies, register
tools/resources/prompts, or wire client handlers (for example a `FakeSamplingHandler`).
