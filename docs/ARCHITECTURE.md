# Architecture

This repository hosts the standalone `McpServerFactory` library.

## Goal

Provide a reusable in-memory integration testing abstraction for .NET MCP
servers, similar in spirit to `WebApplicationFactory<T>` for ASP.NET Core.

## Transport model

- Client and server communicate through in-memory pipes.
- Server uses stream-based MCP server transport.
- Test client uses stream-based MCP client transport.

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
- `DisposeAsync` is safe to call repeatedly.

## Failure semantics

| Scenario | Behavior |
| --- | --- |
| Startup callback throws (`configureServices`, `configureMcpServer`) | Exception is propagated to caller. |
| Host startup fails | Temporary host is stopped/disposed before rethrow. |
| Client creation fails | Temporary host/client resources are disposed before rethrow. |
| `CreateClientAsync` after disposal | Throws `ObjectDisposedException`. |
| Host stop timeout on disposal | Disposal uses `ShutdownTimeout` to bound wait time. |

## Logging behavior

- Default behavior suppresses host logging providers to keep test output clean.
- Consumers can opt back in via `McpServerFactoryOptions`:
  - `SuppressHostLogging = false`
  - `ConfigureLogging = ...`
