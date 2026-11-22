# DocMind: developer guide

DocMind is an enterprise RAG assistant: users upload PDF/DOCX documents and chat with an AI that answers with citations. This repository holds the .NET backend. A frontend (`web/docmind-web`, Angular) and AWS infrastructure (`infra/`) come in later phases. See the [README](../README.md) for the full backend documentation.

## Status

Backend complete (steps 1 to 8): foundations, authentication, conversations and streaming chat, documents and ingestion, RAG with citations, function calling and MCP server, AWS providers (S3, SQS, Bedrock), hardening (rate limiting, upload limits, isolation tests, Dockerfiles, migration bundle).

## Stack

.NET 10 (LTS) · ASP.NET Core Minimal APIs · EF Core 10 + Npgsql + pgvector · ASP.NET Core Identity + JWT · FluentValidation · Microsoft.Extensions.AI (Ollama / Bedrock) · HybridCache · Polly · PdfPig · OpenXML SDK · .NET Aspire 17 · OpenTelemetry · xUnit v3, FluentAssertions 7, NSubstitute, Testcontainers, NetArchTest.

## Layout

```
src/
  DocMind.Domain           entities, Result/Error, no framework dependency (only Pgvector.Vector)
  DocMind.Application      handlers, validators, prompts, abstractions (no provider SDK, no ASP.NET)
  DocMind.Infrastructure   EF Core, Identity/JWT, pgvector search, AWS and Ollama providers, extractors
  DocMind.Api              Minimal API host
  DocMind.Worker           ingestion consumer + recovery sweep + token purge
  DocMind.McpServer        MCP over stdio (search_documents, summarize_document)
  DocMind.ServiceDefaults  OpenTelemetry, service discovery, health endpoints
  DocMind.AppHost          Aspire orchestration for local development
tests/                     UnitTests, IntegrationTests (Testcontainers), ArchitectureTests, Shared (test documents)
docs/adr/                  architecture decision records
```

Dependency rule: `Domain <- Application <- Infrastructure <- hosts`; hosts never reference each other (architecture tests).

## Commands

```bash
dotnet build DocMind.slnx                       # 0 warnings (TreatWarningsAsErrors)
dotnet test --solution DocMind.slnx             # needs Docker (Testcontainers)
dotnet test --project tests/DocMind.UnitTests
dotnet run --project src/DocMind.AppHost        # Postgres+pgvector, Ollama, Api, Worker + dashboard

dotnet tool restore
dotnet ef migrations add <Name> \
  --project src/DocMind.Infrastructure --startup-project src/DocMind.Infrastructure \
  --output-dir Persistence/Migrations

docker build -f src/DocMind.Api/Dockerfile -t docmind-api .     # also Worker, McpServer, Dockerfile.migrations
```

## Configuration

See the table in [README](../README.md#configuration). Required: `ConnectionStrings:docmind`, `Jwt:SigningKey`. Provider switches: `Ai:Provider`, `Storage:Provider`, `Queue:Provider`. Secrets never go in git (`dotnet user-secrets` locally, secret store on AWS). Aspire generates and persists the JWT key.

## Conventions

- C# in English, file-scoped namespaces, nullable, `TreatWarningsAsErrors`. Analyzers (.NET, Meziantou, Roslynator, Sonar) are global; relax a rule only in `.editorconfig` with a reason.
- Central Package Management: every version in `Directory.Packages.props`.
- Handlers: public `sealed` classes named `<UseCase>Handler` in `Application`, registered by scan, returning `Result`/`Result<T>`; they take the `userId` explicitly. No MediatR.
- Entities: private setters, factory methods, `Guid.CreateVersion7()`, time from `TimeProvider`.
- Endpoints: one static internal `<Feature>Endpoints` class per feature under `MapApiV1`; request validation with `ValidationFilter<T>`.
- Every owned query is scoped by `UserId`; other users' resources answer 404 (ADR 10). A test enumerates all endpoints: only register, login, refresh and logout are anonymous.
- Errors are always ProblemDetails with a `code` and a `traceId`; unhandled exceptions return an opaque 500. Provider failures never reach the client in detail.
- New configuration gets an options class with validation (`ValidateOnStart`) and a row in the README table.
- Optional arguments of AI tools must have default values.
- Comments only where the code cannot say it.

## API contract (v1)

Auth: `POST /api/v1/auth/register {email,password,displayName}` -> 201 user · `POST /auth/login {email,password}` -> `{accessToken,expiresAt,user}` + refresh cookie · `POST /auth/refresh` (cookie) -> `{accessToken,expiresAt,user}` · `POST /auth/logout` -> 204 · `GET /auth/me`.

Documents: `POST /api/v1/documents` (multipart field `file`, PDF or DOCX, `Documents:MaxSizeBytes`) -> 201 · `GET /documents?page&pageSize&status` -> `{items,page,pageSize,totalCount,totalPages}` · `GET /documents/{id}` · `DELETE /documents/{id}` -> 204 · `GET /documents/events` (SSE `event: status`, data `{documentId,status,errorMessage,updatedAt}`, status in Pending, Processing, Ready, Failed).

Conversations: `POST /api/v1/conversations {title?}` -> 201 · `GET /conversations` · `GET /conversations/{id}` (with messages, citations, usage) · `PATCH /conversations/{id} {title}` · `DELETE /conversations/{id}` -> 204 · `POST /conversations/{id}/messages {content,documentIds?}` -> SSE:

```
event: token     data: { text }
event: tool      data: { name, status }          status: running | completed | failed
event: citation  data: { documentId, documentName, page, excerpt }
event: done      data: { messageId, usage }      usage: { inputTokens, outputTokens, totalTokens }
event: error     data: ProblemDetails
```

Health: `GET /health/live`, `GET /health/ready`.

Status codes: 400 validation, 401, 404 (also for foreign resources), 409 (duplicate email, quota, document not ready), 413, 415, 429 (with `Retry-After`).

## ADRs

See [adr](adr). Add one for every significant decision.
