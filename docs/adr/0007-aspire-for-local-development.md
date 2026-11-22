# 7. .NET Aspire for local development

Status: accepted

## Context

A working local environment needs PostgreSQL with pgvector, Ollama with two models, the API and the worker, started in the right order with shared configuration and telemetry.

## Decision

`DocMind.AppHost` orchestrates `pgvector/pgvector:pg17` (with PgAdmin), Ollama (`llama3.2`, `nomic-embed-text`), the API (waits for the database, exposes `/health/ready` as its health probe) and the worker (waits for the API, which applies migrations in Development). `DocMind.ServiceDefaults` provides OpenTelemetry, service discovery and the liveness check to every host.

Aspire 17 marks `Aspire.AppHost.Sdk` obsolete, so the AppHost uses `Microsoft.NET.Sdk` with explicit `Aspire.Hosting.AppHost` / `Aspire.Hosting.Dotnet` packages and `AddDotnetProject`.

The default `AddStandardResilienceHandler` is deliberately not applied to every `HttpClient`: its 10 s attempt timeout would cut long LLM generations. AI clients get dedicated resilience policies in the phases that add them.

## Consequences

- `dotnet run --project src/DocMind.AppHost` is the only command needed locally.
- The same environment variable names (`ConnectionStrings__docmind`, `Ai__*`) work outside Aspire and on AWS.
