# 16. Hardening: rate limits, upload limits, headers, secure by default

Status: accepted

## Context

The API is internet-facing and handles untrusted files, an expensive LLM endpoint and credentials.

## Decision

- Authorization is deny-by-default: a fallback policy requires an authenticated user, and only `register`, `login`, `refresh` and `logout` opt out. A test enumerates every endpoint and fails if the anonymous set changes or a protected endpoint answers anonymous callers with anything but 401.
- Rate limiting (`RateLimiting:*`): fixed window per client address for the credential endpoints (default 20/min), sliding window per user for chat and uploads (default 20/min each), and a global per-user-or-address limit (default 300/min). Health probes are exempt. Rejections are ProblemDetails with `Retry-After`. Behind a load balancer enable `ForwardedHeaders:Enabled` so addresses are real client addresses.
- Upload limits: extension and magic-byte check, size cap (`Documents:MaxSizeBytes`), per-user quota, an early `413` from `Content-Length` before the body is read, multipart and Kestrel limits, sanitized file names, opaque storage keys, page and character caps in the extractor.
- Account protection: lockout after five failures, uniform credential errors with timing equalisation, refresh-token rotation with reuse detection and an absolute session lifetime.
- Responses carry `nosniff`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` and a restrictive CSP (not on the documentation UI); `Server` header removed; HSTS and HTTPS redirection are opt-in because TLS usually terminates at the load balancer.
- Container images use chiseled .NET runtime bases (no shell, non-root user) built in multi-stage Dockerfiles; schema changes ship as a self-contained EF Core migration bundle executed before the new version starts.

## Consequences

- Limits are per instance; a shared limiter (for example at the load balancer or WAF) is needed for strict global quotas when scaling out.
- Tests disable rate limiting by default and enable it in dedicated test classes.
