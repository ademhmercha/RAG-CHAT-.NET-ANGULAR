# 5. Configurable embedding dimension

Status: accepted

## Context

`nomic-embed-text` (Ollama) produces 768 dimensions; Bedrock models produce other sizes (for example 256, 512 or 1024 for Titan v2). The vector column width must match the model in use.

## Decision

`Embedding:Dimensions` (default 768, validated between 1 and 2000, the HNSW limit) drives the `vector(n)` column type. EF Core caches one model per context type, so `EmbeddingAwareModelCacheKeyFactory` includes the dimension in the cache key; without it two contexts with different settings in one process would silently share the first model.

## Consequences

- Changing the dimension on an existing database requires a new migration and re-embedding every document; the migration is generated with the dimension set in the environment (`Embedding__Dimensions`).
- Unit tests assert the column type for several dimensions; integration tests assert the real column and index in PostgreSQL.
