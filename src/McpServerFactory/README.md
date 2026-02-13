# McpServerFactory

`McpServerFactory` provides in-memory integration testing primitives for
.NET MCP servers, similar in spirit to `WebApplicationFactory<T>` for
ASP.NET Core APIs.

It starts an MCP server in-process, connects an `McpClient` through
stream pipes, and lets tests call tools directly with zero network setup.

## Why this exists

- MCP servers lacked a reusable .NET integration test factory.
- SDK test internals are not exposed as a dedicated testing package.
- `stash-mcp` needed realistic integration tests beyond unit-level mocks.

## What it gives you

- In-process MCP server host (`Host.CreateApplicationBuilder`)
- In-memory duplex transport wiring (`Pipe` + stream transports)
- Thread-safe `McpClient` creation and lifecycle management
- DI override hooks for mocked dependencies
- Optional helper wrapper (`McpTestClient`)

## API at a glance

- `McpServerIntegrationFactory`
  - `CreateClientAsync()` starts host + returns connected client
  - `Services` gives access to DI container after startup
  - Override `ConfigureMcpServer` and `ConfigureServices` for custom setup
- `McpServerFactory`
  - Backward-compatible entry point (same behavior and API contracts)
- `McpServerFactoryOptions`
  - Server info, startup/disposal timeouts, optional instructions
  - Logging controls (`SuppressHostLogging`, `ConfigureLogging`)
- `McpTestClient`
  - Convenience helper to list tool names and extract text responses

## Notes

- The current namespace is `McpServerFactory.Testing`.
- This package intentionally has no dependency on a specific test framework.
- Tests call MCP tools directly, so no LLM/token usage is required.
