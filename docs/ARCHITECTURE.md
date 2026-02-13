# Architecture

This repository hosts the standalone `McpServerFactory` library.

## Goal

Provide a reusable in-memory integration testing abstraction for .NET MCP
servers, similar in spirit to `WebApplicationFactory<T>` for ASP.NET Core.

## Transport model

- Client and server communicate through in-memory pipes.
- Server uses stream-based MCP server transport.
- Test client uses stream-based MCP client transport.
