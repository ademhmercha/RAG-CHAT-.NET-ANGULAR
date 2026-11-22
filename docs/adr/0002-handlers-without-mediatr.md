# 2. Plain handlers instead of MediatR

Status: accepted

## Context

MediatR adds an indirection (request, pipeline, handler) that the project does not need: there is no cross-cutting pipeline beyond validation and no notification fan-out.

## Decision

Each use case is a `sealed` class named `<UseCase>Handler` exposing `HandleAsync`. `AddApplication()` registers every public handler as scoped by assembly scan, and FluentValidation validators by `AddValidatorsFromAssembly`. Endpoints inject the handler they need and call it.

## Consequences

- Navigation is direct: endpoint, handler, database.
- Validation is applied explicitly at the endpoint boundary (a reusable endpoint filter), not through a hidden pipeline.
- Conventions (sealed, `Handler` suffix, no handler outside `Application`) are checked by architecture tests.
