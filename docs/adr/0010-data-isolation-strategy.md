# 10. Per-user data isolation

Status: accepted

## Context

A user must never read or modify another user's documents, chunks, conversations or messages. The same data is reached from three hosts: HTTP requests (a current user exists), the worker (no HTTP user), and the MCP server.

## Decision

- Every owned table carries `UserId` (chunks denormalize it from the document).
- Isolation is explicit: every handler query is scoped by the caller's `UserId`, and vector search filters on `UserId` in the same statement as the distance ordering. Resources belonging to someone else are reported as 404, not 403, so existence is not leaked.
- EF global query filters are not used: they silently return nothing in the worker and MCP contexts where no user is resolved, which turns configuration mistakes into confusing empty results.
- Dedicated isolation tests (phase 8) create two users and assert that every endpoint refuses cross-user access.

## Consequences

- Each new query needs a `UserId` predicate; reviews and the isolation test matrix guard against omissions.
- Foreign keys cascade from the user, so deleting an account removes all of its data.
