# OrionShowcase Benchmarks

> **Status: pending v0.2.0.** OrionShowcase is an application, not a library. The per-package microbenchmarks live in each Orion repo (see the links at the bottom). What lands here in v0.2 is the end-to-end workload story: how many transfer requests per second the full stack handles, and what each Orion package contributes to that number.

## Per-package microbenchmarks (already published)

Each Orion library ships its own BenchmarkDotNet harness and a `benchmarks.md` at its repo root. Those are the right place to look for "how fast is OrionGuard's email validator" or "what does the OrionPatch interceptor cost per SaveChanges". They are not duplicated here.

- [OrionGuard benchmarks](https://github.com/tunahanaliozturk/OrionGuard/blob/master/benchmarks.md) - null checks, email validation, generated regex, security guards, domain primitives.
- [OrionAudit benchmarks](https://github.com/tunahanaliozturk/OrionAudit/blob/master/benchmarks.md) - snapshot build, JSON Patch compute / apply, SaveChanges overhead, time-travel reconstruction.
- [OrionLock benchmarks](https://github.com/tunahanaliozturk/OrionLock/blob/main/benchmarks.md) - uncontended in-memory abstraction cost, plus the backend scenarios queued for v0.2.
- [OrionKey benchmarks](https://github.com/tunahanaliozturk/OrionKey/blob/main/benchmarks.md) - per-strategy id generation throughput across Snowflake, ULID, UUIDv7, NanoId, CUID2, KSUID, ObjectId, SequentialGuid.
- [OrionPatch benchmarks](https://github.com/tunahanaliozturk/OrionPatch/blob/main/benchmarks.md) - interceptor overhead, dispatcher claim batch, end-to-end enqueue to sink latency (planned for v0.2).
- [OrionVault benchmarks](https://github.com/tunahanaliozturk/OrionVault/blob/main/benchmarks.md) - encrypt / decrypt throughput, value converter overhead, key lookup contention (planned for v0.2).

## End-to-end workload scenarios on the roadmap

These are the things only the integrated stack can answer. They land with v0.2 alongside the Loan aggregate and the docker-compose driver script.

- **Steady-state transfer throughput.** k6 or Bombardier driving `POST /api/accounts/{from}/transfer` against the full stack (API, Postgres, Jaeger, Seq, OutboxDispatcher). Expected metric: requests per second sustained at p99 < 200 ms, with the per-Orion-package span contributions broken out from Jaeger.
- **Cold start to first 200.** Wall-clock from `docker compose up` to the first successful login and transfer. Expected metric: seconds, broken down into container start, migrations, JWT key generation, first OrionGuard validator JIT, first OrionAudit interceptor invocation.
- **OutboxDispatcher lag under load.** `orionpatch.outbox.depth` over time while the workload above runs. Expected metric: p99 enqueue-to-dispatch latency in milliseconds.
- **DailySettlementService contention.** Three API replicas all wake at 23:55 UTC. Expected metric: exactly one replica wins the OrionLock advisory lock; the other two log skipped within 50 ms.
- **PII encryption overhead in real traffic.** Same transfer workload with OrionVault enabled vs. disabled (a configuration switch only, no schema change). Expected metric: the percentage of total request latency attributable to ValueConverter encrypt / decrypt round-trips at the customer-join read path.
- **Audit-log growth rate.** `OrionAudit_Log` row count per minute under the transfer workload, sync mode vs. `UseAsyncCapture`. Expected metric: rows per second, queue depth gauge, and read-after-write lag in async mode.

## Why not yet?

OrionShowcase v0.1 was scoped to land a complete, working, observable integration of all six Orion packages with full integration tests (Testcontainers Postgres, end-to-end register / open / transfer plus PII at-rest verification). The single integrated trace in Jaeger is the central artifact this repo exists to provide. A repeatable end-to-end load-test harness on top of that requires the docker stack driver, a workload generator, and a baseline Postgres tuning pass, none of which are v0.1 work.

If you have run the showcase against your own infrastructure and have numbers you would share, open an issue with the `benchmark` label and we will write them up here with attribution.

## How it will be run

When v0.2 ships, the script will look roughly like:

```bash
cd <repo-root>
docker compose -f deploy/docker-compose.yml up -d
./scripts/seed.sh                       # customers + accounts + balances
./scripts/run-workload.sh transfer 5m   # k6 driving /transfer for 5 minutes
./scripts/collect-metrics.sh            # pull Jaeger + Prometheus + Seq, write report
```

Results land under `bench-results/<timestamp>/` and a summary is committed back to this file with each release.

## Comparison baselines

The honest baseline for an end-to-end showcase is the same workload running against an equivalent "no-Orion" implementation. We will report side by side:

- **Baseline.** Hand-rolled validation (no OrionGuard), no audit trail, no transactional outbox, no column encryption, single-instance daily job. The "what does the integration cost" denominator.
- **OrionShowcase as shipped.** Full stack with all six packages wired in. The "what does the integration buy you" numerator: every feature in the baseline list plus structured validation, automatic audit, transactional event publication, PII at rest, and contention-free settlement.

The point is to be honest about both sides. The integrated stack is slower per-request than a stripped-down baseline by construction, because it does more work. The benchmark answers "how much more", not "is it faster".
