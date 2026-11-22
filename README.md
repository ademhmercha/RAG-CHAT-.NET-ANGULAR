# DocMind: backend

An enterprise assistant built on retrieval-augmented generation (RAG). Users upload PDF and DOCX documents, then ask questions; the assistant answers from their own documents, streams the answer and cites its sources (document, page, excerpt).

This repository contains the .NET backend and the Angular web client ([`web/docmind-web`](web/docmind-web/README.md)). The AWS infrastructure (`infra/`) is a separate phase.

## Highlights

- **Clean Architecture**, enforced by executable architecture tests. No MediatR: plain handlers injected by DI.
- **RAG on PostgreSQL + pgvector** (HNSW, cosine). One database for relational data and vectors.
- **Provider-agnostic AI** through `Microsoft.Extensions.AI`: Ollama locally, Amazon Bedrock on AWS, chosen by configuration. Same for files (disk or S3) and the queue (in-memory channels or SQS).
- **Streaming chat over SSE** with token, tool, citation, done and error events; function calling with two tools, also exposed by an **MCP server**.
- **Robust ingestion**: atomic claims, retries with backoff, crash recovery, status pushed to clients with `LISTEN/NOTIFY`.
- **Security by default**: JWT access token plus rotating httpOnly refresh cookie with reuse detection, deny-by-default authorization, strict per-user data isolation, prompt-injection fencing, rate limiting, upload hardening, security headers.
- **Observability**: OpenTelemetry traces, metrics and logs; liveness and readiness probes.
- **407 tests** (unit, architecture, integration on real PostgreSQL through Testcontainers), zero warnings (`TreatWarningsAsErrors` with four analyzer packs).

## Architecture

```
                         ┌──────────────┐   ┌──────────────┐   ┌───────────────┐
                         │ DocMind.Api  │   │DocMind.Worker│   │DocMind.McpServer│   hosts (composition roots)
                         └──────┬───────┘   └──────┬───────┘   └───────┬───────┘
                                └──────────────────┼───────────────────┘
                                                   ▼
                                        ┌─────────────────────┐
                                        │ DocMind.Infrastructure│  EF Core, Identity/JWT, pgvector search,
                                        └──────────┬──────────┘  S3, SQS, Bedrock, Ollama, PdfPig, OpenXML
                                                   ▼
                                        ┌─────────────────────┐
                                        │  DocMind.Application │  handlers, validators, prompts, abstractions
                                        └──────────┬──────────┘
                                                   ▼
                                        ┌─────────────────────┐
                                        │    DocMind.Domain    │  entities, Result/Error
                                        └─────────────────────┘
```

Hosts never reference each other. `Domain` and `Application` never reference a provider SDK. Both rules are tests.

### Request flows

```
Upload     client ─POST /documents─▶ Api ─▶ IFileStorage + Document(Pending) ─▶ IMessageQueue
                                                                        │
Ingestion  Worker ◀──────────────────────────────────────────────────────┘ (and the recovery sweep)
           claim ─▶ extract text per page ─▶ chunk with overlap ─▶ embed (retry) ─▶ chunks in pgvector ─▶ Ready
           └─ pg_notify ─▶ Api LISTEN ─▶ SSE /documents/events

Chat       client ─POST /conversations/{id}/messages─▶ Api
           embed question ─▶ cosine top-K (caller's documents only) ─▶ fenced prompt ─▶ IChatClient (+ tools)
           ─▶ SSE: tool, token…, citation…, done
```

## Quick start

Prerequisites: .NET 10 SDK and Docker.

```bash
dotnet run --project src/DocMind.AppHost
```

The Aspire dashboard starts PostgreSQL (pgvector), Ollama (`llama3.2` and `nomic-embed-text`, downloaded on first start), the API and the worker. API reference (Development): `/scalar` on the API endpoint listed in the dashboard.

Without Aspire:

```bash
dotnet user-secrets set "ConnectionStrings:docmind" "Host=localhost;Database=docmind;Username=postgres;Password=..." --project src/DocMind.Api
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project src/DocMind.Api
dotnet run --project src/DocMind.Api       # applies migrations in Development
dotnet run --project src/DocMind.Worker    # needs the same connection string and AI settings
```

## Build and test

```bash
dotnet build DocMind.slnx                    # 0 warnings expected
dotnet test --solution DocMind.slnx          # Docker must be running (Testcontainers)
```

| Project | Scope |
|---|---|
| `DocMind.UnitTests` | domain, validators, chunker, prompt building, extractors, local storage, queue, AWS clients with mocked SDKs, provider selection |
| `DocMind.ArchitectureTests` | layer dependencies, conventions |
| `DocMind.IntegrationTests` | full API on PostgreSQL+pgvector: auth, conversations, SSE, upload and ingestion, RAG pipeline, tools, MCP, rate limiting, isolation, endpoint security matrix |

## Configuration

Every key maps to an environment variable with `__` (for example `Ai__Provider`). Secrets come from user-secrets locally and from the platform secret store on AWS.

| Section | Keys |
|---|---|
| Database | `ConnectionStrings:docmind` (required), `Database:ApplyMigrationsOnStartup` (false; true in Development) |
| Auth | `Jwt:SigningKey` (required, 32+ chars, secret), `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` (15), `Jwt:RefreshTokenDays` (7), `Jwt:RefreshTokenAbsoluteDays` (30), `Jwt:RefreshReuseLeewaySeconds` (10), `Jwt:RefreshCookieName` |
| CORS | `Cors:AllowedOrigins` (array, credentials allowed) |
| AI | `Ai:Provider` = `Ollama` or `Bedrock`; `Ai:Ollama:{Endpoint,ChatModel,EmbeddingModel,TimeoutSeconds}`; `Ai:Bedrock:{Region,ChatModelId,EmbeddingModelId,ServiceUrl,TimeoutSeconds}` |
| Embeddings | `Embedding:Dimensions` (768; 1 to 2000; Titan V2 accepts 256, 512, 1024) |
| Files | `Storage:Provider` = `Local` or `S3`; `Storage:Local:RootPath`; `Storage:S3:{BucketName,Region,KeyPrefix,ServiceUrl,ForcePathStyle,TimeoutSeconds}` |
| Queue | `Queue:Provider` = `Channels` or `Sqs`; `Queue:Sqs:{QueueUrl,Region,ServiceUrl,VisibilityTimeoutSeconds}` |
| Documents | `Documents:MaxSizeBytes` (20 MB), `Documents:MaxDocumentsPerUser` (500), `Documents:MaxPageSize` (100) |
| Ingestion | `Ingestion:{ChunkSize,ChunkOverlap,EmbeddingBatchSize,MaxAttempts,RetryBaseDelayMilliseconds,LeaseMinutes,SweepIntervalSeconds,PendingGraceSeconds,MaxConcurrency,MaxDeliveries,MaxPages,MaxCharacters,PollWaitSeconds}` |
| Retrieval | `Rag:{TopK,MinScore,FallbackCitationScore,MaxFallbackCitations,ExcerptCharacters,MaxContextCharacters,ShortQueryCharacters}` |
| Chat | `Chat:{HistoryMessages,Temperature,MaxOutputTokens,MaxToolIterations,ToolsEnabled}`, `Summary:{MaxCharacters,MaxSegments,Words,CacheHours}`, `Sse:HeartbeatMilliseconds` |
| Hardening | `RateLimiting:{Enabled,Global,Auth,Chat,Upload}:{PermitLimit,WindowSeconds}`, `ForwardedHeaders:Enabled`, `Security:Hsts`, `Security:RedirectToHttps` |
| Tools | `Documentation:Enabled` (OpenAPI + Scalar), `McpServer:UserId` (MCP server only) |

Invalid or missing configuration fails at startup with a precise message.

## API

All routes are under `/api/v1`; every error is `application/problem+json` (RFC 9457) with a stable `code` and a `traceId`. The contract is listed in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md); the OpenAPI document is served at `/openapi/v1.json` when documentation is enabled.

Authentication: `POST /auth/login` returns `{ accessToken, expiresAt, user }` and sets the `docmind_refresh` cookie (`HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`). Send the access token as `Authorization: Bearer ...`. `POST /auth/refresh` rotates the cookie. SSE endpoints require the header, so use `fetch` streaming rather than `EventSource`.

## MCP server

`DocMind.McpServer` speaks MCP over stdio and exposes `search_documents` and `summarize_document` for **one** DocMind account, configured with `McpServer:UserId`.

```json
{
  "mcpServers": {
    "docmind": {
      "command": "dotnet",
      "args": ["run", "--project", "src/DocMind.McpServer"],
      "env": {
        "McpServer__UserId": "<user guid>",
        "ConnectionStrings__docmind": "Host=localhost;Database=docmind;Username=postgres;Password=...",
        "Storage__Local__RootPath": "/path/to/storage"
      }
    }
  }
}
```

## Containers and migrations

```bash
docker build -f src/DocMind.Api/Dockerfile       -t docmind-api .
docker build -f src/DocMind.Worker/Dockerfile    -t docmind-worker .
docker build -f src/DocMind.McpServer/Dockerfile -t docmind-mcp .
docker build -f Dockerfile.migrations            -t docmind-migrations .
```

Images are multi-stage, based on chiseled .NET runtimes (no shell, non-root user, listening on 8080). Probes: `GET /health/live` (process) and `GET /health/ready` (database reachable and pgvector installed).

Apply schema changes before starting a new version, with the self-contained bundle:

```bash
docker run --rm docmind-migrations --connection "Host=...;Database=docmind;Username=...;Password=..."
```

Create a migration after changing the model: `dotnet ef migrations add <Name> --project src/DocMind.Infrastructure --startup-project src/DocMind.Infrastructure --output-dir Persistence/Migrations`. A test fails if the model and the migrations diverge. Changing `Embedding:Dimensions` on an existing database requires a new migration and re-ingesting documents.

## Running on AWS

| Need | Setting |
|---|---|
| Aurora/RDS PostgreSQL with pgvector (0.8+) | `ConnectionStrings__docmind`; the migrating role must be allowed to `CREATE EXTENSION vector` |
| S3 bucket | `Storage__Provider=S3`, `Storage__S3__BucketName`, `Storage__S3__Region` |
| SQS queue (+ dead-letter queue on the queue itself) | `Queue__Provider=Sqs`, `Queue__Sqs__QueueUrl`, `Queue__Sqs__Region` |
| Bedrock | `Ai__Provider=Bedrock`, `Ai__Bedrock__Region`, `Ai__Bedrock__ChatModelId`, `Embedding__Dimensions=1024` |
| Behind an ALB | `ForwardedHeaders__Enabled=true`, `Cors__AllowedOrigins__0=https://app.example.com` |
| Secret | `Jwt__SigningKey` from Secrets Manager |

Run the API and the worker as separate services from the same configuration. Task roles need the S3, SQS and Bedrock permissions listed in [ADR 15](docs/adr/0015-aws-providers.md).

## Security model

- Every owned row carries `UserId` and every query is scoped by it; foreign resources answer 404, indistinguishable from missing ones. Vector search filters by user in the same SQL statement.
- The model can only reach the caller's data: tools receive the user from the server, never from model output.
- Retrieved text is fenced with a per-request random boundary and treated as untrusted.
- Uploads are validated by extension, magic bytes and size, stored under opaque keys, and parsed with page and character caps.
- Refresh tokens are stored hashed, rotated on every use, revoked as a family on replay, with an absolute lifetime.

## Documentation

- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md): architecture notes, commands, conventions, API contract
- [docs/adr](docs/adr): architecture decision records (16)
