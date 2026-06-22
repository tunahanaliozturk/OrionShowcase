# OrionShowcase Roadmap

OrionShowcase is the reference banking sample for the Moongazing.Orion family. This roadmap covers the sample itself: what it demonstrates today and what it should demonstrate next. It does not plan features for the individual Orion packages, which have their own repositories and roadmaps. As packages ship new capabilities, the sample picks them up so readers see the latest integrated story against the latest published versions.

## Current state (2026-06)

The whole Orion family is integrated end-to-end. A single transfer request fans out through OrionGuard validation, OrionKey id generation, OrionLock account locks, OrionAudit entity-diff capture, OrionPatch outbox, OrionVault PII decryption, and the cross-cutting and event-level packages (OrionLens, OrionShade, OrionGrant, OrionLedger, OrionOnce, OrionSaga, OrionRelay, OrionStream, OrionBeacon, Orion.Abstractions).

The end-to-end integration suite now runs in CI against a real PostgreSQL 16 and is green (13 tests across 8 scenario classes). Before this, the integration tests existed but had never actually executed in CI. Standing them up surfaced and fixed several latent bugs:

- A host-startup DI cycle: the OrionBeacon leader-election hosted service blocked synchronous host start under WebApplicationFactory. The test host now drops the application background services and keeps only the framework web host.
- Test database isolation: each test class provisions its own `banking_<guid>` database on the shared CI Postgres, so classes that run together cannot see each other's rows. Locally, an unset `BANKING_TEST_POSTGRES` falls back to a throwaway Testcontainers Postgres.
- Encrypted columns needed `bytea`: the OrionVault envelope is raw bytes, so the PII columns moved to `bytea` (migration `EncryptedColumnsToBytea`) and the at-rest test reads them straight off the column.
- Unique transaction ids and a distinct Money per transaction, so concurrent and repeated scenarios do not collide on a shared id or value object.

What the suite demonstrates today, end-to-end against Postgres:

- Register a customer, open an account, transfer between two accounts; balances update correctly.
- PII encryption at rest: `national_id` is stored as an OrionVault envelope (`[keyId:2 BE][nonce:12][tag:16][body]`) and round-trips back to plaintext through the app's DbContext.
- Async validation plus searchable-encrypted lookup: a duplicate national id is rejected by the OrionGuard async uniqueness rule, which resolves the duplicate through the OrionVault deterministic blind index without decrypting any row.
- SSE resume: a reconnecting subscriber that sends `Last-Event-ID` replays only the events published after that cursor; an unknown id falls back to from-now with no replay.
- Idempotent transfers, reader-writer account locks, and saga step timeouts (covered in the Application suite, exercised by the same wiring the host uses).
- Webhook and outbox dead-lettering plus outbox archival: an exhausted row is dead-lettered exactly once and removed from the hot outbox, a replayed terminal path is conflict-tolerant, and dispatched rows past the retention window are archived while pending and dead-lettered rows are left untouched.

CI also builds the solution and runs the unit suites (Domain and Application) on .NET 8, and publishes the API image to GHCR on release.

## v0.2 - one-command run and developer onboarding (target 2026-Q3)

- Add a `docker compose` stack (api, postgres, seq, jaeger) so the README's "five-minute experience" is real and reproducible from a clean checkout. The README already describes `docker/compose.yaml`; this milestone ships the file and a healthcheck-gated startup.
- A one-command run note and a smoke script that logs in, registers, opens two accounts, and runs a transfer, so a reader can confirm the stack end-to-end without clicking through Swagger.
- Pin the compose images and document the exact ports and credentials used in the walkthrough.

## v0.3 - observability walkthrough (target 2026-Q4)

- A documented trace walkthrough: drive one transfer, then follow the single trace id across Jaeger (spans from every Orion ActivitySource alongside HTTP and EF Core) and Seq (the structured log lines for the same trace). Capture the expected span tree so a reader knows what "correct" looks like.
- An observability section that maps each Orion package to the spans, meters, and log fields it contributes, so the trace is self-explanatory.
- Add an integration check that asserts the expected ActivitySources are registered and emitting, so the observability story cannot silently regress.

## v0.4 - broader end-to-end coverage (target 2027-Q1)

- More end-to-end scenarios against Postgres, promoting capabilities currently proven only in the Application suite (idempotent replay returning the same result, reader-writer read-vs-mutation ordering, saga timeout rollback) into full host-level integration tests.
- A webhook end-to-end scenario that drives the real dispatcher against a stub partner endpoint and asserts the signed delivery and the dead-letter path.
- Demonstrate the remaining Orion capabilities not yet shown end-to-end: OrionBeacon leader election across more than one host instance, OrionLedger API-key rotation and bulk revoke over HTTP, and OrionGrant policy enforcement on protected endpoints.

## v0.5 - load and performance scenario (target 2027-Q2)

- A repeatable load scenario against the compose stack that drives concurrent transfers and reports end-to-end throughput and latency, with the lock and outbox paths under contention. This is the end-to-end transfer-flow benchmark the README and benchmarks.md currently defer.
- Document the observed numbers and the hardware they were taken on, and wire the scenario so it can be re-run on demand rather than hand-assembled.

## Ongoing - keep current on the latest packages

- Track each Orion package's latest published version and bump the sample when a release lands, re-running the green integration suite as the gate.
- Keep the README tech-stack table and the package-version list in `Directory.Packages.props` in sync, since they have drifted before.
- When a new Orion package ships a capability the sample does not yet show, add the smallest real banking scenario that exercises it end-to-end.

## How decisions get made

Anything that adds value to the integrated narrative is a candidate. Anything that bloats the codebase without showing off an Orion capability gets rejected. The repo's job is to be the most convincing single-glance argument for adopting the Orion family, backed by a green end-to-end suite, not to be a generic banking sample.
