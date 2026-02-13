# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned

- Placeholder for changes targeting `0.2.0` and later.

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

[Unreleased]: https://github.com/diomonogatari/mcp-server-factory/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/diomonogatari/mcp-server-factory/releases/tag/v0.1.0
