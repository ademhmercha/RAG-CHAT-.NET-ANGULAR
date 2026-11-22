# 13. Retrieval, prompt layout and citations

Status: accepted

## Context

Answers must be grounded in the user's documents, resist instructions hidden in those documents, and cite their sources.

## Decision

- Retrieval embeds the question (prefixed with the previous question when it is short, so follow-ups still retrieve) and runs a cosine top-K search restricted to the caller's `Ready` documents (optionally to a selected subset) in one SQL statement. Results below `Rag:MinScore` are dropped. Iterative HNSW scans are enabled per transaction so selective filters still return K rows.
- The stored user message is the raw question. The prompt sent to the model wraps the retrieved passages between `=== SOURCES <boundary> ===` markers where `<boundary>` is 64 random bits generated per request; any occurrence of it inside the text is removed, so a document cannot close the block. The system prompt states that the content is untrusted data, forbids following instructions inside it and forbids revealing the markers.
- The model is told to cite with `[n]`. Citations are derived from the markers actually present in the answer; if there are none, sources scoring above `Rag:FallbackCitationScore` are cited (at most `Rag:MaxFallbackCitations`). Markers outside the source range are ignored. Citations are persisted with the assistant message.
- When nothing relevant is found the model is told so explicitly. When the user has no ready document at all the question is sent as is (plain assistant).
- The retrieval step is reported as a `search_documents` tool event so the UI can show progress.

## Consequences

- A small model that ignores the citation instruction still gets citations for strongly matching sources, at the cost of occasionally citing a source it did not literally use.
- Chunk size is measured in characters (about four per token) to stay independent from the embedding model's tokenizer.
