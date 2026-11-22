# 4. Result pattern for expected failures

Status: accepted

## Context

Not-found, conflict, validation and authorization outcomes are normal control flow. Modelling them as exceptions is slow, hides the contract in handler signatures, and makes status mapping scattered.

## Decision

Handlers and domain methods return `Result` or `Result<T>` carrying an `Error(Code, Description, ErrorType)`. The API layer converts them with a single mapping (`ErrorType` to HTTP status, body as RFC 9457 `ProblemDetails` with a stable `code` extension). Exceptions remain for programming errors and infrastructure faults; the global exception handler turns them into an opaque 500 with a trace identifier.

## Consequences

- Error codes (`documents.not_found`) are a stable contract usable by the frontend.
- Domain state machines (for example `Document.MarkReady`) report invalid transitions without throwing.
- Argument guards on invariants that callers must respect still throw (`ArgumentException`).
