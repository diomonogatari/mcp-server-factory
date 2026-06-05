# Architecture

This repository hosts the standalone `McpServerFactory` library.

## Goal

Provide a reusable in-memory integration testing abstraction for .NET MCP
servers, similar in spirit to `WebApplicationFactory<T>` for ASP.NET Core.

## What it hosts (and what it does not)

By default the factory builds a fresh host, registers the MCP server, attaches the
in-memory transport, and invokes your `configureMcpServer`/`configureServices` callbacks
to register the tool/resource/prompt classes and services under test. It does **not**
auto-run your server's `Program.cs`.

To exercise your real composition root — configuration binding, options, hosted services —
supply `McpServerFactoryOptions.ConfigureHost`, which receives the `IHostApplicationBuilder`
before the MCP server is registered. The factory always owns the MCP server registration and
the in-memory transport, so `ConfigureHost` should run everything **except** the transport;
register tools/resources/prompts via `configureMcpServer`. (A post-hoc transport swap of a
consumer's full `AddMcpServer().WithStdioServerTransport()` chain would be fragile across SDK
versions, so the convention is to keep transport selection with the factory.)

## Transport model

- Client and server communicate through in-memory pipes.
- Server uses the stream-based MCP server transport (a single session).
- Test client uses the stream-based MCP client transport.
- One factory hosts one in-memory session. For isolation between servers, create multiple
  factory instances — each owns its own host.

## Registration order

`ConfigureLogging` → `ConfigureHost` → `AddMcpServer` + stream transport →
`configureMcpServer` → `configureServices`. DI is last-wins, so `configureServices` can
override anything registered earlier (this is how test doubles are substituted).

## Lifecycle flow

```text
┌──────────────────────────────────────────────────────────────────────┐
│ McpServerIntegrationFactory                                          │
│                                                                      │
│  CreateClientAsync()                                                 │
│   1) Acquire lifecycle lock                                          │
│   2) Build host + register tools/services                            │
│   3) Start host                                                      │
│   4) Create McpClient over in-memory pipes                           │
│   5) Cache host/client and return shared client instance             │
│                                                                      │
│  DisposeAsync()                                                      │
│   1) Acquire lifecycle lock                                          │
│   2) Dispose client                                                  │
│   3) Stop + dispose host (bounded by ShutdownTimeout)               │
│   4) Complete pipe reader/writer endpoints                           │
└──────────────────────────────────────────────────────────────────────┘
```

## Concurrency model

- Initialization and disposal are coordinated by a lifecycle semaphore.
- `CreateClientAsync` is idempotent for a factory instance.
- Concurrent calls return the same connected `McpClient` instance.
- `DisposeAsync` is safe to call repeatedly. Its lock acquisition is bounded by
  `ShutdownTimeout`; if the lock cannot be taken in time (for example a concurrent
  initialization is stuck), disposal proceeds best-effort without the lock rather than
  hanging test teardown indefinitely.

## Failure semantics

| Scenario | Behavior |
| --- | --- |
| Startup callback throws (`configureServices`, `configureMcpServer`, `ConfigureHost`) | Exception is propagated to caller. |
| Host startup fails | Temporary host is stopped/disposed before rethrow. |
| Client creation fails | Temporary host/client are disposed and the freshly created pipes are completed before rethrow (no leak). |
| `CreateClientAsync` after disposal | Throws `ObjectDisposedException`. |
| Host stop timeout on disposal | Disposal uses `ShutdownTimeout` to bound wait time. |

## Logging behavior

- Default behavior suppresses host logging providers to keep test output clean.
- Consumers can opt back in via `McpServerFactoryOptions`:
  - `SuppressHostLogging = false`
  - `ConfigureLogging = ...`
