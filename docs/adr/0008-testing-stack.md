# 8. Testing stack

Status: accepted

## Context

The tests must prove behaviour against the real infrastructure that matters (PostgreSQL with pgvector) and keep architectural rules from eroding.

## Decision

- xUnit v3 on Microsoft.Testing.Platform (`global.json` selects the runner; `dotnet test --solution DocMind.slnx`).
- FluentAssertions pinned to 7.2.x: version 8 moved to a commercial licence.
- Integration tests use Testcontainers with `pgvector/pgvector:pg17` and one throw-away database per test class, applied through the real migrations. In-memory or SQLite providers are not used because they cannot exercise pgvector, JSONB or cascade behaviour.
- `WebApplicationFactory<Program>` hosts the API; configuration is injected with `UseSetting` so it is visible to code that reads configuration before the host is built.
- NetArchTest plus assembly-reference checks enforce the layer rules (ADR 1).
- `SchemaTests` checks `HasPendingModelChanges()` and fails when a model change ships without a migration.

## Consequences

- Docker is required to run the integration tests.
- Analyzers run on test projects too, with a small, documented relaxation set in `tests/.editorconfig`.
