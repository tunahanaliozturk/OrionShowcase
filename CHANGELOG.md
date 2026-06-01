# Changelog

All notable changes to OrionShowcase are recorded here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.1] - 2026-06-01

### Fixed

- CI workflow now runs unit tests and Testcontainers integration tests in separate steps so the unit-test gate stops hanging on the Testcontainers-vs-CI-service-Postgres race that cancelled the v0.1.0 release workflow. With the workflow green, the release event triggers GHCR Docker image publish for `ghcr.io/tunahanaliozturk/orionshowcase-api:v0.1.1` and `:latest`.

### Added

- 5 Mermaid diagrams in README: architecture, transfer sequence, family component map, daily settlement gating flowchart, OpenTelemetry trace tree.
- `benchmarks.md` linking to per-package benchmark documents.
- `CONTRIBUTING.md` and `CODE_OF_CONDUCT.md`.

No code changes since v0.1.0. All public API and runtime behaviour are identical.

## [0.1.0] - 2026-05-28

### Added

- Initial release. Production-shaped banking sample integrating all six Moongazing.Orion packages end-to-end.
- Clean Architecture with Domain, Application, Infrastructure, Api layers.
- Account aggregate with Open, Deposit, Withdraw, Transfer, Freeze, Close. Customer aggregate with Register. Pure domain code with no framework dependencies.
- 10 Minimal API endpoints (auth + customers + accounts + 2 read queries) with JWT bearer authentication.
- MediatR pipeline behaviors: Logging, Validation (OrionGuard FluentStyleValidator), Idempotency, Audit.
- EF Core 9 + PostgreSQL persistence. 9-table initial migration applied on startup.
- OrionVault column encryption on Customer.NationalId, Customer.Email, Customer.Phone via the `OrionVault:Encrypted` EF annotation. Ciphertext on disk, plaintext in code.
- OrionPatch transactional outbox via DomainEventOutboxAdapter (a custom SaveChangesInterceptor that walks the EF change tracker, enqueues domain events via reflection on `IOutbox.Enqueue<T>`, and preserves at-least-once semantics across save failures).
- OrionLock.Postgres distributed locks. Used in TransferMoneyHandler with sorted-key deadlock prevention and in DailySettlementService for single-instance hosted-job gating.
- OrionAudit entity-diff interceptor for Account, Customer, Transaction. Captures RFC 6902 JSON Patch documents into `OrionAudit_Log` automatically on SaveChanges.
- Separate command-level audit via EfAuditWriter writing to `command_audit_entries` (actor, action, request JSON, response JSON, success, errorMessage). Bridges Application's `IAuditWriter` interface.
- OrionKey static facade configured at startup with `OrionKey:WorkerId` from configuration. Snowflake IDs used for CommandAuditEntry primary keys.
- OrionKey-backed `IIdempotencyStore` implemented over EF Core (`idempotency_records` table). The MediatR `IdempotencyBehavior` resolves it for every command implementing `IIdempotentCommand`.
- OrionGuard FluentStyleValidator on all seven commands. Replaces FluentValidation entirely so the showcase practices what it preaches.
- OrionGuard.AspNetCore `UseOrionGuardValidation()` middleware for RFC 9457 ProblemDetails on validation failures.
- Domain-layer OrionGuard usage: `Ensure.NotNull/NotNullOrWhiteSpace/InRange` for argument guards, `FastGuard.NotNull` on hot paths (Money operators), `Ensure.For(value).NotNull().Must(...).Build()` fluent chains in Customer.Register and Tckn ctor, `Contract.Requires/Invariant` for domain invariants in Account.Open, Account.RecordTransfer, Money operator -, Iban mod-97, Tckn checksums.
- Per-endpoint rate-limit policies (`login`, `transfer`, `query`) under `OrionGuard:Policies` configuration. Backed by ASP.NET Core's in-box RateLimiter middleware (OrionGuard.AspNetCore 6.4.2 does not ship rate limiting; the policy-name convention preserves narrative consistency).
- OpenTelemetry traces and metrics exported to Jaeger via OTLP. Registers ActivitySources/Meters from OrionGuard, OrionAudit (note: source name is `OrionAudit` without the `Moongazing.` prefix), OrionLock, OrionKey (Meter only), OrionPatch (ActivitySource only), OrionVault.
- Serilog structured logging to console and Seq.
- Docker compose: api + postgres + seq + jaeger with health checks. One `docker compose up` brings the full stack online in about 60 seconds.
- Domain unit tests (30), Application unit tests (21), IntegrationTests with Testcontainers Postgres covering register, open account, deposit, transfer, and PII-at-rest encryption verification.

### Known limitations

- Authentication is a dev-grade JWT issuer with hardcoded `demo`/`demo` user. Real deployments should integrate OIDC (Keycloak, IdentityServer, Auth0).
- Single Account aggregate. Loan, KYC, multi-tenant features are on the v0.2+ roadmap.
- No frontend (API + Swagger only). Blazor admin shell is on the v1.0 roadmap.
- No Kubernetes manifests. Docker compose only. Helm chart is on the v1.0 roadmap.
- Logo is a placeholder borrowed from OrionVault. Dedicated briefcase glyph is planned for v0.1.1.
- OrionGuard.AspNetCore 6.4.2 is validation + ProblemDetails; rate limits are ASP.NET Core's in-box RateLimiter middleware. If a future OrionGuard.AspNetCore release ships rate limiting, the showcase will swap to it.

[0.1.0]: https://github.com/tunahanaliozturk/OrionShowcase/releases/tag/v0.1.0
