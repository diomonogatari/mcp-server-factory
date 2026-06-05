# McpServerFactory

[![NuGet](https://img.shields.io/nuget/v/McpServerFactory.svg)](https://www.nuget.org/packages/McpServerFactory)
[![NuGet Downloads](https://img.shields.io/nuget/dt/McpServerFactory.svg)](https://www.nuget.org/packages/McpServerFactory)
[![CI](https://github.com/diomonogatari/mcp-server-factory/actions/workflows/ci.yml/badge.svg)](https://github.com/diomonogatari/mcp-server-factory/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/diomonogatari/mcp-server-factory/branch/main/graph/badge.svg)](https://codecov.io/gh/diomonogatari/mcp-server-factory)
[![license](https://img.shields.io/github/license/diomonogatari/mcp-server-factory.svg?maxAge=2592000)](https://github.com/diomonogatari/mcp-server-factory/blob/main/LICENSE)
![.NET](https://img.shields.io/badge/.net-8.0%20%7C%209.0%20%7C%2010.0-yellowgreen.svg)

In-memory integration **test harness** for .NET Model Context Protocol (MCP) servers.

`McpServerFactory` boots an MCP server in-process, connects a real `McpClient` through
in-memory streams, and lets you run realistic integration tests without network ports,
Docker, or external services — a testing experience similar in spirit to
`WebApplicationFactory<T>` for ASP.NET Core.

> **Scope:** by default the factory hosts the **tool/resource/prompt classes you register**
> over an in-memory transport. It does not auto-run your server's `Program.cs`. To exercise
> your real composition root (configuration, options, hosted services), use the
> [`ConfigureHost`](#testing-your-real-composition-root) hook.

## Installation

```bash
dotnet add package McpServerFactory
```

## Template-based scaffolding

Install the template pack to bootstrap an MCP integration test project:

```bash
dotnet new install McpServerFactory.Templates
dotnet new mcp-itest -n MyServer.Tests
```

The `mcp-itest` template accepts `--McpServerFactoryVersion` to override the
`McpServerFactory` package version; the default tracks the version of the
template pack you installed.

## Quick start

```csharp
using McpServerFactory.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Xunit;

[McpServerToolType]
public sealed class EchoTools
{
    [McpServerTool(Name = "echo")]
    public string Echo(string message) => message;
}

public class EchoTests
{
    [Fact]
    public async Task Echo_ReturnsInput()
    {
        await using McpServerIntegrationFactory factory = new(
            configureMcpServer: builder => builder.WithTools<EchoTools>());

        // The factory owns the client; you do not dispose it yourself.
        McpTestClient client = await factory.CreateTestClientAsync();

        string text = await client.CallToolForTextAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "hello" });

        Assert.Equal("hello", text);
    }
}
```

Using xUnit? The companion [`McpServerFactory.Xunit`](#boot-once-per-class-with-xunit) package
boots the server once per test class via `IClassFixture<T>`.

## Why use this library

- **Fast and in-process** — no subprocess, ports, Docker, or stdio plumbing.
- **Real protocol flow** — a real `McpClient` over a real (in-memory) transport: `tools/list`,
  `tools/call`, resources, prompts, and server-initiated sampling all run end-to-end.
- **Easy dependency overrides** — substitute services via `configureServices`.
- **Debuggable** — set breakpoints on both sides; it's all one process.
- **Framework-agnostic core** — works with xUnit, NUnit, and MSTest.

## How it compares

| Approach | Speed | No process/port | Breakpoints both sides | DI substitution | Exercises real `Program.cs` |
| --- | --- | --- | --- | --- | --- |
| **McpServerFactory** | fast | ✅ | ✅ | ✅ | via `ConfigureHost` |
| stdio subprocess + `McpClient` | slow | ❌ | server only | ❌ | ✅ |
| raw pipes + `StreamClientTransport` | fast | ✅ | ✅ | ✅ (manual) | ❌ |
| MCP Inspector (manual) | n/a | ❌ | ❌ | ❌ | ✅ |

**Use it when** you want fast, automated, in-process tests of your tools/resources/prompts and
handlers with dependency substitution. **Reach for a stdio subprocess instead** when you must
validate the actual published binary, its real transport configuration, or process-level startup.

## Testing tools, resources, prompts, and structured output

`McpTestClient` wraps a real `McpClient` with test-shaped helpers (all pagination-safe):

```csharp
McpTestClient client = await factory.CreateTestClientAsync();

string[] tools   = await client.GetToolNamesAsync();
string greeting  = await client.CallToolForTextAsync("greet");
MyDto result     = await client.CallToolForJsonAsync<MyDto>("compute", args); // structured or JSON-text
string contents  = await client.ReadResourceTextAsync("resource://config");
string[] prompts = await client.GetPromptNamesAsync();
```

Tool failures (`IsError`) no longer pass silently: `CallToolForTextAsync` throws
`McpToolCallException`, and `CallToolExpectingErrorAsync` asserts the negative path. The
framework-agnostic `McpAssert` helpers (`ToolExistsAsync`, `Succeeded`, `IsError`, `TextEquals`)
work under any test framework.

## Testing server-initiated sampling

If your server calls the model (server-initiated sampling), wire a deterministic fake so tests
never need a real LLM:

```csharp
FakeSamplingHandler sampling = FakeSamplingHandler.Returning("42");

await using McpServerIntegrationFactory factory = new(
    configureMcpServer: builder => builder.WithTools<AskTools>(),
    options: new McpServerFactoryOptions
    {
        ConfigureClient = client => client.UseSamplingHandler(sampling),
    });

McpTestClient client = await factory.CreateTestClientAsync();
string answer = await client.CallToolForTextAsync("ask", new() { ["question"] = "..." });

Assert.Single(sampling.ReceivedRequests); // assert what the server asked the model
```

`ConfigureClient` exposes the full `McpClientOptions`, so you can also declare elicitation, roots,
or notification handlers. Capture server-sent notifications (logging, progress, list-changed) with
`NotificationRecorder.Attach(client.Inner)` and `await recorder.WaitForMethodAsync(...)`.

## Testing your real composition root

By default the factory hosts the classes you register. To exercise the same registration your
server's `Program.cs` uses — real configuration binding, options, and hosted services — supply
`ConfigureHost`. The factory always owns the MCP server registration and the in-memory transport,
so configure everything **except** the transport; register tools/resources/prompts via
`configureMcpServer`:

```csharp
await using McpServerIntegrationFactory factory = new(
    configureMcpServer: builder => builder.WithTools<MyTools>(),
    options: new McpServerFactoryOptions
    {
        ConfigureHost = builder =>
        {
            builder.Configuration.AddInMemoryCollection(/* test config */);
            builder.Services.AddMyDomainServices();   // your real registration, minus the transport
        },
    });
```

## Boot once per class with xUnit

Install the companion package and derive a fixture:

```bash
dotnet add package McpServerFactory.Xunit
```

```csharp
using McpServerFactory.Testing.Xunit;

public sealed class EchoFixture : McpServerFixture
{
    protected override void ConfigureMcpServer(IMcpServerBuilder builder) => builder.WithTools<EchoTools>();
}

public sealed class EchoTests(EchoFixture fixture) : IClassFixture<EchoFixture>
{
    [Fact]
    public async Task Echoes() =>
        Assert.Equal("hi", await fixture.TestClient.CallToolForTextAsync("echo", new() { ["message"] = "hi" }));
}
```

## Behavioral guarantees

- `CreateClientAsync` / `CreateTestClientAsync` are thread-safe and idempotent per factory instance.
- Concurrent calls return the same connected client; **the factory owns and disposes it** — you do
  not need to dispose the returned client yourself.
- Startup failures do not leak the temporary host instance or its pipes.
- `DisposeAsync` is safe to call multiple times and is bounded by `ShutdownTimeout` (it will not hang
  teardown).
- `CreateClientAsync` throws `ObjectDisposedException` after disposal.
- Need independent server instances (isolation)? Create multiple factory instances — each owns its
  own host. A single factory exposes one in-memory session.

## Compatibility and support

- Target frameworks: `net8.0`, `net9.0`, `net10.0`.
- MCP SDK dependency: `ModelContextProtocol` `0.4.0-preview.3`.
- Compatibility promise: each package release is validated against the pinned MCP SDK version on all
  target frameworks.
- Upgrade policy: MCP SDK bumps are explicit and called out in [CHANGELOG.md](CHANGELOG.md). While the
  MCP SDK is in preview, `McpServerFactory` stays `0.x` and bumps in lockstep.

| McpServerFactory | MCP SDK | Target frameworks |
| --- | --- | --- |
| 0.2.x | 0.4.0-preview.3 | net8.0, net9.0, net10.0 |
| 0.1.x | 0.4.0-preview.3 | net10.0 |

## API overview

- `McpServerFactory` / `McpServerIntegrationFactory`
  - Start an in-process server host; create a connected client via `CreateClientAsync()` or a
    factory-owned wrapper via `CreateTestClientAsync()`.
  - Expose `Services` for DI validation after startup.
- `McpServerFactoryOptions`
  - Configure server identity, timeouts, instructions, logging, the client (`ConfigureClient`), and
    the host composition root (`ConfigureHost`).
- `McpTestClient`
  - Wrapper with tool, resource, prompt, structured-output, and server-metadata helpers.
- `FakeSamplingHandler` / `NotificationRecorder` / `McpAssert`
  - Deterministic sampling, notification capture, and framework-agnostic assertions.
- `McpServerFactory.Xunit` (separate package)
  - `McpServerFixture` for `IClassFixture<T>` boot-once-per-class.

## Logging in test output

By default, host logging providers are suppressed to keep test output clean.
To enable custom logging during tests:

```csharp
using Microsoft.Extensions.Logging;

var options = new McpServerFactoryOptions
{
    SuppressHostLogging = false,
    ConfigureLogging = logging => logging.SetMinimumLevel(LogLevel.Debug),
};
```

## Release notes

See [CHANGELOG.md](CHANGELOG.md) for release history and upcoming changes.

## Samples

- Minimal runnable sample: [`samples/MinimalSmoke`](samples/MinimalSmoke)

## Repository layout

- `src/McpServerFactory` — reusable factory library (framework-agnostic core).
- `src/McpServerFactory.Xunit` — xUnit fixtures (`McpServerFixture`).
- `tests/McpServerFactory.Tests` — unit/integration-focused library tests.
- `templates/McpServerFactory.Templates` — `dotnet new` template pack.
- `samples/MinimalSmoke` — runnable sample console app.
- `docs` — architecture notes and usage guidance.
