# 11. Ingestion pipeline with idempotent claims and a recovery sweep

Status: accepted

## Context

Documents are processed asynchronously by the worker. The queue can lose or duplicate messages (an API crash between the database commit and the publish, an in-memory queue that is not shared across processes, SQS at-least-once delivery, a worker killed mid-processing).

## Decision

- The database row is the source of truth. `Pending` means "must be processed".
- `IngestDocumentHandler` first claims the document with one atomic `UPDATE ... WHERE Status = Pending OR (Status = Processing AND UpdatedAt < now - lease)`. Only the winner proceeds, so duplicate messages and concurrent workers are harmless; a lost lease is reclaimed after `Ingestion:LeaseMinutes`.
- The result is written in one transaction: the status flips to `Ready` only if the row is still `Processing`, old chunks are replaced, new chunks are inserted. A document deleted meanwhile aborts cleanly.
- Retries with exponential backoff and jitter (Polly) wrap only the embedding calls. Permanent problems (corrupt file, no text, wrong embedding size) fail immediately with a user-safe message; transient ones fail after `Ingestion:MaxAttempts` with a generic message. Details go to the logs, never to the API.
- `DocumentRecoverySweeper` (hosted by the worker) periodically processes `Pending` rows older than a grace period and expired `Processing` leases. It makes the in-memory `Channels` queue usable across processes in local development and protects every provider against lost messages.
- Cancellation (shutdown) puts the claimed document back to `Pending`.
- Queue-level failures use the provider redelivery (`Abandon` with backoff) up to `Ingestion:MaxDeliveries`, then the document is marked `Failed`.

## Consequences

- At-least-once delivery with effectively-once processing.
- Status changes are broadcast with PostgreSQL `LISTEN/NOTIFY` (ADR 12), so the API sees changes made by the worker without a shared in-memory bus.
