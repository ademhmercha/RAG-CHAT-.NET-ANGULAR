# 14. One tool implementation for the chat and the MCP server

Status: accepted

## Context

`search_documents` and `summarize_document` must be callable by the model during a chat (function calling) and by external MCP clients, without duplicating logic or weakening isolation.

## Decision

- The behaviour lives once in `DocumentTools` (Application). `ChatToolFactory` exposes it as `AIFunction`s through Microsoft.Extensions.AI; `DocumentMcpTools` exposes it through the MCP SDK. Names and descriptions are shared constants.
- The user identity is never a tool parameter. The chat binds it from the authenticated request; the MCP server binds it from configuration (`McpServer:UserId`, required, validated at start). The server is therefore a personal, local (stdio) server for one account; a multi-user remote MCP endpoint would use HTTP transport with the same JWT validation.
- When a chat request selects documents, both tools are restricted to that selection.
- Optional tool arguments have default values; models regularly omit them.
- Tool failures are returned as data (`error` field) with fixed, safe messages. Exception details are not sent to the model.
- Summaries use map-reduce over sampled segments for long documents and are cached with `HybridCache`. The key contains the document id and its last update time and is only read after the ownership check; deleting a document removes its entries by tag.

## Consequences

- Behaviour is identical across entry points and tested through both.
- The model can only reach the data of the user who is talking to it.
