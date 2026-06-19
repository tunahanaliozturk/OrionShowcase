

<h1 align="center">Moongazing.OrionShowcase</h1>

<p align="center">
  <strong>Production-shaped banking sample integrating all six Moongazing.Orion packages.</strong><br/>
  <em>Clean Architecture, EF Core, MediatR, OpenTelemetry. One `docker compose up` away from a working stack.</em>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-blueviolet?style=flat-square" alt=".NET 8" />
  <img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="MIT License" />
  <img src="https://img.shields.io/github/actions/workflow/status/tunahanaliozturk/OrionShowcase/ci-cd.yml?style=flat-square&label=Build" alt="Build" />
  <a href="https://github.com/tunahanaliozturk/OrionShowcase"><img src="https://img.shields.io/github/stars/tunahanaliozturk/OrionShowcase?style=flat-square" alt="Stars" /></a>
</p>

<p align="center">
  <em>Uses:</em>
  <a href="https://github.com/tunahanaliozturk/OrionGuard">OrionGuard</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionAudit">OrionAudit</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionLock">OrionLock</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionKey">OrionKey</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionPatch">OrionPatch</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionVault">OrionVault</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionLens">OrionLens</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionShade">OrionShade</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionGrant">OrionGrant</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionLedger">OrionLedger</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionOnce">OrionOnce</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionSaga">OrionSaga</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionRelay">OrionRelay</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionStream">OrionStream</a> ·
  <a href="https://github.com/tunahanaliozturk/OrionBeacon">OrionBeacon</a> ·
  <a href="https://github.com/tunahanaliozturk/Orion.Abstractions">Orion.Abstractions</a>
</p>

---

> **Note on the logo.** This release ships a placeholder chest glyph borrowed from OrionVault. A dedicated briefcase-with-star logo is on the roadmap for v0.1.1.

---

## What this is

OrionShowcase is a banking sample written the way a senior .NET team would write one for production: Clean Architecture, CQRS via MediatR, EF Core 9 with PostgreSQL, JWT auth, OpenTelemetry, full Docker stack. What makes it different from every other "clean architecture sample" on GitHub is that it integrates the entire Moongazing.Orion family end-to-end. Reading the code shows you what each Orion package does in a real workflow rather than in isolated quickstarts.

There is nothing in this repo that you could not have written yourself. The point is that the integrated story is much harder to assemble than any single package looks. Seeing one transfer request fan out through OrionGuard's validation, OrionKey's idempotency, OrionLock's distributed locks, OrionAudit's entity-diff capture, OrionPatch's outbox, and OrionVault's PII decryption is the whole pitch.

### Maturity-wave showcases

Three capabilities from the maturity-wave package releases (OrionGuard 6.6, OrionOnce 0.2, OrionVault 0.3), each in a real banking scenario:

- Asynchronous validation (OrionGuard 6.6). Customer registration runs a national-id uniqueness check inside the async validation pipeline (`Validate.For(command).MustAsync(...).ToResultAsync(ct)`). The I/O-bound rule executes in the same failure-aggregation pass as the structural rules and flows back through the existing `ValidationBehavior`. See [`RegisterCustomerUniquenessValidator.cs`](src/Moongazing.OrionShowcase.Application/Customers/Commands/RegisterCustomer/RegisterCustomerUniquenessValidator.cs).
- Idempotent money movement (OrionOnce 0.2). The transfer endpoint runs the transfer through an `IdempotentExecutor` keyed on the `Idempotency-Key` request header. A retried POST with the same key replays the captured typed result (same transfer id, same balance) instead of moving money a second time. The in-memory store is used for the sample; a durable store fits production. See [`TransferEndpoint.cs`](src/Moongazing.OrionShowcase.Api/Endpoints/Accounts/TransferEndpoint.cs) and [`OrionOnceTransferExtensions.cs`](src/Moongazing.OrionShowcase.Api/Idempotency/OrionOnceTransferExtensions.cs).
- Searchable encryption via blind index (OrionVault 0.3). The customer national id is stored as randomized ciphertext and alongside it a deterministic HMAC blind index (`national_id_index`). Equality lookups (the uniqueness check, find-by-national-id) run as an indexed equality seek over the blind index without decrypting any row. Uniqueness is enforced at the database level by a UNIQUE FILTERED index on the blind-index column (`unique` where `national_id_index IS NOT NULL`), so a race between two concurrent registrations cannot insert the same national id even if both pass the async validator. The blind-index column is nullable because it was added to an already-populated table: pre-existing customers keep a null blind index (and are excluded from the unique constraint by the filter) until a production deployment backfills them by recomputing the blind index for each existing national id. The index key is a demo-only value from configuration, distinct from the encryption key. See [`CustomerConfiguration.cs`](src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs) and [`OrionVaultNationalIdIndexer.cs`](src/Moongazing.OrionShowcase.Infrastructure/Vault/OrionVaultNationalIdIndexer.cs).
- SSE resume on the account activity stream (OrionStream 0.2). The activity-stream endpoint reads the `Last-Event-ID` request header a reconnecting browser `EventSource` sends and passes it to `Subscribe(topic, lastEventId)`. The hub replays the account-activity events published after that cursor from a bounded per-topic replay buffer before live events flow, so a client that reconnects misses nothing across the gap. Each event carries a stable, per-topic-unique wire id used as the resume cursor; the per-subscriber buffer is sized to cover a replay burst. See [`AccountActivityStreamEndpoint.cs`](src/Moongazing.OrionShowcase.Api/Endpoints/Accounts/AccountActivityStreamEndpoint.cs), [`AccountActivityPublisher.cs`](src/Moongazing.OrionShowcase.Api/Streaming/AccountActivityPublisher.cs), and [`OrionStreamExtensions.cs`](src/Moongazing.OrionShowcase.Api/Streaming/OrionStreamExtensions.cs).
- Account locks: distributed mutations + reader-writer reads (OrionLock 0.4). Balance MUTATIONS (deposit, withdraw, transfer) are serialized by the DISTRIBUTED Postgres advisory lock (`IDistributedLock`). This is the real cross-replica safety mechanism: because the advisory lock lives in the shared Postgres instance, a multi-replica deployment still serializes mutations of the same account across every process. A transfer takes the distributed holds on its two accounts in sorted id order to avoid deadlock, and existing transfer idempotency is unchanged. Separately, the reader-writer lock (`ISharedExclusiveLock`) demonstrates SHARED-read vs EXCLUSIVE semantics: the balance read (GetBalance) takes a SHARED hold, and the mutation handlers take an additional in-process EXCLUSIVE hold so a read never observes a half-applied mutation within a process. That reader-writer provider is in-memory and therefore SINGLE-PROCESS / SAMPLE-ONLY (OrionLock 0.4 ships shared/exclusive semantics for the in-memory backend only); it must not be mistaken for the cross-replica guarantee, which the distributed lock provides. In production a distributed reader-writer provider (for example Redis) would unify the two behind the same abstractions with no handler change. The in-memory provider is the canonical, writer-starvation-safe one from the `OrionLock.Testing` package. See the deposit/withdraw/transfer handlers, [`GetAccountBalanceHandler.cs`](src/Moongazing.OrionShowcase.Application/Accounts/Queries/GetAccountBalance/GetAccountBalanceHandler.cs), and the lock wiring in [`InfrastructureServiceCollectionExtensions.cs`](src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs).
- Saga step timeouts (OrionSaga 0.2). The account-opening saga gives its `validate-customer` step a per-step timeout. If the step overruns, OrionSaga cancels it and rolls back the completed steps, and the run reports the distinct `TimedOut` outcome. The handler and saga logging treat a timeout or a caller cancellation as an operational signal rather than a generic business failure, keeping the happy path unchanged. See [`AccountOpeningSaga.cs`](src/Moongazing.OrionShowcase.Application/Accounts/Sagas/AccountOpeningSaga.cs) and [`OpenAccountHandler.cs`](src/Moongazing.OrionShowcase.Application/Accounts/Commands/OpenAccount/OpenAccountHandler.cs).
- Webhook dead-lettering (OrionRelay 0.2). A `transfer.completed` partner webhook that exhausts its retry budget, or hits a fatal non-retryable response, is routed to an `IDeadLetterSink` exactly once with its terminal failure context instead of being lost. The showcase opts in to the bounded in-memory sink (capacity-limited so a prolonged partner outage cannot grow the working set without bound); the OrionRelay default is a no-op sink, and a durable sink fits production. Dead-lettered entries are observable via a structured log line and the admin diagnostics endpoint `GET /api/admin/webhooks/dead-letters`. See [`OrionRelayExtensions.cs`](src/Moongazing.OrionShowcase.Api/Webhooks/OrionRelayExtensions.cs), [`WebhookDeliveryLogObserver.cs`](src/Moongazing.OrionShowcase.Api/Webhooks/WebhookDeliveryLogObserver.cs), and [`WebhookDeadLettersEndpoint.cs`](src/Moongazing.OrionShowcase.Api/Endpoints/Admin/WebhookDeadLettersEndpoint.cs).
- Outbox dead-letter and archival (OrionPatch 0.3). An outbox message that exhausts its delivery attempts is routed into a durable dead-letter store (`IDeadLetterStore`, table `orion_patch_dead_letter`) exactly once and removed from the hot outbox, rather than dead-lettered in place. Separately, successfully dispatched rows past a retention window are archived (`IOutboxArchivalStore`, table `orion_patch_outbox_archive`) and purged from the active outbox while pending and dead-lettered rows are left untouched, keeping the claim-query working set small. The EF storage that supplies both capabilities composes over the bundled `EfCoreOutboxStorage`, and a hosted service drives the periodic archival sweep. See [`EfCoreOutboxDeadLetterArchivalStorage.cs`](src/Moongazing.OrionShowcase.Infrastructure/Outbox/EfCoreOutboxDeadLetterArchivalStorage.cs) and [`OutboxArchivalService.cs`](src/Moongazing.OrionShowcase.Infrastructure/HostedServices/OutboxArchivalService.cs).
- API key rotation and bulk revoke (OrionLedger 0.2). A partner can rotate the API key it is authenticating with (`POST /api/partner/api-key/rotate`): a successor key is issued inheriting the predecessor's subject and scopes, and within a grace window both the old and new keys verify before the old key retires. An administrator can revoke every active key for a subject in one call (`POST /api/admin/api-keys/revoke-subject`), which skips already-inactive keys and reports how many were revoked. See [`PartnerRotateApiKeyEndpoint.cs`](src/Moongazing.OrionShowcase.Api/Endpoints/Partner/PartnerRotateApiKeyEndpoint.cs) and [`AdminRevokeSubjectKeysEndpoint.cs`](src/Moongazing.OrionShowcase.Api/Endpoints/Admin/AdminRevokeSubjectKeysEndpoint.cs).

## Five-minute experience

```bash
git clone https://github.com/tunahanaliozturk/OrionShowcase
cd OrionShowcase
docker compose -f docker/compose.yaml up -d
```

Within about 60 seconds, four containers are healthy: api, postgres, seq, jaeger. Open these in a browser:

- `http://localhost:5000/swagger` — the API
- `http://localhost:5341` — Seq (logs)
- `http://localhost:16686` — Jaeger (traces)

In Swagger:

1. `POST /api/auth/login` with `{ "username": "demo", "password": "demo" }`. Copy the access token.
2. Click "Authorize" and paste `Bearer <token>`.
3. `POST /api/customers` to register a customer. TCKN/Email/Phone arrive as plaintext, are written to Postgres as ciphertext (verify with `psql -c "SELECT national_id FROM customers"` — you will see binary garbage).
4. `POST /api/accounts` twice to open two accounts for that customer.
5. `POST /api/accounts/{fromId}/transfer` between them.

Then in Jaeger, open the trace for that one transfer request. You will see spans from all six Orion packages alongside HTTP and EF Core spans. In Seq you will see the structured log lines from the same trace id. In Postgres you can `SELECT * FROM outbox_messages` and see the `TransferCompleted` event waiting for dispatch. This integrated trace is the central artefact this repo exists to provide.

## Architecture

Four-layer Clean Architecture with one-way dependency flow.

```mermaid
flowchart TB
    Api["<b>Api</b><br/>Minimal API endpoints · JWT · Swagger<br/>OrionGuard.AspNetCore validation middleware"]
    App["<b>Application</b><br/>MediatR commands/queries/handlers<br/>OrionGuard FluentStyleValidator pipeline<br/>Logging · Idempotency · Audit behaviors"]
    Infra["<b>Infrastructure</b><br/>EF Core + Postgres · OrionVault wiring<br/>OrionPatch outbox · OrionLock backend<br/>OrionAudit entity-diff capture<br/>OrionKey-backed idempotency store<br/>Hosted services (OutboxDispatcher, DailySettlement)"]
    Domain["<b>Domain</b><br/>Aggregates · value objects · domain events<br/>OrionGuard Ensure/FastGuard/FluentGuard/Contract<br/>No framework references"]

    Api --> App
    App --> Infra
    Infra --> Domain
    Infra -.knows.-> Domain

    classDef api fill:#dbeafe,stroke:#1e40af,color:#1e3a8a
    classDef app fill:#e0e7ff,stroke:#3730a3,color:#312e81
    classDef infra fill:#fce7f3,stroke:#9d174d,color:#831843
    classDef domain fill:#f5f3ff,stroke:#5b21b6,color:#4c1d95
    class Api api
    class App app
    class Infra infra
    class Domain domain
```

Tests:

- `OrionShowcase.Domain.Tests` (30 tests, xunit + FluentAssertions, no infrastructure)
- `OrionShowcase.Application.Tests` (43 tests, in-memory fakes for repositories/locks/idempotency; covers async uniqueness validation, OrionOnce idempotent execution, the OrionVault blind index, reader-writer writer-starvation safety, and that a transient saga timeout is not cached as a final idempotent result)
- `OrionShowcase.IntegrationTests` (Testcontainers Postgres, real DI graph, end-to-end register/open/transfer + PII-at-rest verification)

## Transfer flow (one request, six packages)

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant API as Api endpoint
    participant OG as OrionGuard<br/>validation
    participant MED as MediatR pipeline
    participant OK as OrionKey<br/>idempotency
    participant OA as OrionAudit<br/>+ EfAuditWriter
    participant H as TransferMoneyHandler
    participant OL as OrionLock<br/>(Postgres advisory)
    participant DB as Postgres<br/>(banking)
    participant OV as OrionVault<br/>value converter
    participant OP as OrionPatch<br/>outbox interceptor

    U->>+API: POST /api/accounts/{from}/transfer
    API->>OG: validate request DTO
    OG-->>API: ok
    API->>+MED: Send(TransferMoneyCommand)
    MED->>OK: TryClaim(idempotencyKey)
    OK-->>MED: claimed
    MED->>OA: begin audit
    MED->>+H: Handle(cmd)
    H->>OL: AcquireAsync(account:lower)
    H->>OL: AcquireAsync(account:higher)
    OL-->>H: 2 leases held
    H->>+DB: SELECT accounts (Customer join)
    DB->>OV: decrypt national_id / email / phone
    OV-->>DB: plaintext
    DB-->>-H: account aggregates
    H->>H: Withdraw + Deposit + RecordTransfer<br/>(raises TransferCompleted event)
    H->>+DB: SaveChanges
    DB->>OP: SavingChanges interceptor flushes domain events
    OP->>DB: INSERT outbox_messages (TransferCompleted)
    DB->>OA: SavingChanges interceptor captures entity diff
    OA->>DB: INSERT OrionAudit_Log (JSON Patch)
    DB-->>-H: committed
    H->>OL: dispose 2 leases
    H-->>-MED: Result.Ok(transferId, newBalance)
    MED->>OA: end audit (success + response JSON)
    MED->>OK: StoreResponse(idempotencyKey, json)
    MED-->>-API: response
    API-->>-U: 200 OK
```

Every numbered step lands as a span in Jaeger and a structured log line in Seq with the same trace id.

## What each Orion package does in this codebase

### OrionGuard — validation, guards, ProblemDetails

OrionGuard does the most work in this showcase. We use four parts of its surface area:

**Endpoint-level validation + RFC 9457 ProblemDetails middleware:**

- [`Program.cs`](src/Moongazing.OrionShowcase.Api/Program.cs) wires `AddOrionGuardAspNetCore()` and `UseOrionGuardValidation()`.
- Every command validator inherits from `Moongazing.OrionGuard.Compatibility.FluentStyleValidator<TCommand>` — the drop-in replacement for FluentValidation's `AbstractValidator<T>`. Same `RuleFor(x => x.Property).NotEmpty().MaximumLength(100)` syntax. See [`TransferMoneyValidator.cs`](src/Moongazing.OrionShowcase.Application/Accounts/Commands/TransferMoney/TransferMoneyValidator.cs) and the six sibling validators under `Application/{Customers,Accounts}/Commands/`.

**MediatR pipeline integration:**

- [`ValidationBehavior.cs`](src/Moongazing.OrionShowcase.Application/Pipeline/ValidationBehavior.cs) resolves `Moongazing.OrionGuard.Core.IValidator<TRequest>` instances from DI and runs them before each handler.

**Domain-layer guards (the part that proves we eat our own dog food):**

- `Ensure.NotNull(value, name)` and `Ensure.NotNullOrWhiteSpace(value, name)` replaced every `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` call across the Domain. They preserve the standard .NET argument-exception contract so existing tests and downstream consumers see the same exception types.
- `FastGuard.NotNull` (aggressive-inlined, allocation-free) is used in hot paths like [`Money.cs`](src/Moongazing.OrionShowcase.Domain/ValueObjects/Money.cs) operator overloads.
- `Ensure.For(value, name).NotNull().Must(...).MinLength(...).Email(...).Build()` fluent chains appear in [`Customer.Register`](src/Moongazing.OrionShowcase.Domain/Customers/Customer.cs), [`Tckn`](src/Moongazing.OrionShowcase.Domain/ValueObjects/Tckn.cs) constructor, and [`Account.Deposit`](src/Moongazing.OrionShowcase.Domain/Accounts/Account.cs).
- `Contract.Requires(condition, message)` and `Contract.Invariant(condition, message)` express Design-by-Contract style domain invariants in [`Account.Open`](src/Moongazing.OrionShowcase.Domain/Accounts/Account.cs) (opening deposit non-negative), `Account.RecordTransfer` (counterparty != self), `Money` `operator -` (no negative result), `Iban` (mod-97 checksum), `Tckn` (10th and 11th digit checksums).

**Why we use OrionGuard's validator and not FluentValidation:** consistency with our own pitch. If a showcase for the Orion family ships with a competing validation library, why would a reader trust the recommendation? The FluentStyleValidator base class is API-compatible — `RuleFor(x => x.Foo).NotEmpty()` works unchanged. Migration was a `using` change.

### OrionAudit — automatic entity-diff capture

OrionAudit installs a SaveChangesInterceptor on `BankingDbContext` that records every INSERT/UPDATE/DELETE on opted-in entity types as RFC 6902 JSON Patch documents in an `OrionAudit_Log` table.

- [`InfrastructureServiceCollectionExtensions.cs`](src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs) calls `AddOrionAudit<BankingDbContext>(o => o.Audit<Account>().Audit<Customer>().Audit<Transaction>())`.
- [`BankingDbContext.OnModelCreating`](src/Moongazing.OrionShowcase.Infrastructure/Persistence/BankingDbContext.cs) calls `ApplyOrionAuditConfigurations(this)` to map the audit log, snapshot cursor, and capture queue tables.
- The DbContext options builder calls `UseOrionAudit(sp)` so the interceptor runs on every save.

In addition to OrionAudit's entity-level diff stream, [`EfAuditWriter.cs`](src/Moongazing.OrionShowcase.Infrastructure/Audit/EfAuditWriter.cs) records command-level audit rows (actor, action, request JSON, response JSON, succeeded, errorMessage) into a separate `command_audit_entries` table. The two streams are complementary: OrionAudit answers "what changed in this row over time" and `EfAuditWriter` answers "who invoked which command and what happened".

### OrionLock — distributed locks with Postgres advisory backend

Two locking patterns demonstrate OrionLock:

**Sorted-key deadlock prevention in money transfer:**

[`TransferMoneyHandler.cs`](src/Moongazing.OrionShowcase.Application/Accounts/Commands/TransferMoney/TransferMoneyHandler.cs) acquires distributed locks on both source and target account ids. To prevent the classic two-process-cross-deadlock, both locks are acquired in sorted Guid order so that concurrent transfers between the same two accounts always queue rather than deadlock.

**Distributed mutations vs in-memory reader-writer reads:**

Money mutations (deposit, withdraw, transfer) are guarded by the DISTRIBUTED Postgres advisory lock (`IDistributedLock`), which is what keeps balance mutations serialized across multiple processes/replicas. The reader-writer lock (`ISharedExclusiveLock`) is wired over the canonical in-memory provider from `OrionLock.Testing` and is used to demonstrate SHARED-read (GetBalance) vs EXCLUSIVE semantics, plus an additional in-process guard on the mutation path. That in-memory reader-writer provider is single-process / sample-only: it does not span replicas, so it is never the cross-replica safety mechanism. A distributed reader-writer provider (for example Redis) would unify the two in production behind the same abstractions. The canonical in-memory provider also prevents writer starvation via a pending-writer reservation, so a steady reader stream cannot starve a waiting writer.

**Single-instance hosted job:**

[`DailySettlementService.cs`](src/Moongazing.OrionShowcase.Infrastructure/HostedServices/DailySettlementService.cs) is a `BackgroundService` that wakes at 23:55 UTC. It now gates on OrionBeacon leader election (see the OrionBeacon entry above): if this node is not the elected leader, the service logs `Settlement skipped` and goes back to sleep. This is the standard pattern for daily jobs that must run exactly once across N horizontally-scaled API replicas. (OrionLock is still used for the transfer hot path's account locks below.)

```mermaid
flowchart TD
    Start([Replica wakes at 23:55 UTC]) --> Try{OrionBeacon<br/>ILeaderElector.IsLeader?}
    Try -- "not leader<br/>(another replica holds the lease)" --> Skip[Log 'Settlement skipped']
    Skip --> Sleep[Sleep until next run]
    Try -- "leader" --> Run[RunDailySettlement.ExecuteAsync<br/>close interest, EOD reports, ...]
    Run --> Sleep
    Sleep --> Start

    classDef skip fill:#fee2e2,stroke:#991b1b,color:#7f1d1d
    classDef run fill:#dcfce7,stroke:#166534,color:#14532d
    class Skip,Sleep skip
    class Run,Release run
```

Backend: `OrionLock.Postgres` 0.2.3 uses Postgres `pg_try_advisory_lock` with session-scoped semantics — if the holding process crashes, Postgres auto-releases the lock when the session ends.

### OrionKey — Snowflake IDs and idempotency

OrionKey 0.4.1 exposes a process-global static facade rather than a DI service. `OrionKey.Configure(o => o.SnowflakeWorkerId = ...)` runs once at startup. Then `OrionKey.NextSnowflake()` is called wherever a 64-bit time-sortable id is needed.

- [`EfAuditWriter.cs`](src/Moongazing.OrionShowcase.Infrastructure/Audit/EfAuditWriter.cs) uses `OrionKey.NextSnowflake()` for `CommandAuditEntry.Id` so audit rows are naturally time-ordered without an autoincrement collision risk across replicas.
- [`OrionKeyIdempotencyStore.cs`](src/Moongazing.OrionShowcase.Infrastructure/Idempotency/OrionKeyIdempotencyStore.cs) implements Application's `IIdempotencyStore` against an EF-backed `idempotency_records` table. (OrionKey 0.4.1 does not yet expose a built-in idempotency cache — the package focuses on ID generation. The integration here is "use OrionKey for the surrogate id; do the cache with EF".)
- The `IdempotencyBehavior<TRequest, TResponse>` MediatR pipeline behavior calls into `IIdempotencyStore` so any command implementing `IIdempotentCommand` is automatically deduplicated by `IdempotencyKey`.

### OrionPatch — transactional outbox

OrionPatch's `SaveChangesInterceptor` collects events queued via `IOutbox.Enqueue<T>(...)` into an `outbox_messages` table inside the same database transaction as the domain changes. A hosted dispatcher later pushes them to configured sinks.

[`DomainEventOutboxAdapter.cs`](src/Moongazing.OrionShowcase.Infrastructure/Outbox/DomainEventOutboxAdapter.cs) is the small bridge that walks the EF change tracker, finds entities deriving from `AggregateRoot<TId>`, and enqueues each accumulated domain event via reflection on `IOutbox.Enqueue<T>` so the concrete event type reaches `MessageTypeNameResolver`. Three-phase lifecycle (SavingChanges -> SavedChanges -> SaveChangesFailed) preserves at-least-once semantics across save failures.

[`BankingDbContext.OnModelCreating`](src/Moongazing.OrionShowcase.Infrastructure/Persistence/BankingDbContext.cs) calls `ApplyOrionPatchConfiguration(this)` to map the outbox row table. [`InfrastructureServiceCollectionExtensions`](src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs) calls `AddOrionPatch().UseEntityFrameworkCore<BankingDbContext>()` and the DbContext options call `UseOrionPatch(sp)`.

A single `TransferCompleted` event is raised by `Account.RecordTransfer` in the Domain, ends up in `outbox_messages` in the same transaction as the balance debit/credit, and gets dispatched asynchronously by the OrionPatch hosted service. No two-phase commit, no event loss.

### OrionVault — column encryption at rest

Customer PII (NationalId, Email, Phone) is encrypted at the database column level. The DB sees ciphertext; application code sees plaintext.

- [`CustomerConfiguration.cs`](src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs) marks the three columns with `HasAnnotation("OrionVault:Encrypted", true)`. (The typed `.IsEncrypted()` extension only supports `PropertyBuilder<string>` and `PropertyBuilder<byte[]>` directly. We use the underlying annotation for our value-converted `Tckn` type so the OrionVault model customizer picks it up.)
- [`InfrastructureServiceCollectionExtensions.cs`](src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs) wires `AddOrionVault(o => { o.UseStaticKeys(...); }).UseEntityFrameworkCore<BankingDbContext>()` and the DbContext options call `UseOrionVault(sp)`.
- At-rest verification: [`PiiEncryptionTests.cs`](test/Moongazing.OrionShowcase.IntegrationTests/Scenarios/PiiEncryptionTests.cs) reads `national_id` directly from Postgres as raw bytes via Npgsql and confirms the on-disk layout is `[keyId(2 BE) | nonce(12) | tag(16) | ciphertext]` — the 30-byte fixed overhead documented in OrionVault's spec.

## The ten newly integrated Orion packages

Beyond the original six, the showcase now integrates ten more Orion packages across two passes. The first five are cross-cutting infrastructure/auth concerns; the second five are domain- and event-level concerns wired into the account-opening and transfer flows.

### Pass 1 — cross-cutting infrastructure and auth

- **OrionLens** — ambient correlation context. `AddOrionLens()` / `UseOrionLens()` in [`Program.cs`](src/Moongazing.OrionShowcase.Api/Program.cs) mint or echo an `X-Correlation-ID` and propagate it as baggage before anything logs or authenticates. [`CorrelationEnricher.cs`](src/Moongazing.OrionShowcase.Api/Observability/CorrelationEnricher.cs) surfaces the correlation id on every Serilog event.
- **OrionShade** — PII/secret log redaction. [`OrionShadeExtensions.cs`](src/Moongazing.OrionShowcase.Api/Redaction/OrionShadeExtensions.cs) registers an `IRedactor` used at customer-data log sites so TCKN/email/phone never reach the logs in the clear.
- **OrionGrant** — policy-based authorization. [`OrionGrantExtensions.cs`](src/Moongazing.OrionShowcase.Api/Authorization/OrionGrantExtensions.cs) maps JWT roles to the banking permission set in [`BankingPermissions.cs`](src/Moongazing.OrionShowcase.Api/Authorization/BankingPermissions.cs); [`PermissionEndpointFilter.cs`](src/Moongazing.OrionShowcase.Api/Authorization/PermissionEndpointFilter.cs) enforces `RequirePermission(...)` on endpoints such as transfer.
- **OrionLedger** — API-key issuance/verification. [`OrionLedgerExtensions.cs`](src/Moongazing.OrionShowcase.Api/ApiKeys/OrionLedgerExtensions.cs) plus [`ApiKeyAuthMiddleware.cs`](src/Moongazing.OrionShowcase.Api/ApiKeys/ApiKeyAuthMiddleware.cs) establish an `X-Api-Key` principal for the partner endpoints under `Endpoints/Partner/`.
- **OrionOnce** — HTTP idempotency. `AddOrionOnce()` / `UseOrionOnce()` deduplicate mutating POSTs that carry an `Idempotency-Key` header, complementing the command-level `IdempotencyBehavior`.

### Pass 2 — domain and event-level

- **OrionSaga** — in-process saga with compensation. Account opening runs as a three-step saga in [`AccountOpeningSaga.cs`](src/Moongazing.OrionShowcase.Application/Accounts/Sagas/AccountOpeningSaga.cs): `validate-customer` -> `create-account` -> `set-initial-limit`. Each side-effecting step has a compensating action, so if a later step throws (the demo hook `ForceFailureAfterLimit` exercises this), OrionSaga rolls the completed steps back in reverse — the created account is closed and the daily limit is removed. `AddOrionSaga()` is registered in [`ApplicationServiceCollectionExtensions.cs`](src/Moongazing.OrionShowcase.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs); [`OpenAccountHandler.cs`](src/Moongazing.OrionShowcase.Application/Accounts/Commands/OpenAccount/OpenAccountHandler.cs) drives the saga and maps `SagaResult` to the command result.
- **OrionRelay** — signed outbound webhooks. When a transfer completes, [`TransferEndpoint.cs`](src/Moongazing.OrionShowcase.Api/Endpoints/Accounts/TransferEndpoint.cs) dispatches a signed `transfer.completed` webhook through OrionRelay's `IWebhookDispatcher` (HMAC signing + retry policy). [`OrionRelayExtensions.cs`](src/Moongazing.OrionShowcase.Api/Webhooks/OrionRelayExtensions.cs) calls `AddOrionRelay(secret, ...)` with endpoint/secret from the `Relay` config section; when no partner is configured a [`StubWebhookHandler.cs`](src/Moongazing.OrionShowcase.Api/Webhooks/StubWebhookHandler.cs) short-circuits the transport so the app never fails, logging the signed request and the `Orion-Signature` header. Dispatch is best-effort: a partner outage never fails the committed transfer.
- **OrionStream** — Server-Sent Events. `GET /api/accounts/{id}/activity/stream` ([`AccountActivityStreamEndpoint.cs`](src/Moongazing.OrionShowcase.Api/Endpoints/Accounts/AccountActivityStreamEndpoint.cs)) streams per-account activity over SSE via OrionStream's `ISseHub`, bounded by a 15-minute lifetime cap and the client abort token. When a transfer posts, [`AccountActivityPublisher.cs`](src/Moongazing.OrionShowcase.Api/Streaming/AccountActivityPublisher.cs) publishes a `transfer.posted` event to both the source and destination account topics. `AddOrionStream(...)` is registered in [`OrionStreamExtensions.cs`](src/Moongazing.OrionShowcase.Api/Streaming/OrionStreamExtensions.cs).
- **OrionBeacon** — leader election. The daily settlement job is now leader-only: [`DailySettlementService.cs`](src/Moongazing.OrionShowcase.Infrastructure/HostedServices/DailySettlementService.cs) only runs while `ILeaderElector.IsLeader`, so across N replicas the job fires on exactly one node. `AddOrionBeacon(...)` in [`InfrastructureServiceCollectionExtensions.cs`](src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs) uses the in-memory lease store (single-node dev) and [`LoggingLeadershipObserver.cs`](src/Moongazing.OrionShowcase.Infrastructure/HostedServices/LoggingLeadershipObserver.cs) logs each leadership transition. Configuration lives under the `Beacon` section.
- **Orion.Abstractions** — foundational Orion contracts. It arrives transitively via the event/domain-level packages; the Infrastructure project references it directly for clarity and [`SystemClock.cs`](src/Moongazing.OrionShowcase.Infrastructure/Time/SystemClock.cs) implements its `IOrionClock` alongside the domain `IClock`, so the app and the Orion packages read wall-clock time from one source.

## Where we deliberately use packages from outside the Orion family

Honesty section. Not every component in the stack has an Orion equivalent, and the ones below are industry standards we use without apology:

- **MediatR** for CQRS — no Orion equivalent. MediatR is the de-facto standard. (Multi-step workflows with compensation now use OrionSaga; MediatR still carries the request/response dispatch.)
- **OpenTelemetry SDK + OTLP exporter** for traces and metrics — open standard, exports Orion ActivitySources/Meters to Jaeger.
- **Microsoft.AspNetCore.Authentication.JwtBearer** for JWT validation — in-box, no Orion equivalent.
- **Microsoft.AspNetCore.RateLimiting** for per-endpoint rate limits — OrionGuard.AspNetCore 6.4.2 is a validation + ProblemDetails package, not a rate limiter. The policy names in `appsettings.json` are prefixed with `OrionGuard:Policies` for narrative consistency, but the actual middleware is ASP.NET Core's in-box one.
- **Swashbuckle.AspNetCore** for Swagger UI / OpenAPI generation.
- **Serilog** + Seq sink for structured logging.
- **EF Core 9 + Npgsql** for persistence.
- **Testcontainers.PostgreSql** for integration test isolation.

If a future Orion package ships any of the above (a saga library, an OIDC integration, a tracing exporter), the showcase swaps to it.

## Where each Orion package wires in (component map)

```mermaid
flowchart LR
    subgraph Api["Api layer"]
        Prog[Program.cs]
        Endp[Endpoints]
        Jwt[JwtBearer]
    end

    subgraph App["Application layer"]
        Cmd[Commands +<br/>Handlers]
        Pipe[MediatR<br/>pipeline]
        Val[FluentStyleValidators]
    end

    subgraph Infra["Infrastructure layer"]
        Ctx[BankingDbContext]
        Repo[Repositories]
        Adp[DomainEventOutboxAdapter]
        Hsv[DailySettlementService]
        Aud[EfAuditWriter]
        Ids[OrionKeyIdempotencyStore]
    end

    subgraph Dom["Domain layer"]
        Agg[Account +<br/>Customer aggregates]
        Vo[Value objects]
    end

    OG[OrionGuard<br/>+ AspNetCore]:::orion
    OA[OrionAudit]:::orion
    OL[OrionLock.Postgres]:::orion
    OK[OrionKey]:::orion
    OP[OrionPatch.EFCore]:::orion
    OV[OrionVault.EFCore]:::orion

    Prog -.UseOrionGuardValidation.-> OG
    Val -.FluentStyleValidator.-> OG
    Pipe -.ValidationBehavior.-> OG
    Agg -.Ensure/Contract/FluentGuard.-> OG

    Ctx -.UseOrionAudit / Audit entity diff.-> OA
    Aud -.command-level audit rows.-> OA

    Cmd -.IDistributedLock.AcquireAsync.-> OL
    Hsv -.TryAcquireAsync settlement:daily.-> OL

    Aud -.NextSnowflake.-> OK
    Ids -.snowflake IDs +<br/>EF-backed cache.-> OK

    Adp -.IOutbox.Enqueue domain events.-> OP
    Ctx -.UseOrionPatch interceptor.-> OP

    Ctx -.UseOrionVault interceptor.-> OV
    Repo -.ciphertext on disk,<br/>plaintext to handlers.-> OV

    classDef orion fill:#e0e7ff,stroke:#312e81,color:#1e1b4b,stroke-width:2px
```

Every arrow is a real line of code in this repo. Click through to the file links in the previous section to see them.

## OpenTelemetry trace anatomy

A single `POST /api/accounts/{fromId}/transfer` request produces this span tree (visible in Jaeger).

```mermaid
flowchart TB
    Http["HTTP POST /api/accounts/{fromId}/transfer"]
    Filter[orionguard.endpoint_filter.validate]
    JwtSpan[Authentication.JwtBearer]
    Med[MediatR.Send TransferMoneyCommand]
    Val[ValidationBehavior]
    Log[LoggingBehavior]
    Ide[IdempotencyBehavior]
    IdeDb[DB SELECT idempotency_records]
    AudBeg[AuditBehavior begin]
    Hand[TransferMoneyHandler]
    LkA[orionlock.acquire account:0001]
    LkB[orionlock.acquire account:0002]
    SelAcc[DB SELECT accounts<br/>+ OrionVault decrypt]
    InsTx[DB INSERT transactions x2]
    UpdAcc[DB UPDATE accounts SET balance x2]
    Outbox[DB INSERT outbox_messages<br/>OrionPatch SaveChangesInterceptor]
    Aud[DB INSERT command_audit_entries]
    OAudit[DB INSERT OrionAudit_Log<br/>RFC 6902 JSON Patch]
    RelA[orionlock.release x2]
    AudEnd[AuditBehavior end]

    Http --> Filter --> JwtSpan --> Med
    Med --> Val --> Log --> Ide --> IdeDb
    Ide --> AudBeg --> Hand
    Hand --> LkA --> LkB --> SelAcc --> InsTx --> UpdAcc --> Outbox --> Aud --> OAudit --> RelA
    RelA --> AudEnd
```

Activity sources exported: `Moongazing.OrionGuard`, `OrionAudit`, `Moongazing.OrionLock`, `Moongazing.OrionPatch`, `Moongazing.OrionVault`. OrionKey 0.4.1 currently exposes only a Meter, no ActivitySource — `orionkey.snowflake.generated` counter is visible in Jaeger's metrics view.

## Tech stack

- .NET 8, ASP.NET Core 8 Minimal API
- EF Core 9.0.1 + Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4
- PostgreSQL 16 (alpine)
- MediatR 12.4.1
- OrionGuard 6.4.2, OrionGuard.AspNetCore 6.4.2
- OrionAudit 0.6.2
- OrionLock 0.2.3, OrionLock.Postgres 0.2.3
- OrionKey 0.4.1
- OrionPatch 0.1.1, OrionPatch.EntityFrameworkCore 0.1.1
- OrionVault 0.1.2, OrionVault.EntityFrameworkCore 0.1.2
- OpenTelemetry 1.15.3 (Hosting + OTLP exporter + AspNetCore + EFCore + Npgsql)
- Serilog.AspNetCore + Serilog.Sinks.Seq
- Swashbuckle.AspNetCore
- xUnit + FluentAssertions + Testcontainers.PostgreSql

## Known limitations

- Authentication is a dev-grade JWT issuer with hardcoded `demo`/`demo` user. Real deployments swap in OIDC (Keycloak, IdentityServer, Auth0).
- Single Account aggregate. Loan, KYC, multi-tenant slices are roadmap items.
- API only — no Blazor / SPA frontend.
- No Kubernetes manifests. Docker compose ships; Helm chart is roadmap.
- OrionKey 0.4.1 exposes a process-global static facade for Snowflake configuration. Multi-tenant or per-host worker-id rotation is roadmap material in OrionKey itself.

## Benchmarks

See [benchmarks.md](benchmarks.md). OrionShowcase is an application, not a library, so per-package microbenchmarks live in each Orion repo (linked from there). End-to-end transfer-flow throughput benchmarks against the docker stack are planned for v0.2.

## Roadmap

See [ROADMAP.md](ROADMAP.md). Highlights:

- v0.2: Loan aggregate, cross-aggregate saga via outbox
- v0.3: Multi-tenant slice with OrionVault per-tenant key partitioning
- v0.4: gRPC variant (Moongazing.OrionGuard.Grpc demo)
- v1.0: OIDC integration, Blazor admin frontend, Helm chart, OrionFlow saga (when it ships)

## Family

| Package | Role |
|---|---|
| [Moongazing.OrionGuard](https://github.com/tunahanaliozturk/OrionGuard) | Guard clauses, fluent validation, ProblemDetails |
| [Moongazing.OrionAudit](https://github.com/tunahanaliozturk/OrionAudit) | Automatic entity-diff capture (JSON Patch) |
| [Moongazing.OrionLock](https://github.com/tunahanaliozturk/OrionLock) | Distributed locks (Postgres/SqlServer/Redis backends) |
| [Moongazing.OrionKey](https://github.com/tunahanaliozturk/OrionKey) | Snowflake IDs and GUID v7 generators |
| [Moongazing.OrionPatch](https://github.com/tunahanaliozturk/OrionPatch) | Transactional outbox for EF Core |
| [Moongazing.OrionVault](https://github.com/tunahanaliozturk/OrionVault) | Column encryption at rest (AES-256-GCM) |
| [Moongazing.OrionLens](https://github.com/tunahanaliozturk/OrionLens) | Ambient correlation context + propagation |
| [Moongazing.OrionShade](https://github.com/tunahanaliozturk/OrionShade) | PII/secret log redaction |
| [Moongazing.OrionGrant](https://github.com/tunahanaliozturk/OrionGrant) | Policy-based authorization |
| [Moongazing.OrionLedger](https://github.com/tunahanaliozturk/OrionLedger) | API-key issuance and verification |
| [Moongazing.OrionOnce](https://github.com/tunahanaliozturk/OrionOnce) | HTTP idempotency for mutating requests |
| [Moongazing.OrionSaga](https://github.com/tunahanaliozturk/OrionSaga) | In-process saga with compensation |
| [Moongazing.OrionRelay](https://github.com/tunahanaliozturk/OrionRelay) | Signed outbound webhooks |
| [Moongazing.OrionStream](https://github.com/tunahanaliozturk/OrionStream) | Server-Sent Events hub |
| [Moongazing.OrionBeacon](https://github.com/tunahanaliozturk/OrionBeacon) | Leader election for single-instance jobs |
| [Moongazing.Orion.Abstractions](https://github.com/tunahanaliozturk/Orion.Abstractions) | Shared Orion contracts (IOrionClock, instrumentation) |

## License

MIT. See [LICENSE](LICENSE).

## Contributing

Issues and pull requests welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) before opening one.
