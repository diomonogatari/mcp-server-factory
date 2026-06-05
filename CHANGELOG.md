# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-06-05

First stable release. The official MCP C# SDK reached `1.0`, so `McpServerFactory` stabilizes its
own public surface in lockstep and now follows [Semantic Versioning](https://semver.org/).

### Changed

- **Bumped the MCP SDK** `ModelContextProtocol` from `0.4.0-preview.3` to the stable **`1.4.0`**.
  Target frameworks (`net8.0`, `net9.0`, `net10.0`) and the public `McpServerFactory` API are
  unchanged — this is a dependency bump, not an API break for consumers.

### Internal

- Migrated to SDK API changes introduced across the `0.5.0`–`1.4.0` line:
  - Replaced the removed `EnumerateToolsAsync`/`EnumerateResourcesAsync`/`EnumeratePromptsAsync`
    client streams with the paging-draining `ListToolsAsync`/`ListResourcesAsync`/`ListPromptsAsync`.
  - `CallToolResult.StructuredContent` is now a `JsonElement?` (was `JsonNode?`); `CallToolForJsonAsync`
    deserializes from it accordingly.
  - `McpClient.ReadResourceAsync` now takes a `System.Uri`; `ReadResourceTextAsync` still accepts a
    `string` URI and converts internally.
  - `CreateMessageResult.Content` and `SamplingMessage.Content` are now `IList<ContentBlock>`;
    `FakeSamplingHandler` builds a single-block list.

## [0.2.0] - 2026-06-05

### Added

- **Multi-targeting** `net8.0`, `net9.0`, and `net10.0` (was `net10.0` only), so net8 LTS and net9
  servers can consume the package. `Microsoft.Extensions.*` versions are floored per target framework.
- **`McpServerFactory.Xunit`** companion package with an `McpServerFixture` base
  (`IClassFixture<T>`) to boot the server once per test class.
- **Server-initiated feature testing**:
  - `McpServerFactoryOptions.ConfigureClient` to declare client capabilities and handlers.
  - `FakeSamplingHandler` to satisfy server-initiated sampling deterministically and record requests.
  - `NotificationRecorder` to capture and await server-sent notifications.
- **`McpServerFactoryOptions.ConfigureHost`** to run your real composition root (configuration,
  options, hosted services) while the factory owns the in-memory transport.
- **`McpServerFactory.CreateTestClientAsync`** returning a factory-owned `McpTestClient` (no
  hand-wrapping, no double-dispose).
- Grown `McpTestClient` surface: `GetToolNamesAsync`/`GetToolAsync`, `CallToolForJsonAsync<T>`
  (structured or JSON-text), `ReadResourceTextAsync`, `GetResourceUrisAsync`, prompt helpers,
  and `ServerInfo`/`ServerCapabilities`/`ServerInstructions` — all pagination-safe.
- `McpAssert` framework-agnostic assertions and `McpToolCallException`.

### Changed

- `CallToolForTextAsync` now throws `McpToolCallException` on an error result instead of returning
  the error text as success; added `CallToolExpectingErrorAsync` for negative-path tests.
- `McpTestClient` no longer disposes its client by default (the factory owns it), removing the
  double-dispose in the documented patterns.
- README reframed as a test harness (not a production builder), with a comparison table,
  when-to-use guidance, and the explicit note that it re-hosts your registrations unless
  `ConfigureHost` is used.
- The `mcp-itest` template now scaffolds `CreateTestClientAsync`, and its default
  `McpServerFactory` version is injected at pack time so it tracks the package version.

### Fixed

- Startup-failure path no longer orphans the freshly created in-memory pipes.
- `DisposeAsync` lock acquisition is bounded by `ShutdownTimeout` and will not hang test teardown.

## [0.1.0] - 2026-02-13

### Added

- Initial standalone repository scaffold for `McpServerFactory`.
- Core in-memory MCP integration testing abstractions:
  - `McpServerFactory`
  - `McpServerIntegrationFactory`
  - `McpServerFactoryOptions`
  - `McpTestClient`
- CI workflow for restore, format verification, build, tests, and coverage upload.
- Release workflow to pack and publish to NuGet.org and GitHub Packages from release tags.
- First smoke integration tests covering tool registration, invocation, and DI override behavior.
- Lifecycle hardening tests for concurrency, cancellation, disposal idempotency, and startup-failure recovery.
- SourceLink package integration for source-indexed symbol publishing.
- XML documentation for public library API surface.
- Minimal runnable sample project under `samples/MinimalSmoke`.
- `dotnet new` template package (`McpServerFactory.Templates`) with `mcp-itest`
  scaffold for xUnit integration tests.
- CI validation that packs, installs, instantiates, and runs tests from the
  generated template output.

### Documentation

- Public README with installation, quick start, and release guidance.
- Compatibility and behavioral guarantees documentation.
- Expanded architecture guide with lifecycle flow and failure semantics.
- Template installation and usage guidance.

[Unreleased]: https://github.com/diomonogatari/mcp-server-factory/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/diomonogatari/mcp-server-factory/compare/v0.2.0...v1.0.0
[0.2.0]: https://github.com/diomonogatari/mcp-server-factory/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/diomonogatari/mcp-server-factory/releases/tag/v0.1.0
