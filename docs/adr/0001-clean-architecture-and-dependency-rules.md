# 1. Clean Architecture with enforced dependency rules

Status: accepted

## Context

DocMind has three hosts (API, ingestion worker, MCP server) that must share the same business logic, and several infrastructure choices (Ollama or Bedrock, disk or S3, Channels or SQS) that change between local development and AWS.

## Decision

Four layers with inward-only dependencies:

- `Domain`: entities, value objects, `Result`. No framework dependency, except the `Pgvector.Vector` data type (see ADR 3).
- `Application`: use cases (handlers), validators, abstractions (`IAppDbContext`, `ICurrentUser`, and later `IFileStorage`, `IMessageQueue`). No provider SDK, no ASP.NET.
- `Infrastructure`: EF Core, Identity, and the provider implementations.
- Hosts (`Api`, `Worker`, `McpServer`): composition roots. They never reference each other.

`Application` references `Microsoft.EntityFrameworkCore` (not Npgsql) so handlers query `IAppDbContext` directly instead of through a repository layer that would only mirror `DbSet`.

## Consequences

- The rules are executable: `DocMind.ArchitectureTests` inspects the referenced assemblies and fails the build when a layer reaches where it should not (for example `Application` referencing Npgsql, Ollama, or AWS SDKs).
- Swapping a provider never touches business code.
- A new host (for example a CLI) reuses `Application` unchanged.
