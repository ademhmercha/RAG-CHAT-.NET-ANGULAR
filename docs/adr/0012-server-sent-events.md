# 12. Server-Sent Events for chat and document status

Status: accepted

## Context

The chat answer must stream token by token, and the document list must react to status changes made by another process (the worker).

## Decision

- Both streams are SSE written by a small custom `IResult`. It disables buffering, sends `X-Accel-Buffering: no`, emits a `: keep-alive` comment every `Sse:HeartbeatMilliseconds` (default 15 s, below typical proxy idle timeouts) and formats multi-line data safely (a payload can never forge an event).
- Chat events: `token`, `tool`, `citation`, `done`, `error`. Errors raised after the stream starts become an `error` event carrying a ProblemDetails document; errors before it are ordinary ProblemDetails responses.
- The producer is cancelled whenever the write loop ends for any reason, and the pump waits for it to stop before disposing, so a disconnected client cannot leave a half-running model call. If the client leaves mid-answer the partial answer is still stored (the `finally` of the async iterator runs on disposal).
- Document status changes use `pg_notify`/`LISTEN` through a single dedicated connection per API instance (`DocumentEventHub`) that fans out to bounded per-subscriber channels filtered by `UserId`.
- `EventSource` cannot send an `Authorization` header, so the frontend must consume these endpoints with `fetch` streaming. Putting the token in the query string is deliberately not supported.

## Consequences

- Events emitted while a client is disconnected are lost; clients refetch the list when the stream (re)opens.
- Each API instance holds one extra database connection for notifications.
