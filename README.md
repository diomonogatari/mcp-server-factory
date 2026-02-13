# McpServerFactory

[![NuGet](https://img.shields.io/nuget/v/McpServerFactory.svg)](https://www.nuget.org/packages/McpServerFactory)
[![NuGet Downloads](https://img.shields.io/nuget/dt/McpServerFactory.svg)](https://www.nuget.org/packages/McpServerFactory)
[![CI](https://github.com/diomonogatari/mcp-server-factory/actions/workflows/ci.yml/badge.svg)](https://github.com/diomonogatari/mcp-server-factory/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/diomonogatari/mcp-server-factory/branch/main/graph/badge.svg)](https://codecov.io/gh/diomonogatari/mcp-server-factory)
[![license](https://img.shields.io/github/license/diomonogatari/mcp-server-factory.svg?maxAge=2592000)](https://github.com/diomonogatari/mcp-server-factory/blob/main/LICENSE)
![.NET 10.0](https://img.shields.io/badge/.net-10.0-yellowgreen.svg)
![Status: preview](https://img.shields.io/badge/status-0.1.0_(preview)-orange.svg)

In-memory integration testing factory for .NET Model Context Protocol (MCP)
servers.

`McpServerFactory` gives .NET MCP servers a testing experience similar in
spirit to `WebApplicationFactory<T>` for ASP.NET Core. It boots an MCP server
in-process, connects a real `McpClient` through in-memory streams, and lets you
run realistic integration tests without external services or network ports.

## Installation

```bash
dotnet add package McpServerFactory
```

## Quick start

```csharp
using McpServerFactory.Testing;
using ModelContextProtocol.Server;

[McpServerToolType]
public sealed class EchoTools
{
    [McpServerTool(Name = "echo")]
    public string Echo(string message) => message;
}

await using var factory = new McpServerFactory(
    configureMcpServer: builder => builder.WithTools<EchoTools>());

await using var client = await factory.CreateClientAsync();

var result = await client.CallToolAsync(
    "echo",
    arguments: new Dictionary<string, object?> { ["message"] = "hello" });
```

## Why use it

- In-memory transport using the MCP SDK's public stream APIs.
- Real MCP protocol flow (`tools/list`, `tools/call`) in tests.
- Easy dependency overrides through `configureServices`.
- Framework-agnostic library (works with xUnit, NUnit, MSTest, etc.).

## API overview

- `McpServerFactory`
  - Starts an in-process server host.
  - Creates a connected `McpClient` via `CreateClientAsync()`.
  - Exposes `Services` for DI validation after startup.
- `McpServerFactoryOptions`
  - Configure server identity, initialization timeout, and instructions.
- `McpTestClient`
  - Convenience wrapper for common test operations.

## Release notes

See [CHANGELOG.md](CHANGELOG.md) for release history and upcoming changes.

## Repository layout

- `src/McpServerFactory` — reusable factory library.
- `tests/McpServerFactory.Tests` — unit/integration-focused library tests.
- `docs` — architecture notes and usage guidance.
