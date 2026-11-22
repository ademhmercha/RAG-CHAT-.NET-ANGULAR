# 9. Liveness and readiness probes

Status: accepted

## Context

Orchestrators (Aspire, ECS, Kubernetes) restart a process that fails liveness and stop routing to one that fails readiness. Mixing the two causes restart storms when the database blips.

## Decision

- `/health/live`: process only (the `self` check). Never touches dependencies.
- `/health/ready`: a dedicated `DatabaseHealthCheck` that opens a plain connection (bypassing the EF retry strategy, which would stretch a probe to many seconds), bounds the probe to 5 s, and verifies the `vector` extension is installed, so an instance reports not-ready until migrations have run.
- Probes are always mapped (not only in Development) and return a bare status.

## Consequences

- A database outage flips readiness to 503 within seconds while the process keeps running.
- Covered by integration tests (healthy, database down, liveness unaffected).
