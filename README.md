# McpServerFactory

[![NuGet](https://img.shields.io/nuget/v/McpServerFactory.svg)](https://www.nuget.org/packages/McpServerFactory)
[![NuGet Downloads](https://img.shields.io/nuget/dt/McpServerFactory.svg)](https://www.nuget.org/packages/McpServerFactory)
[![CI](https://github.com/diomonogatari/mcp-server-factory/actions/workflows/ci.yml/badge.svg)](https://github.com/diomonogatari/mcp-server-factory/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/diomonogatari/mcp-server-factory/branch/main/graph/badge.svg)](https://codecov.io/gh/diomonogatari/mcp-server-factory)
[![license](https://img.shields.io/github/license/diomonogatari/mcp-server-factory.svg?maxAge=2592000)](https://github.com/diomonogatari/mcp-server-factory/blob/main/LICENSE)
![.NET 10.0](https://img.shields.io/badge/.net-10.0-yellowgreen.svg)
![Status: 0.1.0](https://img.shields.io/badge/status-0.1.0-brightgreen.svg)

In-memory integration testing factory for .NET Model Context Protocol (MCP) servers.

`McpServerFactory` gives .NET MCP servers a testing experience similar in spirit to
`WebApplicationFactory<T>` for ASP.NET Core. It boots an MCP server in-process,
connects a real `McpClient` through in-memory streams, and lets you run realistic
integration tests without network ports, Docker, or external services.

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
package version (default `0.1.0`).

## Quick start

```csharp
using McpServerFactory.Testing;
using ModelContextProtocol.Server;

[McpServerToolType]
public sealed class EchoTools
{
    [McpServerTool(Name = "echo")]
    public string Echo(string message)
    {
        return message;
    }
}

await using var factory = new McpServerIntegrationFactory(
    configureMcpServer: builder => builder.WithTools<EchoTools>());

await using var client = await factory.CreateClientAsync();

var result = await client.CallToolAsync(
    "echo",
    arguments: new Dictionary<string, object?> { ["message"] = "hello" });
```

## Why use this library

- In-memory transport using the MCP SDK's public stream APIs.
- Real MCP protocol flow (`tools/list`, `tools/call`) in tests.
- Easy dependency overrides through `configureServices` and `ConfigureServices`.
- Framework-agnostic library (works with xUnit, NUnit, MSTest, etc.).

## Behavioral guarantees

- `CreateClientAsync` is thread-safe and idempotent per factory instance.
- Concurrent calls return the same connected `McpClient` instance.
- Startup failures do not leak the temporary host instance.
- `DisposeAsync` is safe to call multiple times.
- `CreateClientAsync` throws `ObjectDisposedException` after disposal.

## Compatibility and support

- Target framework: `net10.0`.
- MCP SDK dependency: `ModelContextProtocol` `0.4.0-preview.3`.
- Compatibility promise: each package release is validated against the pinned MCP SDK version.
- Upgrade policy: MCP SDK bumps are explicit and called out in
  [CHANGELOG.md](CHANGELOG.md).

## API overview

- `McpServerFactory`
  - Backward-compatible factory entry point.
- `McpServerIntegrationFactory`
  - Preferred factory type for new code.
  - Starts an in-process server host.
  - Creates a connected `McpClient` via `CreateClientAsync()`.
  - Exposes `Services` for DI validation after startup.
- `McpServerFactoryOptions`
  - Configure server identity, startup/disposal timeouts, and instructions.
  - Suppress host logging by default and optionally customize logging.
- `McpTestClient`
  - Convenience wrapper for common test operations.

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

- `src/McpServerFactory` — reusable factory library.
- `tests/McpServerFactory.Tests` — unit/integration-focused library tests.
- `templates/McpServerFactory.Templates` — `dotnet new` template pack.
- `samples/MinimalSmoke` — runnable sample console app.
- `docs` — architecture notes and usage guidance.
