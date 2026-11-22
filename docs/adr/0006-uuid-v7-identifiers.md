# 6. UUID v7 identifiers generated in the domain

Status: accepted

## Context

Random GUID keys fragment B-tree indexes. Identifiers also need to exist before persistence (the upload publishes a queue message referring to the document).

## Decision

Entities generate `Guid.CreateVersion7()` in their factory methods; EF is configured with `ValueGeneratedNever`. All timestamps are `DateTimeOffset` in UTC, taken from an injected `TimeProvider`, which Npgsql maps to `timestamptz`.

## Consequences

- Insert-friendly, roughly time-ordered keys without a database round trip.
- Time is injectable, so state transitions and timestamps are deterministic in tests.
