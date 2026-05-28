# OrionShowcase Roadmap

OrionShowcase tracks the Moongazing.Orion family's growth. As new Orion packages ship or existing ones gain features, the showcase picks them up so readers always see the latest integrated story.

## v0.1.0 — 2026-Q2 (current)

Single Account aggregate, 10 endpoints, all six existing Orion packages integrated end-to-end with Postgres + Seq + Jaeger docker stack. Domain-layer guards (Ensure / FastGuard / FluentGuard / Contract) demonstrate OrionGuard's full Core surface. EF Core 9 migration applied on startup.

## v0.2 — Loan aggregate

Loan application workflow: customer applies, KYC step, approval, disbursement. Demonstrates cross-aggregate coordination via OrionPatch outbox events (a saga-shaped flow without yet pulling in a dedicated saga library).

## v0.3 — Multi-tenant slice

Per-tenant OrionVault key partitioning (requires OrionVault v0.4 KMS support). Tenant-scoped OrionGuard rate-limit policies. Tenant context flows from JWT claim to repository query filter.

## v0.4 — gRPC variant

Add gRPC endpoints alongside REST. Demonstrates Moongazing.OrionGuard.Grpc validation interceptor and OpenTelemetry gRPC instrumentation alongside the existing HTTP exports.

## v1.0 — Production-shaped

- OIDC integration (Keycloak or IdentityServer) replacing the dev-grade JWT issuer
- Blazor admin frontend showing OrionGuard.Blazor validation messages
- Kubernetes manifests + Helm chart
- Real OrionFlow saga (when OrionFlow ships)
- Production-hardened benchmarks against representative workloads

## How decisions get made

Anything that adds value to the integrated narrative is a candidate. Anything that bloats the codebase without showing off an Orion feature gets rejected. The repo's job is to be the most convincing single-glance argument for adopting the Orion family, not to be a generic banking sample.
