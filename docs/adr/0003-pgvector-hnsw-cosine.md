# 3. pgvector with an HNSW cosine index

Status: accepted

## Context

Retrieval needs nearest-neighbour search over chunk embeddings, filtered by owner. Running a separate vector database adds an operational dependency, a second consistency boundary, and a second place where tenant isolation must be enforced.

## Decision

- Store embeddings in PostgreSQL with the `vector` extension, in the same database as the rest of the data.
- Index with HNSW and `vector_cosine_ops` (`m = 16`, `ef_construction = 64`). It needs no training step and holds recall as data grows.
- `DocumentChunk.UserId` is denormalized from the document so the owner filter applies in the same query as the distance ordering.
- `Pgvector.Vector` (a plain data type with no EF dependency) is used directly on the domain entity. Mapping it through a value converter would stop EF from translating `CosineDistance`.

## Consequences

- One database to back up, migrate and secure; cascades remove chunks with their document or user.
- HNSW with a selective `WHERE` can return fewer rows than requested. The retrieval step (phase 5) enables iterative scans (`hnsw.iterative_scan`) and covers the case with a test.
- The extension is created by the first migration, which requires a role allowed to create extensions (on RDS: `rds_superuser`).
