# DocMind: web client

The Angular front end of DocMind. Users sign in, upload PDF and DOCX documents, follow their processing live, then chat with an assistant that streams answers and cites its sources (document, page, excerpt).

## Highlights

- **Angular 22**, standalone components, signals, `@if` / `@for`, `inject()`, **OnPush everywhere**, zoneless change detection.
- **State with NgRx SignalStore**: `AuthStore`, `ConversationsStore`, `ChatStore`, `DocumentsStore`, `DocumentScopeStore`. Stores wipe themselves when the session ends.
- **Authentication without storing tokens**: the access token lives in memory only; the refresh token is an httpOnly cookie. Refresh is single-flight, 401 responses are replayed once, the session is restored at startup.
- **Streaming chat over `fetch` + `ReadableStream`** (an `EventSource` cannot send an `Authorization` header): token, tool, citation, done and error events, stop button through `AbortController`, smart auto-scroll.
- **Safe rich text**: Markdown rendered with `marked`, sanitized with DOMPurify, code highlighted with highlight.js, copy button on code blocks.
- **Live document status** from the backend SSE endpoint, with reconnection and back-off.
- **Drag and drop uploads** with per-file progress, client-side validation (type, size, empty file), cancellation and a bounded upload queue.
- **Material 3 theme** with light, dark and system modes, **French and English** at runtime (Transloco), WCAG-minded accessibility (skip link, landmarks, focus management, live regions, reduced motion), responsive down to phones.
- **Contract-first types** generated from the backend OpenAPI document.
- **Runtime configuration**: the API URL comes from `config.json`, not from the build, so one image runs everywhere.

## Quick start

Requirements: Node.js 24 (or 22.22+) and a running backend.

```bash
npm ci
npm start                    # http://localhost:4200, proxies /api to http://localhost:5080
```

The dev server proxies `/api` to the backend so the refresh cookie (`SameSite=Strict`) stays same-origin. Point it elsewhere with `API_URL=http://host:port npm start`.

With .NET Aspire the `web` resource of `src/DocMind.AppHost` starts this app next to the API and injects the API address.

## Scripts

| Script | Purpose |
|---|---|
| `npm start` | dev server with API proxy |
| `npm run build:prod` | production build in `dist/docmind-web/browser` |
| `npm run lint` | ESLint (angular-eslint, typescript-eslint, template accessibility rules) |
| `npm run format` / `format:check` | Prettier |
| `npm test` | unit tests (Vitest through `ng test`) |
| `npm run e2e` | Playwright end-to-end tests (`npm run e2e:install` once) |
| `npm run api:types` | regenerate `src/app/core/api/schema.d.ts` from the running API's OpenAPI document |
| `npm run api:types:offline` | regenerate it from the committed `openapi/openapi.json` snapshot |

A change is done when `npm run lint`, `npm test` and `npm run build:prod` pass.

## Structure

```
src/app/
  core/        singleton infrastructure, no UI
    api/         generated schema, typed aliases, ApiUrl
    auth/        AuthStore, AuthApi (token in memory, single-flight refresh)
    config/      runtime config loading (config.json) and startup failure page
    guards/      authGuard, guestGuard (open-redirect safe return urls)
    http/        ProblemDetails mapping, SSE parser, authorized fetch
    i18n/        language service, Transloco loader, translated paginator
    interceptors/ auth (bearer, 401 replay) and error (snackbar) interceptors
    services/    theme, notifications, clipboard, title strategy
  shared/      reusable, presentational building blocks
    confirm-dialog, empty-state, error-state, skeleton, form-error, markdown, format pipes
  features/    lazy-loaded, one folder per route area
    auth/        login, register, auth layout
    chat/        conversation list, message list/item, composer, citation panel, scope selector
    documents/   dropzone, upload queue, documents table, status chip
    settings/    profile, appearance, session
  layout/      application shell, user and preferences menus
```

Dependency rule: `features` may use `core` and `shared`; `core` never imports from `features` or `layout`; features do not import from one another except for the documents API reused by the chat document selector.

## Design notes

**Why not `EventSource`?** It cannot set headers, and the API authenticates with a bearer token. `AuthorizedFetch` opens the stream with `fetch`, replays once after a 401 refresh, and `parseSse` decodes the stream (CRLF, multi-line data, keep-alive comments, UTF-8 split across chunks). It is shared by chat and document events.

**Why upload with `XMLHttpRequest`?** `fetch` cannot report upload progress, and the progress bar is part of the experience. `DocumentUploader` mirrors the same auth behaviour as the interceptor.

**Error handling.** Every API error is a ProblemDetails with a `code`. `ProblemMessages` maps `code` to a translated message (`errors.codes.<code>`), falls back to the status, and adds a short trace reference for server errors. The error interceptor shows a snackbar unless the caller handles the error inline (`SKIP_ERROR_NOTIFICATION`).

**Stores.** Each store owns its async methods and exposes read-only signals and computed values. Components stay thin: they bind signals and call store methods. Stream consumption (`ChatStore.send`) guards against stale responses and cancellation races.

**Security.** No token in storage, strict CSP in the nginx image, Markdown sanitized before it is trusted, raw HTML in model output escaped, links open with `noopener noreferrer nofollow`, return urls validated against open redirects, no secrets in the bundle.

## Runtime configuration

`public/config.json` is fetched before the app bootstraps:

```json
{ "apiBaseUrl": "", "environment": "development" }
```

An empty `apiBaseUrl` means "same origin" (the dev proxy or nginx forwards `/api`). Set an absolute URL to call another origin; the API must then allow it with CORS and cookies, and `CSP_CONNECT_SRC` must list it. A missing or invalid file shows an explicit failure page.

## Docker

```bash
docker build -t docmind-web .
docker run --rm -p 8080:8080 -e API_UPSTREAM=http://api:8080 docmind-web
```

The image is a multi-stage build (Node build, unprivileged nginx runtime). Environment variables:

| Variable | Default | Meaning |
|---|---|---|
| `API_UPSTREAM` | `http://api:8080` | where nginx proxies `/api/` (SSE unbuffered) |
| `API_BASE_URL` | empty | written to `config.json` (empty = same origin) |
| `APP_ENVIRONMENT` | `production` | written to `config.json` |
| `CSP_CONNECT_SRC` | `'self'` | `connect-src` of the Content-Security-Policy |

`/healthz` answers the container health check. The SPA fallback, long-lived caching of hashed assets and `no-cache` for `index.html` and `config.json` are configured in `nginx/default.conf.template`.

## Testing

- **Unit tests** (`npm test`): stores, interceptors, guards, SSE parser, Markdown sanitization, validators, formatting and components, with `HttpTestingController` and Transloco test setup.
- **End-to-end tests** (`npm run e2e`): Playwright drives Chromium through registration, sign-in and sign-out, session restore, document upload with live status, deletion, a streamed answer with a clickable citation, conversation rename and delete, language and theme persistence and the mobile drawer. The API is replaced by an in-memory stub (`e2e/fixtures/mock-api.ts`) so the suite is deterministic and needs no model. Set `E2E_REAL_API=1` with the backend running to also run a smoke test against the real API, and `E2E_BASE_URL` to test an already running instance (for example the Docker image).

## Known limitations

- The API has no endpoint to download or preview the original file, so a citation opens a panel with the excerpt, page and document details rather than the PDF itself.
- There is no profile update endpoint, so the profile section is read-only.
- The OpenAPI document does not describe the SSE streams; their payloads are typed by hand in `chat-stream.service.ts` and `document-events.service.ts`.
- The upload size limit (20 MB) mirrors the backend default in `upload-validation.ts`; the server remains the source of truth and its rejection is reported to the user.
