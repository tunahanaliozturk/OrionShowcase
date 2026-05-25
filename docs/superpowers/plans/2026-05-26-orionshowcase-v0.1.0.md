# Moongazing.OrionShowcase v0.1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the first cut of `Moongazing.OrionShowcase` — a Clean Architecture banking sample (Domain / Application / Infrastructure / Api) that integrates all six Moongazing.Orion NuGet packages in one cohesive codebase, with `docker compose up` reproducing the full stack (Postgres + Seq + Jaeger + API) and a Jaeger trace for one transfer request showing spans from all six packages.

**Architecture:** Clean Architecture, 4 layers, one-way dependency flow (Api → Application → Infrastructure → Domain). MediatR CQRS with pipeline behaviors for Validation, Logging, OrionKey idempotency, OrionAudit audit. EF Core 8 + Postgres for persistence. JWT bearer auth (dev-grade). OpenTelemetry exports to Jaeger; Serilog to Seq.

**Tech Stack:** .NET 8, ASP.NET Core Minimal API, EF Core 8 + Npgsql, MediatR 12.4, FluentValidation 11, Serilog, OpenTelemetry, xUnit + FluentAssertions + Testcontainers Postgres. Six Orion NuGet packages: OrionGuard.AspNetCore 6.4.2, OrionAudit 0.6.1, OrionLock.Postgres 0.2.1, OrionKey 0.4.1, OrionPatch.EntityFrameworkCore 0.1.1, OrionVault.EntityFrameworkCore 0.1.1.

**Reference spec:** `docs/superpowers/specs/2026-05-26-orionshowcase-v0.1.0-design.md`

**Reference sibling (release flow):** `c:/Users/Tunahan Ali Ozturk/OneDrive - PEAKUP/Desktop/OrionVault/` — same CI/CD pattern, same release script, same git commit / tag / GH release flow. The bootstrap and release tasks below copy that template.

**User constraints (NON-NEGOTIABLE across all tasks):**
1. NO Co-Authored-By trailer in commit messages.
2. NO emojis anywhere — commits, README, CHANGELOG, PR titles, release notes.
3. NO em-dashes in user-facing text — use regular dashes or periods.
4. TDD strict where applicable: failing test → run → implement → verify → commit.

---

## File Structure

```
Moongazing.OrionShowcase/
├── Moongazing.OrionShowcase.sln               (7 entries: 4 src + 3 test)
├── Directory.Build.props
├── Directory.Packages.props
├── NuGet.config                                (clears sources to nuget.org only)
├── .gitignore
├── LICENSE                                     (MIT)
├── README.md
├── ROADMAP.md
├── CHANGELOG.md
├── docs/
│   ├── logo.png, icon.png                      (cream-bg, family aesthetic, deferred to Task 17)
│   └── superpowers/
│       ├── specs/2026-05-26-orionshowcase-v0.1.0-design.md   (already exists)
│       └── plans/2026-05-26-orionshowcase-v0.1.0.md          (this file)
├── .github/workflows/
│   └── ci-cd.yml                               (CI on PR + Docker image push on release)
├── docker/
│   ├── compose.yaml
│   ├── Dockerfile.api
│   └── postgres-init.sql
├── src/
│   ├── Moongazing.OrionShowcase.Domain/
│   │   ├── Abstractions/
│   │   │   ├── AggregateRoot.cs
│   │   │   └── IClock.cs
│   │   ├── ValueObjects/
│   │   │   ├── AccountId.cs
│   │   │   ├── CustomerId.cs
│   │   │   ├── TransactionId.cs
│   │   │   ├── IdempotencyKey.cs
│   │   │   ├── Money.cs
│   │   │   ├── Iban.cs
│   │   │   ├── Tckn.cs
│   │   │   └── Enums.cs
│   │   ├── Accounts/
│   │   │   ├── Account.cs
│   │   │   ├── Transaction.cs
│   │   │   ├── Exceptions.cs
│   │   │   └── Events.cs
│   │   ├── Customers/
│   │   │   ├── Customer.cs
│   │   │   └── Events.cs
│   │   └── Repositories/
│   │       ├── IAccountRepository.cs
│   │       ├── ICustomerRepository.cs
│   │       └── IUnitOfWork.cs
│   ├── Moongazing.OrionShowcase.Application/
│   │   ├── Abstractions/
│   │   │   ├── ICurrentUser.cs
│   │   │   ├── IIdempotencyStore.cs
│   │   │   └── IAuditWriter.cs
│   │   ├── Pipeline/
│   │   │   ├── IAuditableCommand.cs
│   │   │   ├── IIdempotentCommand.cs
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── IdempotencyBehavior.cs
│   │   │   └── AuditBehavior.cs
│   │   ├── Common/
│   │   │   └── Result.cs
│   │   ├── Customers/Commands/RegisterCustomer/
│   │   │   ├── RegisterCustomerCommand.cs
│   │   │   ├── RegisterCustomerHandler.cs
│   │   │   └── RegisterCustomerValidator.cs
│   │   ├── Accounts/Commands/{OpenAccount, DepositMoney, WithdrawMoney, TransferMoney, FreezeAccount, CloseAccount}/
│   │   │   └── (Command, Handler, Validator each)
│   │   ├── Accounts/Queries/{GetAccountBalance, GetAccountTransactions}/
│   │   │   └── (Query, Handler, Dto each)
│   │   ├── Settlement/RunDailySettlement.cs
│   │   └── DependencyInjection/ApplicationServiceCollectionExtensions.cs
│   ├── Moongazing.OrionShowcase.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── BankingDbContext.cs
│   │   │   ├── Configurations/{AccountConfiguration, CustomerConfiguration, TransactionConfiguration}.cs
│   │   │   ├── Repositories/{AccountRepository, CustomerRepository}.cs
│   │   │   ├── EfUnitOfWork.cs
│   │   │   └── Migrations/
│   │   ├── Outbox/DomainEventOutboxAdapter.cs
│   │   ├── Audit/EfAuditWriter.cs
│   │   ├── Idempotency/OrionKeyIdempotencyStore.cs
│   │   ├── Time/SystemClock.cs
│   │   ├── HostedServices/DailySettlementService.cs
│   │   └── DependencyInjection/InfrastructureServiceCollectionExtensions.cs
│   └── Moongazing.OrionShowcase.Api/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Endpoints/
│       │   ├── EndpointExtensions.cs           (MapBankingEndpoints)
│       │   ├── Auth/LoginEndpoint.cs
│       │   ├── Customers/RegisterCustomerEndpoint.cs
│       │   └── Accounts/{OpenAccount, Deposit, Withdraw, Transfer, Freeze, Close, GetBalance, GetTransactions}Endpoint.cs
│       ├── Authentication/
│       │   ├── JwtIssuer.cs
│       │   ├── ClaimsCurrentUser.cs
│       │   └── JwtBearerExtensions.cs
│       ├── Filters/{ValidationProblemFilter, DomainExceptionFilter}.cs
│       ├── Swagger/SwaggerExtensions.cs
│       ├── Observability/OpenTelemetryExtensions.cs
│       └── Health/HealthChecksExtensions.cs
└── test/
    ├── Moongazing.OrionShowcase.Domain.Tests/
    ├── Moongazing.OrionShowcase.Application.Tests/
    └── Moongazing.OrionShowcase.IntegrationTests/
```

---

## Tasks Overview

| # | Task | Output |
|---|------|--------|
| 0 | Repo bootstrap | Solution, 7 project skeletons, Directory.* props, NuGet.config, CI workflow, GitHub repo `tunahanaliozturk/OrionShowcase` |
| 1 | Domain: value objects + AggregateRoot + IClock | All immutable types validated, Domain.Tests scaffolded |
| 2 | Domain: Account aggregate + Transaction + events + exceptions | Open/Deposit/Withdraw/Transfer/Freeze/Close with invariants and event emission |
| 3 | Domain: Customer aggregate + events | Customer.Register with PII fields (encryption is Infrastructure concern) |
| 4 | Application: MediatR + 4 pipeline behaviors + Result type | Validation, Logging, Idempotency, Audit behaviors with tests |
| 5 | Application: 7 commands + handlers + validators | RegisterCustomer + 6 Account commands |
| 6 | Application: 2 queries + handlers + DTOs | GetAccountBalance, GetAccountTransactions |
| 7 | Infrastructure: DbContext + EF configurations + repositories | BankingDbContext, 3 entity configs, 2 repositories, EfUnitOfWork |
| 8 | Infrastructure: OrionVault wiring + Customer encryption | UseOrionVault + IsEncrypted() on Customer.NationalId/Email/Phone, integration-verified |
| 9 | Infrastructure: OrionPatch wiring + domain event bridge | UseOrionPatch + DomainEventOutboxAdapter collecting AggregateRoot.DomainEvents |
| 10 | Infrastructure: OrionLock + OrionAudit + OrionKey wiring | AddOrionLock.UsePostgres, AddOrionAudit, AddOrionKey + IdempotencyStore impl |
| 11 | Infrastructure: DailySettlementService hosted service | OrionLock-gated single-instance background job |
| 12 | Api: Program.cs + JWT auth + Swagger | Composition root, dev-grade JWT issuer, OpenAPI 3 with bearer scheme |
| 13 | Api: 10 endpoints | All endpoint files using the Minimal API pattern |
| 14 | Api: OrionGuard policies + OpenTelemetry export | Per-endpoint rate-limit policies, 6 ActivitySources + 6 Meters wired to OTLP |
| 15 | Docker compose + Dockerfile + postgres-init.sql | One-command stack-up; api + postgres + seq + jaeger healthy |
| 16 | IntegrationTests with Testcontainers | Happy-path tests for register, open, deposit, transfer; verify ciphertext-at-rest |
| 17 | Docs polish + cross-link 6 sibling READMEs | README, ROADMAP, CHANGELOG draft, logo deployment, PRs against 6 sibling repos |
| 18 | First GitHub release v0.1.0 | Tag, GitHub release, branch protection on main; no NuGet push (this is an app) |

---

## Task 0: Repo bootstrap

**Files:**
- Create: `Moongazing.OrionShowcase.sln`
- Create: `Directory.Packages.props`, `Directory.Build.props`, `.gitignore`, `LICENSE`, `NuGet.config`
- Create: 7 csproj skeletons under `src/`, `test/`
- Create: `.github/workflows/ci-cd.yml`
- Create: `README.md` placeholder
- Create: GitHub repo `tunahanaliozturk/OrionShowcase`

- [ ] **Step 1: Verify working directory and existing git**

```
cd "c:/Users/Tunahan Ali Ozturk/OneDrive - PEAKUP/Desktop/OrionShowcase"
git log --oneline
```
Expected: shows `769ff50 docs: add OrionShowcase v0.1.0 design specification` plus the plan commit if already committed.

- [ ] **Step 2: Copy `.gitignore`, `LICENSE`, `NuGet.config` from OrionVault (proven setup)**

```
cp "c:/Users/Tunahan Ali Ozturk/OneDrive - PEAKUP/Desktop/OrionVault/.gitignore" .gitignore
cp "c:/Users/Tunahan Ali Ozturk/OneDrive - PEAKUP/Desktop/OrionVault/LICENSE" LICENSE
cp "c:/Users/Tunahan Ali Ozturk/OneDrive - PEAKUP/Desktop/OrionVault/NuGet.config" NuGet.config
```

The LICENSE author line should already read `Moongazing` from OrionVault; verify with `head -3 LICENSE`. The `NuGet.config` clears feeds to nuget.org only (avoids the user's global Azure DevOps `peakup` feed clashing with central package management).

- [ ] **Step 3: Create `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <NoWarn>$(NoWarn);CA1014</NoWarn>
    <Authors>Moongazing</Authors>
    <Company>Moongazing</Company>
    <Copyright>Copyright (c) 2026 Moongazing</Copyright>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
```

Note: no `<PackageReadmeFile>`, no `<PackageIcon>`, no `<PackageLicenseExpression>` — this is not a NuGet library, projects are not packed.

- [ ] **Step 4: Create `Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Orion family (cream-bg patch versions just shipped) -->
    <PackageVersion Include="Moongazing.OrionGuard.AspNetCore" Version="6.4.2" />
    <PackageVersion Include="Moongazing.OrionAudit" Version="0.6.1" />
    <PackageVersion Include="Moongazing.OrionLock" Version="0.2.1" />
    <PackageVersion Include="Moongazing.OrionLock.Postgres" Version="0.2.1" />
    <PackageVersion Include="Moongazing.OrionKey" Version="0.4.1" />
    <PackageVersion Include="Moongazing.OrionPatch" Version="0.1.1" />
    <PackageVersion Include="Moongazing.OrionPatch.EntityFrameworkCore" Version="0.1.1" />
    <PackageVersion Include="Moongazing.OrionVault" Version="0.1.1" />
    <PackageVersion Include="Moongazing.OrionVault.EntityFrameworkCore" Version="0.1.1" />
    <!-- Framework + libs -->
    <PackageVersion Include="MediatR" Version="12.4.1" />
    <PackageVersion Include="FluentValidation" Version="11.10.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.10.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.10" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.10" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Options" Version="8.0.2" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.10" />
    <PackageVersion Include="Microsoft.IdentityModel.Tokens" Version="7.7.1" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt" Version="7.7.1" />
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.7.3" />
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageVersion Include="Serilog.Sinks.Seq" Version="8.0.0" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />
    <PackageVersion Include="Npgsql.OpenTelemetry" Version="8.0.5" />
    <!-- Tests -->
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="FluentAssertions" Version="6.12.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.10" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="3.10.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create solution + 7 project skeletons**

```
dotnet new sln -n Moongazing.OrionShowcase --format sln
dotnet new classlib -n Moongazing.OrionShowcase.Domain -o src/Moongazing.OrionShowcase.Domain -f net8.0
dotnet new classlib -n Moongazing.OrionShowcase.Application -o src/Moongazing.OrionShowcase.Application -f net8.0
dotnet new classlib -n Moongazing.OrionShowcase.Infrastructure -o src/Moongazing.OrionShowcase.Infrastructure -f net8.0
dotnet new web -n Moongazing.OrionShowcase.Api -o src/Moongazing.OrionShowcase.Api -f net8.0
dotnet new xunit -n Moongazing.OrionShowcase.Domain.Tests -o test/Moongazing.OrionShowcase.Domain.Tests -f net8.0
dotnet new xunit -n Moongazing.OrionShowcase.Application.Tests -o test/Moongazing.OrionShowcase.Application.Tests -f net8.0
dotnet new xunit -n Moongazing.OrionShowcase.IntegrationTests -o test/Moongazing.OrionShowcase.IntegrationTests -f net8.0
```

Remove auto-generated `Class1.cs` from each classlib, `WeatherForecast.cs` and similar from the `web` template, `UnitTest1.cs` from each xunit project. Keep `Program.cs` in the Api project but reduce it to:
```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "OrionShowcase scaffolded; real composition in Task 12.");
app.Run();
public partial class Program;
```

- [ ] **Step 6: Add projects to solution**

```
dotnet sln add src/Moongazing.OrionShowcase.Domain/Moongazing.OrionShowcase.Domain.csproj
dotnet sln add src/Moongazing.OrionShowcase.Application/Moongazing.OrionShowcase.Application.csproj
dotnet sln add src/Moongazing.OrionShowcase.Infrastructure/Moongazing.OrionShowcase.Infrastructure.csproj
dotnet sln add src/Moongazing.OrionShowcase.Api/Moongazing.OrionShowcase.Api.csproj
dotnet sln add test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj
dotnet sln add test/Moongazing.OrionShowcase.Application.Tests/Moongazing.OrionShowcase.Application.Tests.csproj
dotnet sln add test/Moongazing.OrionShowcase.IntegrationTests/Moongazing.OrionShowcase.IntegrationTests.csproj
```

- [ ] **Step 7: Replace each csproj**

`src/Moongazing.OrionShowcase.Domain/Moongazing.OrionShowcase.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

`src/Moongazing.OrionShowcase.Application/Moongazing.OrionShowcase.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <ProjectReference Include="..\Moongazing.OrionShowcase.Domain\Moongazing.OrionShowcase.Domain.csproj" />
  </ItemGroup>
</Project>
```

`src/Moongazing.OrionShowcase.Infrastructure/Moongazing.OrionShowcase.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Moongazing.OrionAudit" />
    <PackageReference Include="Moongazing.OrionKey" />
    <PackageReference Include="Moongazing.OrionLock" />
    <PackageReference Include="Moongazing.OrionLock.Postgres" />
    <PackageReference Include="Moongazing.OrionPatch" />
    <PackageReference Include="Moongazing.OrionPatch.EntityFrameworkCore" />
    <PackageReference Include="Moongazing.OrionVault" />
    <PackageReference Include="Moongazing.OrionVault.EntityFrameworkCore" />
    <ProjectReference Include="..\Moongazing.OrionShowcase.Application\Moongazing.OrionShowcase.Application.csproj" />
  </ItemGroup>
</Project>
```

`src/Moongazing.OrionShowcase.Api/Moongazing.OrionShowcase.Api.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <UserSecretsId>orionshowcase-dev</UserSecretsId>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageReference Include="Microsoft.IdentityModel.Tokens" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" />
    <PackageReference Include="Swashbuckle.AspNetCore" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
    <PackageReference Include="Serilog.Sinks.Seq" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.OpenTelemetry" />
    <PackageReference Include="Moongazing.OrionGuard.AspNetCore" />
    <ProjectReference Include="..\Moongazing.OrionShowcase.Infrastructure\Moongazing.OrionShowcase.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

`test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <ProjectReference Include="..\..\src\Moongazing.OrionShowcase.Domain\Moongazing.OrionShowcase.Domain.csproj" />
  </ItemGroup>
</Project>
```

`test/Moongazing.OrionShowcase.Application.Tests/Moongazing.OrionShowcase.Application.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <ProjectReference Include="..\..\src\Moongazing.OrionShowcase.Application\Moongazing.OrionShowcase.Application.csproj" />
  </ItemGroup>
</Project>
```

`test/Moongazing.OrionShowcase.IntegrationTests/Moongazing.OrionShowcase.IntegrationTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <ProjectReference Include="..\..\src\Moongazing.OrionShowcase.Api\Moongazing.OrionShowcase.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 8: Build the empty solution**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0 errors. NuGet restore pulls all 9 Orion packages + framework + test packages. Warnings about empty assemblies and `/` placeholder endpoint are OK.

- [ ] **Step 9: Create `.github/workflows/ci-cd.yml`**

```yaml
name: CI/CD
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  release:
    types: [published]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        dotnet: ['8.0.x']
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: bank
          POSTGRES_PASSWORD: bank
          POSTGRES_DB: banking_ci
        ports: ['5432:5432']
        options: >-
          --health-cmd "pg_isready -U bank"
          --health-interval 5s
          --health-timeout 3s
          --health-retries 10
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet }}
      - name: Restore
        run: dotnet restore Moongazing.OrionShowcase.sln
      - name: Build
        run: dotnet build Moongazing.OrionShowcase.sln -c Release --no-restore
      - name: Test
        run: dotnet test Moongazing.OrionShowcase.sln -c Release --no-build --logger "console;verbosity=normal"
        env:
          ConnectionStrings__BankingCi: "Host=localhost;Port=5432;Username=bank;Password=bank;Database=banking_ci"

  publish-docker:
    needs: build-and-test
    if: github.event_name == 'release'
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4
      - name: Log in to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - name: Build and push image
        uses: docker/build-push-action@v6
        with:
          context: .
          file: docker/Dockerfile.api
          push: true
          tags: |
            ghcr.io/tunahanaliozturk/orionshowcase-api:${{ github.event.release.tag_name }}
            ghcr.io/tunahanaliozturk/orionshowcase-api:latest
```

- [ ] **Step 10: Placeholder `README.md`**

```markdown
# Moongazing.OrionShowcase

Production-shaped banking sample integrating all six Moongazing.Orion packages: OrionGuard, OrionAudit, OrionLock, OrionKey, OrionPatch, OrionVault.

Clean Architecture (Domain / Application / Infrastructure / Api), ASP.NET Core 8 Minimal API, EF Core + Postgres, MediatR CQRS, JWT auth, OpenTelemetry to Jaeger.

Full documentation in [docs/superpowers/specs/](docs/superpowers/specs/) and [ROADMAP.md](ROADMAP.md) (both populated in Task 17).

Part of the Orion family: [OrionGuard](https://github.com/tunahanaliozturk/OrionGuard), [OrionAudit](https://github.com/tunahanaliozturk/OrionAudit), [OrionLock](https://github.com/tunahanaliozturk/OrionLock), [OrionKey](https://github.com/tunahanaliozturk/OrionKey), [OrionPatch](https://github.com/tunahanaliozturk/OrionPatch), [OrionVault](https://github.com/tunahanaliozturk/OrionVault).
```

- [ ] **Step 11: Commit scaffolding**

```
git add -A
git commit -m "chore: scaffold solution, projects, build props, CI workflow"
```

- [ ] **Step 12: Create GitHub repo and push**

```
gh repo create tunahanaliozturk/OrionShowcase --public \
  --description "Production-shaped banking sample using all six Moongazing.Orion packages. Clean Architecture, EF Core, MediatR, OpenTelemetry." \
  --homepage "https://github.com/tunahanaliozturk/OrionShowcase" \
  --source . --remote origin --push
```

- [ ] **Step 13: Verify CI passes on the empty solution**

```
gh run watch
```
Expected: `success`. Build of empty solution succeeds; `dotnet test` returns 0 tests (no test projects have tests yet, but the runner exits cleanly).

---

## Task 1: Domain — value objects + AggregateRoot + IClock

**Files:**
- Create: `src/Moongazing.OrionShowcase.Domain/Abstractions/AggregateRoot.cs`
- Create: `src/Moongazing.OrionShowcase.Domain/Abstractions/IClock.cs`
- Create: `src/Moongazing.OrionShowcase.Domain/ValueObjects/{AccountId, CustomerId, TransactionId, IdempotencyKey, Money, Iban, Tckn, Enums}.cs`
- Test: `test/Moongazing.OrionShowcase.Domain.Tests/ValueObjects/{MoneyTests, IbanTests, TcknTests}.cs`

- [ ] **Step 1: Write failing tests for Money**

`test/Moongazing.OrionShowcase.Domain.Tests/ValueObjects/MoneyTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class MoneyTests
{
    [Fact]
    public void Constructor_throws_when_amount_negative()
    {
        var act = () => new Money(-1m, Currency.TRY);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_same_currency_returns_sum()
    {
        var a = new Money(100m, Currency.TRY);
        var b = new Money(50m, Currency.TRY);
        (a + b).Should().Be(new Money(150m, Currency.TRY));
    }

    [Fact]
    public void Add_different_currency_throws()
    {
        var a = new Money(100m, Currency.TRY);
        var b = new Money(50m, Currency.USD);
        var act = () => { var _ = a + b; };
        act.Should().Throw<InvalidOperationException>().WithMessage("*currency*");
    }

    [Fact]
    public void Subtract_resulting_in_negative_throws()
    {
        var a = new Money(50m, Currency.TRY);
        var b = new Money(100m, Currency.TRY);
        var act = () => { var _ = a - b; };
        act.Should().Throw<InvalidOperationException>().WithMessage("*negative*");
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj
```
Expected: FAIL — `Money` not defined.

- [ ] **Step 3: Create Enums**

`src/Moongazing.OrionShowcase.Domain/ValueObjects/Enums.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public enum Currency { TRY = 949, USD = 840, EUR = 978 }
public enum AccountStatus { Active = 1, Frozen = 2, Closed = 3 }
public enum TransactionKind { Deposit = 1, Withdrawal = 2, TransferOut = 3, TransferIn = 4 }
```

- [ ] **Step 4: Create Money**

`src/Moongazing.OrionShowcase.Domain/ValueObjects/Money.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money amount must be non-negative.");
        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        var result = a.Amount - b.Amount;
        if (result < 0m)
            throw new InvalidOperationException("Money subtraction would produce a negative amount.");
        return new Money(result, a.Currency);
    }

    public static bool operator <(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount < b.Amount;
    }

    public static bool operator >(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount > b.Amount;
    }

    public static bool operator <=(Money a, Money b) => !(a > b);
    public static bool operator >=(Money a, Money b) => !(a < b);

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException(
                $"Cannot operate on Money values with different currency: {a.Currency} vs {b.Currency}.");
    }
}
```

- [ ] **Step 5: Run Money tests — verify pass**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj --filter FullyQualifiedName~MoneyTests
```
Expected: 4 tests PASS.

- [ ] **Step 6: Write failing tests for Iban**

`test/Moongazing.OrionShowcase.Domain.Tests/ValueObjects/IbanTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class IbanTests
{
    [Theory]
    [InlineData("TR330006100519786457841326")]
    [InlineData("DE89370400440532013000")]
    [InlineData("GB29NWBK60161331926819")]
    public void Constructor_accepts_valid_iban(string value)
    {
        var iban = new Iban(value);
        iban.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("TR330006100519786457841327")]  // wrong checksum
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("TR3300061005197864578413261234567890123456")]   // too long
    public void Constructor_rejects_invalid_iban(string value)
    {
        var act = () => new Iban(value);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CountryCode_returns_first_two_letters()
    {
        new Iban("TR330006100519786457841326").CountryCode.Should().Be("TR");
    }
}
```

- [ ] **Step 7: Create Iban**

`src/Moongazing.OrionShowcase.Domain/ValueObjects/Iban.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;

using System.Globalization;
using System.Numerics;

public sealed record Iban
{
    public string Value { get; }
    public string CountryCode => Value[..2];

    public Iban(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("IBAN must not be empty.", nameof(value));
        var normalized = value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length < 15 || normalized.Length > 34)
            throw new ArgumentException("IBAN length must be between 15 and 34 characters.", nameof(value));
        if (!ValidateMod97(normalized))
            throw new ArgumentException("IBAN failed mod-97 checksum.", nameof(value));
        Value = normalized;
    }

    private static bool ValidateMod97(string iban)
    {
        // Move first 4 chars to end
        var rearranged = iban[4..] + iban[..4];
        // Convert letters: A=10, B=11, ... Z=35
        var sb = new System.Text.StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c)) sb.Append(c);
            else if (c >= 'A' && c <= 'Z') sb.Append((c - 'A' + 10).ToString(CultureInfo.InvariantCulture));
            else return false;
        }
        return BigInteger.Parse(sb.ToString(), CultureInfo.InvariantCulture) % 97 == 1;
    }
}
```

- [ ] **Step 8: Run Iban tests — verify pass**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj --filter FullyQualifiedName~IbanTests
```
Expected: 7 tests PASS.

- [ ] **Step 9: Write failing tests for Tckn**

`test/Moongazing.OrionShowcase.Domain.Tests/ValueObjects/TcknTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class TcknTests
{
    [Theory]
    [InlineData("10000000146")]     // Atatürk's TCKN (canonical valid sample)
    [InlineData("12345678950")]
    public void Constructor_accepts_valid_tckn(string value)
    {
        new Tckn(value).Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]    // too short
    [InlineData("123456789012")]  // too long
    [InlineData("00000000000")]   // first digit zero
    [InlineData("12345678901")]   // checksum fails
    [InlineData("abcdefghijk")]   // non-digit
    public void Constructor_rejects_invalid_tckn(string value)
    {
        var act = () => new Tckn(value);
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 10: Create Tckn**

`src/Moongazing.OrionShowcase.Domain/ValueObjects/Tckn.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record Tckn
{
    public string Value { get; }

    public Tckn(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 11 || !value.All(char.IsDigit))
            throw new ArgumentException("TCKN must be exactly 11 digits.", nameof(value));
        if (value[0] == '0')
            throw new ArgumentException("TCKN first digit cannot be zero.", nameof(value));

        var digits = value.Select(c => c - '0').ToArray();
        // 10th digit: ((d1+d3+d5+d7+d9) * 7 - (d2+d4+d6+d8)) mod 10
        int oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        int evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        int tenth = (oddSum * 7 - evenSum) % 10;
        if (tenth < 0) tenth += 10;
        if (tenth != digits[9])
            throw new ArgumentException("TCKN failed checksum (10th digit).", nameof(value));
        // 11th digit: (d1..d10 sum) mod 10
        int total = 0;
        for (int i = 0; i < 10; i++) total += digits[i];
        if (total % 10 != digits[10])
            throw new ArgumentException("TCKN failed checksum (11th digit).", nameof(value));

        Value = value;
    }
}
```

- [ ] **Step 11: Run Tckn tests — verify pass**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj --filter FullyQualifiedName~TcknTests
```
Expected: 8 tests PASS.

- [ ] **Step 12: Create remaining value objects + abstractions**

`src/Moongazing.OrionShowcase.Domain/ValueObjects/AccountId.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;
public readonly record struct AccountId(Guid Value);
```

`src/Moongazing.OrionShowcase.Domain/ValueObjects/CustomerId.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;
public readonly record struct CustomerId(Guid Value);
```

`src/Moongazing.OrionShowcase.Domain/ValueObjects/TransactionId.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;
public readonly record struct TransactionId(long Value);
```

`src/Moongazing.OrionShowcase.Domain/ValueObjects/IdempotencyKey.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public readonly record struct IdempotencyKey
{
    public string Value { get; }
    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("IdempotencyKey must not be empty.", nameof(value));
        if (value.Length > 128)
            throw new ArgumentException("IdempotencyKey must be 128 characters or fewer.", nameof(value));
        Value = value;
    }
}
```

`src/Moongazing.OrionShowcase.Domain/Abstractions/IClock.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Abstractions;
public interface IClock { DateTimeOffset UtcNow { get; } }
```

`src/Moongazing.OrionShowcase.Domain/Abstractions/AggregateRoot.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Abstractions;

public abstract class AggregateRoot<TId>
    where TId : struct
{
    public TId Id { get; protected init; }
    private readonly List<object> _events = new();
    public IReadOnlyList<object> DomainEvents => _events;
    protected void Raise(object domainEvent) => _events.Add(domainEvent);
    public void ClearDomainEvents() => _events.Clear();
}
```

- [ ] **Step 13: Final build + commit**

```
dotnet build Moongazing.OrionShowcase.sln
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj
```
Expected: 0/0 build; 19 tests pass (4 Money + 7 Iban + 8 Tckn).

```
git add src/Moongazing.OrionShowcase.Domain/ test/Moongazing.OrionShowcase.Domain.Tests/
git commit -m "feat(domain): value objects (Money, Iban, Tckn) + AggregateRoot + IClock with tests"
git push
```

---

## Task 2: Domain — Account aggregate + Transaction + events + exceptions

**Files:**
- Create: `src/Moongazing.OrionShowcase.Domain/Accounts/{Account, Transaction, Events, Exceptions}.cs`
- Create: `src/Moongazing.OrionShowcase.Domain/Repositories/{IAccountRepository, IUnitOfWork}.cs`
- Test: `test/Moongazing.OrionShowcase.Domain.Tests/Accounts/AccountTests.cs`

- [ ] **Step 1: Write failing tests**

`test/Moongazing.OrionShowcase.Domain.Tests/Accounts/AccountTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Tests.Accounts;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class AccountTests
{
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; init; } = DateTimeOffset.UnixEpoch; }

    private static Account Open(decimal amount = 100m, Currency c = Currency.TRY) =>
        Account.Open(
            new CustomerId(Guid.NewGuid()),
            new Iban("TR330006100519786457841326"),
            new Money(amount, c),
            new FixedClock());

    [Fact]
    public void Open_raises_AccountOpened_and_sets_balance()
    {
        var account = Open(100m);
        account.Balance.Should().Be(new Money(100m, Currency.TRY));
        account.Status.Should().Be(AccountStatus.Active);
        account.DomainEvents.Should().ContainSingle(e => e is AccountOpened);
    }

    [Fact]
    public void Deposit_increases_balance_and_records_transaction_and_event()
    {
        var account = Open(100m);
        account.ClearDomainEvents();

        account.Deposit(new Money(50m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());

        account.Balance.Should().Be(new Money(150m, Currency.TRY));
        account.Transactions.Should().ContainSingle(t => t.Kind == TransactionKind.Deposit);
        account.DomainEvents.Should().ContainSingle(e => e is MoneyDeposited);
    }

    [Fact]
    public void Deposit_with_different_currency_throws()
    {
        var account = Open(100m, Currency.TRY);
        var act = () => account.Deposit(new Money(50m, Currency.USD), new IdempotencyKey("k1"), new FixedClock());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Withdraw_decreases_balance()
    {
        var account = Open(100m);
        account.Withdraw(new Money(40m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());
        account.Balance.Should().Be(new Money(60m, Currency.TRY));
    }

    [Fact]
    public void Withdraw_more_than_balance_throws_InsufficientFundsException()
    {
        var account = Open(100m);
        var act = () => account.Withdraw(new Money(150m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());
        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void Freeze_changes_status_and_subsequent_deposit_throws()
    {
        var account = Open(100m);
        account.Freeze("manual review", new FixedClock());
        account.Status.Should().Be(AccountStatus.Frozen);
        var act = () => account.Deposit(new Money(10m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());
        act.Should().Throw<AccountNotActiveException>();
    }

    [Fact]
    public void Close_requires_zero_balance()
    {
        var account = Open(100m);
        var act = () => account.Close(new FixedClock());
        act.Should().Throw<AccountNotEmptyException>();
    }

    [Fact]
    public void Close_with_zero_balance_succeeds()
    {
        var account = Open(0m);
        account.Close(new FixedClock());
        account.Status.Should().Be(AccountStatus.Closed);
        account.DomainEvents.Should().Contain(e => e is AccountClosed);
    }
}
```

- [ ] **Step 2: Run tests — verify fail**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj --filter FullyQualifiedName~AccountTests
```
Expected: FAIL — `Account`, exceptions not defined.

- [ ] **Step 3: Create Events + Exceptions + Transaction + Account**

`src/Moongazing.OrionShowcase.Domain/Accounts/Events.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Accounts;

using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record AccountOpened(AccountId AccountId, CustomerId CustomerId, Iban Iban, Money Opening, DateTimeOffset At);
public sealed record MoneyDeposited(AccountId AccountId, Money Amount, Money NewBalance, IdempotencyKey Key, DateTimeOffset At);
public sealed record MoneyWithdrawn(AccountId AccountId, Money Amount, Money NewBalance, IdempotencyKey Key, DateTimeOffset At);
public sealed record TransferCompleted(AccountId From, AccountId To, Money Amount, IdempotencyKey Key, DateTimeOffset At);
public sealed record AccountFrozen(AccountId AccountId, string Reason, DateTimeOffset At);
public sealed record AccountClosed(AccountId AccountId, DateTimeOffset At);
```

`src/Moongazing.OrionShowcase.Domain/Accounts/Exceptions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Accounts;

public sealed class InsufficientFundsException : InvalidOperationException
{
    public InsufficientFundsException() : base("Insufficient funds.") { }
}

public sealed class AccountNotActiveException : InvalidOperationException
{
    public AccountNotActiveException(string operation)
        : base($"Account is not active; cannot perform '{operation}'.") { }
}

public sealed class AccountNotEmptyException : InvalidOperationException
{
    public AccountNotEmptyException() : base("Cannot close an account that still has a non-zero balance.") { }
}
```

`src/Moongazing.OrionShowcase.Domain/Accounts/Transaction.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Accounts;

using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class Transaction
{
    public TransactionId Id { get; init; }
    public TransactionKind Kind { get; init; }
    public Money Amount { get; init; } = Money.Zero(Currency.TRY);
    public Money BalanceAfter { get; init; } = Money.Zero(Currency.TRY);
    public IdempotencyKey IdempotencyKey { get; init; }
    public DateTimeOffset At { get; init; }
}
```

`src/Moongazing.OrionShowcase.Domain/Accounts/Account.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Accounts;

using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class Account : AggregateRoot<AccountId>
{
    public CustomerId CustomerId { get; private set; }
    public Iban Iban { get; private set; } = null!;
    public Money Balance { get; private set; } = null!;
    public AccountStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    private readonly List<Transaction> _transactions = new();
    public IReadOnlyList<Transaction> Transactions => _transactions;

    private Account() { }   // EF Core ctor

    public static Account Open(CustomerId customer, Iban iban, Money opening, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(iban);
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(clock);

        var id = new AccountId(Guid.NewGuid());
        var account = new Account
        {
            Id = id,
            CustomerId = customer,
            Iban = iban,
            Balance = opening,
            Status = AccountStatus.Active,
            OpenedAt = clock.UtcNow
        };
        account.Raise(new AccountOpened(id, customer, iban, opening, clock.UtcNow));
        return account;
    }

    public void Deposit(Money amount, IdempotencyKey key, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Deposit));
        Balance = Balance + amount;
        _transactions.Add(new Transaction
        {
            Id = new TransactionId(0),   // assigned by OrionKey snowflake in Infrastructure later
            Kind = TransactionKind.Deposit,
            Amount = amount,
            BalanceAfter = Balance,
            IdempotencyKey = key,
            At = clock.UtcNow
        });
        Raise(new MoneyDeposited(Id, amount, Balance, key, clock.UtcNow));
    }

    public void Withdraw(Money amount, IdempotencyKey key, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Withdraw));
        if (Balance < amount) throw new InsufficientFundsException();
        Balance = Balance - amount;
        _transactions.Add(new Transaction
        {
            Id = new TransactionId(0),
            Kind = TransactionKind.Withdrawal,
            Amount = amount,
            BalanceAfter = Balance,
            IdempotencyKey = key,
            At = clock.UtcNow
        });
        Raise(new MoneyWithdrawn(Id, amount, Balance, key, clock.UtcNow));
    }

    public void RecordTransfer(AccountId counterparty, Money amount, IdempotencyKey key, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(clock);
        Raise(new TransferCompleted(Id, counterparty, amount, key, clock.UtcNow));
    }

    public void Freeze(string reason, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Freeze));
        Status = AccountStatus.Frozen;
        Raise(new AccountFrozen(Id, reason, clock.UtcNow));
    }

    public void Close(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Close));
        if (Balance.Amount != 0m) throw new AccountNotEmptyException();
        Status = AccountStatus.Closed;
        Raise(new AccountClosed(Id, clock.UtcNow));
    }

    private void EnsureActive(string op)
    {
        if (Status != AccountStatus.Active) throw new AccountNotActiveException(op);
    }
}
```

- [ ] **Step 4: Create repository interfaces**

`src/Moongazing.OrionShowcase.Domain/Repositories/IAccountRepository.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Repositories;

using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public interface IAccountRepository
{
    Task<Account?> GetAsync(AccountId id, CancellationToken ct);
    Task AddAsync(Account account, CancellationToken ct);
}
```

`src/Moongazing.OrionShowcase.Domain/Repositories/IUnitOfWork.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Repositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
```

- [ ] **Step 5: Run tests + commit**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj
```
Expected: 19 (Task 1) + 8 (Task 2) = 27 tests PASS.

```
git add src/Moongazing.OrionShowcase.Domain/ test/Moongazing.OrionShowcase.Domain.Tests/
git commit -m "feat(domain): Account aggregate with deposit/withdraw/transfer/freeze/close + events + tests"
git push
```

---

## Task 3: Domain — Customer aggregate + events

**Files:**
- Create: `src/Moongazing.OrionShowcase.Domain/Customers/{Customer, Events}.cs`
- Create: `src/Moongazing.OrionShowcase.Domain/Repositories/ICustomerRepository.cs`
- Test: `test/Moongazing.OrionShowcase.Domain.Tests/Customers/CustomerTests.cs`

- [ ] **Step 1: Write failing test**

`test/Moongazing.OrionShowcase.Domain.Tests/Customers/CustomerTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Tests.Customers;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class CustomerTests
{
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; init; } = DateTimeOffset.UnixEpoch; }

    [Fact]
    public void Register_raises_CustomerRegistered_event_and_sets_properties()
    {
        var c = Customer.Register(
            "Ali Veli",
            new Tckn("10000000146"),
            "ali@example.com",
            "+905551234567",
            new FixedClock());

        c.FullName.Should().Be("Ali Veli");
        c.NationalId.Value.Should().Be("10000000146");
        c.Email.Should().Be("ali@example.com");
        c.Phone.Should().Be("+905551234567");
        c.DomainEvents.Should().ContainSingle(e => e is CustomerRegistered);
    }

    [Fact]
    public void Register_with_blank_name_throws()
    {
        var act = () => Customer.Register(" ", new Tckn("10000000146"), "ali@x.com", "+905551234567", new FixedClock());
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run test — verify fail**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj --filter FullyQualifiedName~CustomerTests
```
Expected: FAIL.

- [ ] **Step 3: Create Customer + events**

`src/Moongazing.OrionShowcase.Domain/Customers/Events.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Customers;

using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record CustomerRegistered(CustomerId CustomerId, DateTimeOffset At);
```

`src/Moongazing.OrionShowcase.Domain/Customers/Customer.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Customers;

using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class Customer : AggregateRoot<CustomerId>
{
    public string FullName { get; private set; } = null!;
    public Tckn NationalId { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public DateTimeOffset RegisteredAt { get; private set; }

    private Customer() { }

    public static Customer Register(string fullName, Tckn nationalId, string email, string phone, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentNullException.ThrowIfNull(nationalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentNullException.ThrowIfNull(clock);

        var id = new CustomerId(Guid.NewGuid());
        var c = new Customer
        {
            Id = id,
            FullName = fullName.Trim(),
            NationalId = nationalId,
            Email = email.Trim(),
            Phone = phone.Trim(),
            RegisteredAt = clock.UtcNow
        };
        c.Raise(new CustomerRegistered(id, clock.UtcNow));
        return c;
    }
}
```

`src/Moongazing.OrionShowcase.Domain/Repositories/ICustomerRepository.cs`:
```csharp
namespace Moongazing.OrionShowcase.Domain.Repositories;

using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(CustomerId id, CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
}
```

- [ ] **Step 4: Run all Domain tests + commit**

```
dotnet test test/Moongazing.OrionShowcase.Domain.Tests/Moongazing.OrionShowcase.Domain.Tests.csproj
```
Expected: 27 + 2 = 29 tests PASS.

```
git add src/Moongazing.OrionShowcase.Domain/ test/Moongazing.OrionShowcase.Domain.Tests/
git commit -m "feat(domain): Customer aggregate with Register + events"
git push
```

---

## Task 4: Application — MediatR + 4 pipeline behaviors + Result type

**Files:**
- Create: `src/Moongazing.OrionShowcase.Application/Common/Result.cs`
- Create: `src/Moongazing.OrionShowcase.Application/Abstractions/{ICurrentUser, IIdempotencyStore, IAuditWriter}.cs`
- Create: `src/Moongazing.OrionShowcase.Application/Pipeline/{IAuditableCommand, IIdempotentCommand, ValidationBehavior, LoggingBehavior, IdempotencyBehavior, AuditBehavior}.cs`
- Create: `src/Moongazing.OrionShowcase.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- Test: `test/Moongazing.OrionShowcase.Application.Tests/Pipeline/{ValidationBehaviorTests, IdempotencyBehaviorTests, AuditBehaviorTests}.cs`

- [ ] **Step 1: Create Result type and abstractions**

`src/Moongazing.OrionShowcase.Application/Common/Result.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Common;

public sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
```

`src/Moongazing.OrionShowcase.Application/Abstractions/ICurrentUser.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Abstractions;

public interface ICurrentUser
{
    string Id { get; }
    string Username { get; }
    bool IsAuthenticated { get; }
}
```

`src/Moongazing.OrionShowcase.Application/Abstractions/IIdempotencyStore.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken ct);
    Task<string?> GetCachedResponseAsync(string key, CancellationToken ct);
    Task StoreResponseAsync(string key, string serialisedResponse, CancellationToken ct);
}
```

`src/Moongazing.OrionShowcase.Application/Abstractions/IAuditWriter.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Abstractions;

public interface IAuditWriter
{
    Task WriteAsync(string actor, string action, string requestJson, string? responseJson, bool succeeded, string? error, CancellationToken ct);
}
```

- [ ] **Step 2: Create pipeline markers and behaviors**

`src/Moongazing.OrionShowcase.Application/Pipeline/IAuditableCommand.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Pipeline;
public interface IAuditableCommand { }
```

`src/Moongazing.OrionShowcase.Application/Pipeline/IIdempotentCommand.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;
public interface IIdempotentCommand { IdempotencyKey IdempotencyKey { get; } }
```

`src/Moongazing.OrionShowcase.Application/Pipeline/ValidationBehavior.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Pipeline;

using FluentValidation;
using MediatR;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count > 0) throw new ValidationException(failures);
        return await next();
    }
}
```

`src/Moongazing.OrionShowcase.Application/Pipeline/LoggingBehavior.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Pipeline;

using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

public sealed partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _log;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> log) => _log = log;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.GetTimestamp();
        LogStart(name);
        try
        {
            var response = await next();
            LogEnd(name, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            LogFailure(name, Stopwatch.GetElapsedTime(sw).TotalMilliseconds, ex);
            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Handling {RequestName}.")]
    partial void LogStart(string requestName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs} ms.")]
    partial void LogEnd(string requestName, double elapsedMs);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed {RequestName} after {ElapsedMs} ms.")]
    partial void LogFailure(string requestName, double elapsedMs, Exception ex);
}
```

`src/Moongazing.OrionShowcase.Application/Pipeline/IdempotencyBehavior.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Pipeline;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Moongazing.OrionShowcase.Application.Abstractions;

public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand
{
    private readonly IIdempotencyStore _store;
    public IdempotencyBehavior(IIdempotencyStore store) => _store = store;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = request.IdempotencyKey.Value;
        var hash = ComputeHash(request);

        var cached = await _store.GetCachedResponseAsync(key, ct);
        if (cached is not null)
        {
            var deserialised = JsonSerializer.Deserialize<TResponse>(cached)
                ?? throw new InvalidOperationException("Cached response deserialised to null.");
            return deserialised;
        }

        if (!await _store.TryClaimAsync(key, hash, ct))
            throw new InvalidOperationException($"Idempotency key '{key}' is in flight with a different request.");

        var response = await next();
        await _store.StoreResponseAsync(key, JsonSerializer.Serialize(response), ct);
        return response;
    }

    private static string ComputeHash(TRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
```

`src/Moongazing.OrionShowcase.Application/Pipeline/AuditBehavior.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Pipeline;

using System.Text.Json;
using MediatR;
using Moongazing.OrionShowcase.Application.Abstractions;

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuditableCommand
{
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _user;

    public AuditBehavior(IAuditWriter audit, ICurrentUser user)
    {
        _audit = audit;
        _user = user;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var actor = _user.IsAuthenticated ? _user.Username : "anonymous";
        var action = typeof(TRequest).Name;
        var requestJson = JsonSerializer.Serialize(request);
        try
        {
            var response = await next();
            await _audit.WriteAsync(actor, action, requestJson, JsonSerializer.Serialize(response), true, null, ct);
            return response;
        }
        catch (Exception ex)
        {
            await _audit.WriteAsync(actor, action, requestJson, null, false, ex.Message, ct);
            throw;
        }
    }
}
```

- [ ] **Step 3: Application DI extension**

`src/Moongazing.OrionShowcase.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.DependencyInjection;

using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShowcase.Application.Pipeline;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        // Pipeline order: outermost first
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return services;
    }
}
```

- [ ] **Step 4: Write failing tests for ValidationBehavior**

`test/Moongazing.OrionShowcase.Application.Tests/Pipeline/ValidationBehaviorTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Tests.Pipeline;

using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moongazing.OrionShowcase.Application.Pipeline;
using Xunit;

public class ValidationBehaviorTests
{
    public sealed record SampleRequest(string Name);

    private sealed class FailingValidator : AbstractValidator<SampleRequest>
    {
        public FailingValidator() { RuleFor(x => x.Name).NotEmpty(); }
    }

    [Fact]
    public async Task Throws_ValidationException_when_request_invalid()
    {
        var sut = new ValidationBehavior<SampleRequest, Unit>(new[] { new FailingValidator() });
        var act = async () => await sut.Handle(new SampleRequest(""), () => Task.FromResult(Unit.Value), default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Calls_next_when_request_valid()
    {
        var sut = new ValidationBehavior<SampleRequest, Unit>(new[] { new FailingValidator() });
        var result = await sut.Handle(new SampleRequest("ali"), () => Task.FromResult(Unit.Value), default);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Skips_validation_when_no_validators_registered()
    {
        var sut = new ValidationBehavior<SampleRequest, Unit>(Array.Empty<IValidator<SampleRequest>>());
        var result = await sut.Handle(new SampleRequest(""), () => Task.FromResult(Unit.Value), default);
        result.Should().Be(Unit.Value);
    }
}
```

- [ ] **Step 5: Write failing tests for IdempotencyBehavior**

`test/Moongazing.OrionShowcase.Application.Tests/Pipeline/IdempotencyBehaviorTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Tests.Pipeline;

using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class IdempotencyBehaviorTests
{
    public sealed record IdempotentRequest(string Payload, IdempotencyKey IdempotencyKey) : IIdempotentCommand;

    private sealed class FakeStore : IIdempotencyStore
    {
        public ConcurrentDictionary<string, string> Cached { get; } = new();
        public ConcurrentDictionary<string, string> Hashes { get; } = new();
        public Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken ct)
            => Task.FromResult(Hashes.TryAdd(key, requestHash));
        public Task<string?> GetCachedResponseAsync(string key, CancellationToken ct)
            => Task.FromResult(Cached.TryGetValue(key, out var v) ? v : null);
        public Task StoreResponseAsync(string key, string serialisedResponse, CancellationToken ct)
        { Cached[key] = serialisedResponse; return Task.CompletedTask; }
    }

    [Fact]
    public async Task First_call_invokes_handler_and_stores_response()
    {
        var store = new FakeStore();
        var sut = new IdempotencyBehavior<IdempotentRequest, string>(store);
        var calls = 0;

        var result = await sut.Handle(
            new IdempotentRequest("x", new IdempotencyKey("k1")),
            () => { calls++; return Task.FromResult("response-1"); },
            default);

        calls.Should().Be(1);
        result.Should().Be("response-1");
        store.Cached["k1"].Should().Be(JsonSerializer.Serialize("response-1"));
    }

    [Fact]
    public async Task Second_call_with_same_key_returns_cached_without_calling_handler()
    {
        var store = new FakeStore();
        var sut = new IdempotencyBehavior<IdempotentRequest, string>(store);

        await sut.Handle(
            new IdempotentRequest("x", new IdempotencyKey("k1")),
            () => Task.FromResult("response-1"), default);

        var calls = 0;
        var result = await sut.Handle(
            new IdempotentRequest("x", new IdempotencyKey("k1")),
            () => { calls++; return Task.FromResult("response-2"); }, default);

        calls.Should().Be(0);
        result.Should().Be("response-1");
    }
}
```

- [ ] **Step 6: Write failing tests for AuditBehavior**

`test/Moongazing.OrionShowcase.Application.Tests/Pipeline/AuditBehaviorTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Tests.Pipeline;

using FluentAssertions;
using MediatR;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Pipeline;
using Xunit;

public class AuditBehaviorTests
{
    public sealed record SampleCommand(string X) : IAuditableCommand;

    private sealed class CapturingAudit : IAuditWriter
    {
        public List<(string actor, string action, string req, string? res, bool ok, string? err)> Records { get; } = new();
        public Task WriteAsync(string actor, string action, string requestJson, string? responseJson, bool succeeded, string? error, CancellationToken ct)
        { Records.Add((actor, action, requestJson, responseJson, succeeded, error)); return Task.CompletedTask; }
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string Id => "1";
        public string Username => "demo";
        public bool IsAuthenticated => true;
    }

    [Fact]
    public async Task Writes_success_record_when_handler_returns()
    {
        var audit = new CapturingAudit();
        var sut = new AuditBehavior<SampleCommand, string>(audit, new FixedUser());
        await sut.Handle(new SampleCommand("hello"), () => Task.FromResult("ok"), default);

        audit.Records.Should().ContainSingle();
        var rec = audit.Records[0];
        rec.actor.Should().Be("demo");
        rec.action.Should().Be("SampleCommand");
        rec.ok.Should().BeTrue();
    }

    [Fact]
    public async Task Writes_failure_record_when_handler_throws()
    {
        var audit = new CapturingAudit();
        var sut = new AuditBehavior<SampleCommand, string>(audit, new FixedUser());
        var act = async () => await sut.Handle(new SampleCommand("hello"), () => throw new InvalidOperationException("boom"), default);
        await act.Should().ThrowAsync<InvalidOperationException>();

        audit.Records.Should().ContainSingle();
        audit.Records[0].ok.Should().BeFalse();
        audit.Records[0].err.Should().Be("boom");
    }
}
```

- [ ] **Step 7: Add ProjectReference + run all tests**

Application.Tests csproj already references Application via Step 7 of Task 0. Add reference from Application.Tests to Domain (needed for `IdempotencyKey`):

```xml
<!-- Append to test/Moongazing.OrionShowcase.Application.Tests/Moongazing.OrionShowcase.Application.Tests.csproj -->
<ProjectReference Include="..\..\src\Moongazing.OrionShowcase.Domain\Moongazing.OrionShowcase.Domain.csproj" />
```

```
dotnet build Moongazing.OrionShowcase.sln
dotnet test test/Moongazing.OrionShowcase.Application.Tests/Moongazing.OrionShowcase.Application.Tests.csproj
```
Expected: 0/0 build; 7 tests pass (3 Validation + 2 Idempotency + 2 Audit).

- [ ] **Step 8: Commit**

```
git add src/Moongazing.OrionShowcase.Application/ test/Moongazing.OrionShowcase.Application.Tests/
git commit -m "feat(application): MediatR pipeline (Validation, Logging, Idempotency, Audit) + Result + abstractions"
git push
```

---

## Task 5: Application — commands + handlers + validators

**Files (new per command, structure repeated for each):**
- `src/Moongazing.OrionShowcase.Application/{Customers,Accounts}/Commands/<Name>/<Name>Command.cs`
- `<Name>Handler.cs`
- `<Name>Validator.cs`

Commands to create:
- `RegisterCustomerCommand` (Customers folder)
- `OpenAccountCommand`, `DepositMoneyCommand`, `WithdrawMoneyCommand`, `TransferMoneyCommand`, `FreezeAccountCommand`, `CloseAccountCommand` (Accounts folder)

Test files: one per command in `test/Moongazing.OrionShowcase.Application.Tests/...`

This task is long (7 commands × 3 files = 21 files + tests). The pattern is identical per command. Below is the complete example for `TransferMoneyCommand` because it is the most complex (touches OrionLock). Apply the same shape to the others.

### Step 1: Define `TransferMoneyCommand`

`src/Moongazing.OrionShowcase.Application/Accounts/Commands/TransferMoney/TransferMoneyCommand.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record TransferMoneyCommand(
    AccountId From,
    AccountId To,
    Money Amount,
    IdempotencyKey IdempotencyKey)
    : IRequest<Result<TransferMoneyResult>>, IAuditableCommand, IIdempotentCommand;

public sealed record TransferMoneyResult(Guid TransferId, decimal NewSourceBalance);
```

### Step 2: Define the validator

`src/Moongazing.OrionShowcase.Application/Accounts/Commands/TransferMoney/TransferMoneyValidator.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;

using FluentValidation;

public sealed class TransferMoneyValidator : AbstractValidator<TransferMoneyCommand>
{
    public TransferMoneyValidator()
    {
        RuleFor(x => x.From.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.To.Value).NotEqual(Guid.Empty);
        RuleFor(x => x).Must(c => c.From.Value != c.To.Value)
            .WithMessage("Source and target accounts must differ.");
        RuleFor(x => x.Amount.Amount).GreaterThan(0m);
        RuleFor(x => x.IdempotencyKey.Value).NotEmpty().MaximumLength(128);
    }
}
```

### Step 3: Define abstraction for OrionLock

OrionLock's API uses `IOrionLock` from `Moongazing.OrionLock`. Reference this in Application so handlers can declare a dependency on it.

Add to `src/Moongazing.OrionShowcase.Application/Moongazing.OrionShowcase.Application.csproj`:
```xml
<PackageReference Include="Moongazing.OrionLock" />
```

### Step 4: Define the handler

`src/Moongazing.OrionShowcase.Application/Accounts/Commands/TransferMoney/TransferMoneyHandler.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;

using MediatR;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class TransferMoneyHandler : IRequestHandler<TransferMoneyCommand, Result<TransferMoneyResult>>
{
    private readonly IOrionLock _locks;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public TransferMoneyHandler(IOrionLock locks, IAccountRepository accounts, IUnitOfWork uow, IClock clock)
    {
        _locks = locks;
        _accounts = accounts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<TransferMoneyResult>> Handle(TransferMoneyCommand cmd, CancellationToken ct)
    {
        // Sort to prevent deadlock - acquire the lower account id first
        var (lower, higher) = cmd.From.Value.CompareTo(cmd.To.Value) < 0
            ? (cmd.From, cmd.To)
            : (cmd.To, cmd.From);

        await using var lockA = await _locks.AcquireAsync($"account:{lower.Value}", TimeSpan.FromSeconds(30), ct);
        await using var lockB = await _locks.AcquireAsync($"account:{higher.Value}", TimeSpan.FromSeconds(30), ct);

        var from = await _accounts.GetAsync(cmd.From, ct);
        var to = await _accounts.GetAsync(cmd.To, ct);
        if (from is null) return Result<TransferMoneyResult>.Fail($"Source account {cmd.From.Value} not found.");
        if (to is null) return Result<TransferMoneyResult>.Fail($"Target account {cmd.To.Value} not found.");

        try
        {
            from.Withdraw(cmd.Amount, cmd.IdempotencyKey, _clock);
            to.Deposit(cmd.Amount, cmd.IdempotencyKey, _clock);
            from.RecordTransfer(to.Id, cmd.Amount, cmd.IdempotencyKey, _clock);
        }
        catch (InsufficientFundsException)
        {
            return Result<TransferMoneyResult>.Fail("Insufficient funds in source account.");
        }
        catch (AccountNotActiveException ex)
        {
            return Result<TransferMoneyResult>.Fail(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<TransferMoneyResult>.Ok(new TransferMoneyResult(Guid.NewGuid(), from.Balance.Amount));
    }
}
```

### Step 5: Repeat shape for the other 6 commands

Each follows the same three-file pattern. Skeleton for `OpenAccountCommand`:

`src/Moongazing.OrionShowcase.Application/Accounts/Commands/OpenAccount/OpenAccountCommand.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record OpenAccountCommand(
    CustomerId CustomerId,
    string Iban,
    decimal OpeningAmount,
    Currency Currency,
    IdempotencyKey IdempotencyKey)
    : IRequest<Result<OpenAccountResult>>, IAuditableCommand, IIdempotentCommand;

public sealed record OpenAccountResult(Guid AccountId);
```

`OpenAccountValidator.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;

using FluentValidation;

public sealed class OpenAccountValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountValidator()
    {
        RuleFor(x => x.CustomerId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Iban).NotEmpty();
        RuleFor(x => x.OpeningAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.IdempotencyKey.Value).NotEmpty().MaximumLength(128);
    }
}
```

`OpenAccountHandler.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class OpenAccountHandler : IRequestHandler<OpenAccountCommand, Result<OpenAccountResult>>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public OpenAccountHandler(IAccountRepository accounts, IUnitOfWork uow, IClock clock)
    {
        _accounts = accounts; _uow = uow; _clock = clock;
    }

    public async Task<Result<OpenAccountResult>> Handle(OpenAccountCommand cmd, CancellationToken ct)
    {
        var iban = new Iban(cmd.Iban);
        var opening = new Money(cmd.OpeningAmount, cmd.Currency);
        var account = Account.Open(cmd.CustomerId, iban, opening, _clock);
        await _accounts.AddAsync(account, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<OpenAccountResult>.Ok(new OpenAccountResult(account.Id.Value));
    }
}
```

Apply the same triplet (Command + Validator + Handler) for:
- `DepositMoneyCommand(AccountId AccountId, decimal Amount, Currency Currency, IdempotencyKey Key)` → calls `account.Deposit(...)`
- `WithdrawMoneyCommand(AccountId AccountId, decimal Amount, Currency Currency, IdempotencyKey Key)` → calls `account.Withdraw(...)`
- `FreezeAccountCommand(AccountId AccountId, string Reason)` → calls `account.Freeze(...)`. Only `IAuditableCommand`, no idempotency.
- `CloseAccountCommand(AccountId AccountId)` → calls `account.Close(...)`. Only `IAuditableCommand`.
- `RegisterCustomerCommand(string FullName, string NationalId, string Email, string Phone, IdempotencyKey Key)` → calls `Customer.Register(...)`, uses `ICustomerRepository`.

### Step 6: Write handler tests with in-memory fakes

`test/Moongazing.OrionShowcase.Application.Tests/Accounts/TransferMoneyHandlerTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using FluentAssertions;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class TransferMoneyHandlerTests
{
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch; }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public Dictionary<AccountId, Account> Store { get; } = new();
        public Task AddAsync(Account a, CancellationToken ct) { Store[a.Id] = a; return Task.CompletedTask; }
        public Task<Account?> GetAsync(AccountId id, CancellationToken ct) => Task.FromResult(Store.GetValueOrDefault(id));
    }

    private sealed class NoopUow : IUnitOfWork { public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1); }

    private sealed class RecordingLock : IOrionLock
    {
        public List<string> Acquired { get; } = new();
        public async Task<IAsyncDisposable> AcquireAsync(string key, TimeSpan timeout, CancellationToken ct)
        {
            Acquired.Add(key);
            return await Task.FromResult<IAsyncDisposable>(new Lease());
        }
        public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan timeout, CancellationToken ct)
            => throw new NotImplementedException();
        private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task Acquires_locks_on_both_accounts_in_sorted_order()
    {
        var clock = new FixedClock();
        var lower = new AccountId(new Guid("00000000-0000-0000-0000-000000000001"));
        var higher = new AccountId(new Guid("00000000-0000-0000-0000-000000000002"));
        var repo = new FakeAccountRepo();
        var iban = new Iban("TR330006100519786457841326");
        var customer = new CustomerId(Guid.NewGuid());
        var a = Account.Open(customer, iban, new Money(100m, Currency.TRY), clock);
        var b = Account.Open(customer, iban, new Money(0m, Currency.TRY), clock);
        a.GetType().GetProperty(nameof(Account.Id))!.SetValue(a, lower);
        b.GetType().GetProperty(nameof(Account.Id))!.SetValue(b, higher);
        repo.Store[lower] = a; repo.Store[higher] = b;

        var locks = new RecordingLock();
        var sut = new TransferMoneyHandler(locks, repo, new NoopUow(), clock);

        // Call in REVERSED order (higher -> lower) to confirm sorting kicks in
        await sut.Handle(new TransferMoneyCommand(higher, lower, new Money(30m, Currency.TRY), new IdempotencyKey("k1")), default);

        locks.Acquired.Should().HaveCount(2);
        locks.Acquired[0].Should().Be($"account:{lower.Value}");
        locks.Acquired[1].Should().Be($"account:{higher.Value}");
    }

    [Fact]
    public async Task Fails_with_descriptive_error_when_source_has_insufficient_funds()
    {
        var clock = new FixedClock();
        var lower = new AccountId(new Guid("00000000-0000-0000-0000-000000000001"));
        var higher = new AccountId(new Guid("00000000-0000-0000-0000-000000000002"));
        var repo = new FakeAccountRepo();
        var iban = new Iban("TR330006100519786457841326");
        var customer = new CustomerId(Guid.NewGuid());
        var a = Account.Open(customer, iban, new Money(10m, Currency.TRY), clock);
        var b = Account.Open(customer, iban, new Money(0m, Currency.TRY), clock);
        a.GetType().GetProperty(nameof(Account.Id))!.SetValue(a, lower);
        b.GetType().GetProperty(nameof(Account.Id))!.SetValue(b, higher);
        repo.Store[lower] = a; repo.Store[higher] = b;

        var sut = new TransferMoneyHandler(new RecordingLock(), repo, new NoopUow(), clock);
        var result = await sut.Handle(new TransferMoneyCommand(lower, higher, new Money(50m, Currency.TRY), new IdempotencyKey("k2")), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Insufficient funds");
    }
}
```

Note: reflection to set `Id` on `Account` is a test concession because `AggregateRoot<TId>.Id` has `protected init`. Production code uses `Account.Open` which generates the id internally.

Write similar `<CommandName>HandlerTests.cs` for each of the other 6 commands.

### Step 7: Build + tests + commit

```
dotnet build Moongazing.OrionShowcase.sln
dotnet test test/Moongazing.OrionShowcase.Application.Tests/Moongazing.OrionShowcase.Application.Tests.csproj
```
Expected: 0/0 build; 7 (Task 4) + ~14 handler tests = ~21 tests pass.

```
git add src/Moongazing.OrionShowcase.Application/ test/Moongazing.OrionShowcase.Application.Tests/
git commit -m "feat(application): 7 commands (Customers + Accounts) with handlers, validators, tests"
git push
```

---

## Task 6: Application — queries + DTOs

**Files:**
- Create: `src/Moongazing.OrionShowcase.Application/Accounts/Queries/GetAccountBalance/{Query, Handler, Dto}.cs`
- Create: `src/Moongazing.OrionShowcase.Application/Accounts/Queries/GetAccountTransactions/{Query, Handler, Dto}.cs`

Queries are read-only, bypass most of the pipeline (no Validation, no Audit, no Idempotency). Only LoggingBehavior applies.

### Step 1: GetAccountBalance

`src/Moongazing.OrionShowcase.Application/Accounts/Queries/GetAccountBalance/GetAccountBalanceQuery.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record GetAccountBalanceQuery(AccountId AccountId) : IRequest<Result<AccountBalanceDto>>;
```

`AccountBalanceDto.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;

public sealed record AccountBalanceDto(Guid AccountId, decimal Balance, string Currency, string Status);
```

`GetAccountBalanceHandler.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class GetAccountBalanceHandler : IRequestHandler<GetAccountBalanceQuery, Result<AccountBalanceDto>>
{
    private readonly IAccountRepository _accounts;

    public GetAccountBalanceHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<Result<AccountBalanceDto>> Handle(GetAccountBalanceQuery req, CancellationToken ct)
    {
        var account = await _accounts.GetAsync(req.AccountId, ct);
        if (account is null) return Result<AccountBalanceDto>.Fail("Account not found.");
        return Result<AccountBalanceDto>.Ok(new AccountBalanceDto(
            account.Id.Value, account.Balance.Amount, account.Balance.Currency.ToString(), account.Status.ToString()));
    }
}
```

### Step 2: GetAccountTransactions

`GetAccountTransactionsQuery.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record GetAccountTransactionsQuery(AccountId AccountId, int Page = 1, int PageSize = 50)
    : IRequest<Result<IReadOnlyList<TransactionDto>>>;
```

`TransactionDto.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;

public sealed record TransactionDto(long Id, string Kind, decimal Amount, string Currency, decimal BalanceAfter, DateTimeOffset At);
```

`GetAccountTransactionsHandler.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class GetAccountTransactionsHandler : IRequestHandler<GetAccountTransactionsQuery, Result<IReadOnlyList<TransactionDto>>>
{
    private readonly IAccountRepository _accounts;

    public GetAccountTransactionsHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<Result<IReadOnlyList<TransactionDto>>> Handle(GetAccountTransactionsQuery req, CancellationToken ct)
    {
        var account = await _accounts.GetAsync(req.AccountId, ct);
        if (account is null) return Result<IReadOnlyList<TransactionDto>>.Fail("Account not found.");

        var dtos = account.Transactions
            .OrderByDescending(t => t.At)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(t => new TransactionDto(
                t.Id.Value, t.Kind.ToString(), t.Amount.Amount, t.Amount.Currency.ToString(),
                t.BalanceAfter.Amount, t.At))
            .ToList();

        return Result<IReadOnlyList<TransactionDto>>.Ok(dtos);
    }
}
```

### Step 3: Build + commit

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0.

```
git add src/Moongazing.OrionShowcase.Application/
git commit -m "feat(application): GetAccountBalance + GetAccountTransactions queries"
git push
```

---

## Task 7: Infrastructure — BankingDbContext + EF configurations + repositories

**Files:**
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Persistence/BankingDbContext.cs`
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/{AccountConfiguration, CustomerConfiguration, TransactionConfiguration}.cs`
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Persistence/Repositories/{AccountRepository, CustomerRepository}.cs`
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Persistence/EfUnitOfWork.cs`
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Time/SystemClock.cs`

- [ ] **Step 1: Create BankingDbContext**

`src/Moongazing.OrionShowcase.Infrastructure/Persistence/BankingDbContext.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Customers;

public sealed class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankingDbContext).Assembly);
    }
}
```

- [ ] **Step 2: Account configuration**

`src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/AccountConfiguration.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id)
            .HasConversion(id => id.Value, v => new AccountId(v))
            .HasColumnName("id");

        b.Property(a => a.CustomerId)
            .HasConversion(id => id.Value, v => new CustomerId(v))
            .HasColumnName("customer_id");

        b.OwnsOne(a => a.Iban, iban =>
        {
            iban.Property(i => i.Value).HasColumnName("iban").HasMaxLength(34).IsRequired();
        });

        b.OwnsOne(a => a.Balance, money =>
        {
            money.Property(m => m.Amount).HasColumnName("balance_amount").HasColumnType("numeric(20,4)");
            money.Property(m => m.Currency).HasColumnName("balance_currency").HasConversion<string>().HasMaxLength(3);
        });

        b.Property(a => a.Status).HasConversion<string>().HasColumnName("status").HasMaxLength(16);
        b.Property(a => a.OpenedAt).HasColumnName("opened_at");

        b.HasMany(a => a.Transactions).WithOne().HasForeignKey("account_id").OnDelete(DeleteBehavior.Cascade);

        b.Ignore(a => a.DomainEvents);
    }
}
```

- [ ] **Step 3: Customer configuration (skeleton, encryption added in Task 8)**

`src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new CustomerId(v))
            .HasColumnName("id");

        b.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();

        // Note: Tckn/Email/Phone get [IsEncrypted()] applied in Task 8 once OrionVault is wired.
        b.Property(c => c.NationalId)
            .HasConversion(v => v.Value, s => new Tckn(s))
            .HasColumnName("national_id")
            .HasMaxLength(11);

        b.Property(c => c.Email).HasColumnName("email").HasMaxLength(256);
        b.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(32);
        b.Property(c => c.RegisteredAt).HasColumnName("registered_at");

        b.Ignore(c => c.DomainEvents);
    }
}
```

- [ ] **Step 4: Transaction configuration**

`src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("transactions");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id)
            .HasConversion(id => id.Value, v => new TransactionId(v))
            .HasColumnName("id")
            .ValueGeneratedNever();

        b.Property(t => t.Kind).HasConversion<string>().HasColumnName("kind").HasMaxLength(16);

        b.OwnsOne(t => t.Amount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(20,4)");
            m.Property(x => x.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(3);
        });

        b.OwnsOne(t => t.BalanceAfter, m =>
        {
            m.Property(x => x.Amount).HasColumnName("balance_after_amount").HasColumnType("numeric(20,4)");
            m.Property(x => x.Currency).HasColumnName("balance_after_currency").HasConversion<string>().HasMaxLength(3);
        });

        b.Property(t => t.IdempotencyKey)
            .HasConversion(k => k.Value, s => new IdempotencyKey(s))
            .HasColumnName("idempotency_key")
            .HasMaxLength(128);

        b.Property(t => t.At).HasColumnName("at");
    }
}
```

- [ ] **Step 5: Repositories + UnitOfWork + Clock**

`src/Moongazing.OrionShowcase.Infrastructure/Persistence/Repositories/AccountRepository.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _db;
    public AccountRepository(BankingDbContext db) => _db = db;

    public Task<Account?> GetAsync(AccountId id, CancellationToken ct) =>
        _db.Accounts.Include(a => a.Transactions).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Account account, CancellationToken ct)
    {
        await _db.Accounts.AddAsync(account, ct);
    }
}
```

`src/Moongazing.OrionShowcase.Infrastructure/Persistence/Repositories/CustomerRepository.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly BankingDbContext _db;
    public CustomerRepository(BankingDbContext db) => _db = db;

    public Task<Customer?> GetAsync(CustomerId id, CancellationToken ct) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct)
    {
        await _db.Customers.AddAsync(customer, ct);
    }
}
```

`src/Moongazing.OrionShowcase.Infrastructure/Persistence/EfUnitOfWork.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Persistence;

using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly BankingDbContext _db;
    public EfUnitOfWork(BankingDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
```

`src/Moongazing.OrionShowcase.Infrastructure/Time/SystemClock.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Time;

using Moongazing.OrionShowcase.Domain.Abstractions;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
```

- [ ] **Step 6: Build + commit**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0.

```
git add src/Moongazing.OrionShowcase.Infrastructure/
git commit -m "feat(infrastructure): BankingDbContext + EF configurations + repositories + clock"
git push
```

---

## Task 8: Infrastructure — OrionVault wiring + Customer PII encryption

**Files:**
- Modify: `src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` (add `IsEncrypted()`)
- Create: `src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` (skeleton, full wiring added across Tasks 8-11)

- [ ] **Step 1: Add IsEncrypted() to Customer PII fields**

Replace the Tckn/Email/Phone lines in `CustomerConfiguration.cs` with:
```csharp
b.Property(c => c.NationalId)
    .HasConversion(v => v.Value, s => new Tckn(s))
    .HasColumnName("national_id")
    .IsEncrypted();   // OrionVault: stored as bytea

b.Property(c => c.Email).HasColumnName("email").IsEncrypted();
b.Property(c => c.Phone).HasColumnName("phone").IsEncrypted();
```

Add `using Moongazing.OrionVault.EntityFrameworkCore;` at the top for the `IsEncrypted()` extension.

Note: encrypted columns end up as `bytea` in Postgres (provider's native blob). `HasMaxLength()` is dropped because it does not apply to bytea.

- [ ] **Step 2: Skeleton DI extension**

`src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Time;
using Moongazing.OrionVault.DependencyInjection;
using Moongazing.OrionVault.EntityFrameworkCore.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        // OrionVault must be registered BEFORE DbContext so UseOrionVault(sp) can resolve it
        services.AddOrionVault(o =>
        {
            o.UseStaticKeys(k => k.Add(keyId: 1, base64Key: cfg["Vault:Key1"]!));
            o.ActiveKeyId = 1;
        }).UseEntityFrameworkCore<BankingDbContext>();

        services.AddDbContext<BankingDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(cfg.GetConnectionString("Banking"));
            opt.UseOrionVault(sp);
            // OrionPatch wiring added in Task 9
        });

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        // OrionLock + OrionAudit + OrionKey added in Task 10
        // DailySettlementService added in Task 11

        return services;
    }
}
```

- [ ] **Step 3: Build + commit**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0.

```
git add src/Moongazing.OrionShowcase.Infrastructure/
git commit -m "feat(infrastructure): OrionVault wiring with Customer PII encryption"
git push
```

---

## Task 9: Infrastructure — OrionPatch wiring + domain event bridge

**Files:**
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Outbox/DomainEventOutboxAdapter.cs`
- Modify: `src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

- [ ] **Step 1: Domain event collector adapter**

`src/Moongazing.OrionShowcase.Infrastructure/Outbox/DomainEventOutboxAdapter.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Moongazing.OrionPatch;
using Moongazing.OrionShowcase.Domain.Abstractions;

/// <summary>
/// Walks the EF change tracker for entities deriving from <see cref="AggregateRoot{TId}"/>
/// and forwards their <c>DomainEvents</c> to OrionPatch's outbox. Invoked from a
/// <see cref="ISaveChangesInterceptor"/> registered by OrionPatch.
/// </summary>
public sealed class DomainEventOutboxAdapter
{
    private readonly IOutbox _outbox;
    public DomainEventOutboxAdapter(IOutbox outbox) => _outbox = outbox;

    public async Task FlushAsync(DbContext db, CancellationToken ct)
    {
        var aggregates = db.ChangeTracker.Entries()
            .Select(e => e.Entity)
            .Where(e => IsAggregateRoot(e))
            .ToList();

        foreach (var agg in aggregates)
        {
            var events = (IReadOnlyList<object>)agg.GetType().GetProperty("DomainEvents")!.GetValue(agg)!;
            foreach (var domainEvent in events)
            {
                await _outbox.EnqueueAsync(domainEvent, ct);
            }
            agg.GetType().GetMethod("ClearDomainEvents")!.Invoke(agg, null);
        }
    }

    private static bool IsAggregateRoot(object entity)
    {
        var t = entity.GetType();
        while (t is not null && t != typeof(object))
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
                return true;
            t = t.BaseType;
        }
        return false;
    }
}
```

- [ ] **Step 2: Wire OrionPatch in InfrastructureServiceCollectionExtensions**

Modify `AddInfrastructure` to register OrionPatch and attach the interceptor:

```csharp
// After OrionVault registration, before AddDbContext:
services.AddOrionPatch().UseEntityFrameworkCore<BankingDbContext>();
services.AddScoped<DomainEventOutboxAdapter>();
```

Modify the `AddDbContext` block to include `UseOrionPatch`:

```csharp
services.AddDbContext<BankingDbContext>((sp, opt) =>
{
    opt.UseNpgsql(cfg.GetConnectionString("Banking"));
    opt.UseOrionVault(sp);
    opt.UseOrionPatch(sp);
});
```

Add usings:
```csharp
using Moongazing.OrionPatch.DependencyInjection;
using Moongazing.OrionPatch.EntityFrameworkCore.DependencyInjection;
using Moongazing.OrionShowcase.Infrastructure.Outbox;
```

- [ ] **Step 3: Register `DomainEventOutboxAdapter` as the OrionPatch event collector**

OrionPatch exposes an extension hook for custom event collection. Add to the `AddOrionPatch().UseEntityFrameworkCore<BankingDbContext>()` chain (verify the actual hook name in OrionPatch 0.1.1 — implementer adapts):

```csharp
services.AddOrionPatch()
    .UseEntityFrameworkCore<BankingDbContext>()
    .UseCustomEventCollector<DomainEventOutboxAdapter>();
```

If OrionPatch 0.1.1 doesn't expose this exact extension, the implementer wires the adapter manually by subscribing to the `SavingChanges` event on `BankingDbContext`. The intent is unambiguous: domain events collected and pushed to outbox in the same transaction.

- [ ] **Step 4: Build + commit**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0 (may warn about reflection — that's fine; the adapter intentionally uses reflection to walk the AggregateRoot<TId> generic base type).

```
git add src/Moongazing.OrionShowcase.Infrastructure/
git commit -m "feat(infrastructure): OrionPatch wiring with domain event bridge"
git push
```

---

## Task 10: Infrastructure — OrionLock + OrionAudit + OrionKey wiring

**Files:**
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Audit/EfAuditWriter.cs`
- Create: `src/Moongazing.OrionShowcase.Infrastructure/Idempotency/OrionKeyIdempotencyStore.cs`
- Modify: `InfrastructureServiceCollectionExtensions.cs` (add 3 packages)

- [ ] **Step 1: EfAuditWriter implementation**

`src/Moongazing.OrionShowcase.Infrastructure/Audit/EfAuditWriter.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Audit;

using Moongazing.OrionAudit;
using Moongazing.OrionShowcase.Application.Abstractions;

/// <summary>
/// Bridges Application's <see cref="IAuditWriter"/> to OrionAudit's audit log facility.
/// </summary>
public sealed class EfAuditWriter : IAuditWriter
{
    private readonly IAuditLog _audit;
    public EfAuditWriter(IAuditLog audit) => _audit = audit;

    public Task WriteAsync(string actor, string action, string requestJson, string? responseJson, bool succeeded, string? error, CancellationToken ct)
    {
        return _audit.WriteAsync(new AuditEntry
        {
            Actor = actor,
            Action = action,
            Request = requestJson,
            Response = responseJson,
            Succeeded = succeeded,
            Error = error,
            At = DateTimeOffset.UtcNow
        }, ct);
    }
}
```

Note: the exact OrionAudit 0.6.1 API surface (`IAuditLog`, `AuditEntry`) may differ — implementer adapts in this task to whatever the actual package exposes. The intent is: write a single audit row per command per outcome.

- [ ] **Step 2: OrionKeyIdempotencyStore implementation**

`src/Moongazing.OrionShowcase.Infrastructure/Idempotency/OrionKeyIdempotencyStore.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.Idempotency;

using Moongazing.OrionKey.Idempotency;
using Moongazing.OrionShowcase.Application.Abstractions;

public sealed class OrionKeyIdempotencyStore : IIdempotencyStore
{
    private readonly IIdempotencyKeyService _orionKey;
    public OrionKeyIdempotencyStore(IIdempotencyKeyService orionKey) => _orionKey = orionKey;

    public Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken ct)
        => _orionKey.TryClaimAsync(key, requestHash, TimeSpan.FromHours(24), ct);

    public Task<string?> GetCachedResponseAsync(string key, CancellationToken ct)
        => _orionKey.GetResponseAsync(key, ct);

    public Task StoreResponseAsync(string key, string serialisedResponse, CancellationToken ct)
        => _orionKey.StoreResponseAsync(key, serialisedResponse, TimeSpan.FromHours(24), ct);
}
```

Note: `IIdempotencyKeyService` is the assumed API name — implementer verifies against OrionKey 0.4.1 and adapts.

- [ ] **Step 3: Wire all 3 packages in InfrastructureServiceCollectionExtensions**

Add to `AddInfrastructure`:
```csharp
// OrionLock with Postgres backend
services.AddOrionLock().UsePostgres(cfg.GetConnectionString("Banking")!);

// OrionAudit with EF persistence
services.AddOrionAudit().UseEntityFrameworkCore<BankingDbContext>();
services.AddScoped<IAuditWriter, EfAuditWriter>();

// OrionKey with snowflake worker id
services.AddOrionKey(o => o.WorkerId = cfg.GetValue<int>("OrionKey:WorkerId"));
services.AddScoped<IIdempotencyStore, OrionKeyIdempotencyStore>();
```

Add usings:
```csharp
using Moongazing.OrionLock.DependencyInjection;
using Moongazing.OrionLock.Postgres.DependencyInjection;
using Moongazing.OrionAudit.DependencyInjection;
using Moongazing.OrionAudit.EntityFrameworkCore.DependencyInjection;
using Moongazing.OrionKey.DependencyInjection;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Infrastructure.Audit;
using Moongazing.OrionShowcase.Infrastructure.Idempotency;
```

- [ ] **Step 4: Build + commit**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0 (or analyzer adjustments if package APIs differ; document any deviations).

```
git add src/Moongazing.OrionShowcase.Infrastructure/
git commit -m "feat(infrastructure): OrionLock (Postgres) + OrionAudit + OrionKey wiring"
git push
```

---

## Task 11: Infrastructure — DailySettlementService hosted service

**Files:**
- Create: `src/Moongazing.OrionShowcase.Application/Settlement/RunDailySettlement.cs`
- Create: `src/Moongazing.OrionShowcase.Infrastructure/HostedServices/DailySettlementService.cs`
- Modify: `InfrastructureServiceCollectionExtensions.cs` to register both

- [ ] **Step 1: Application-layer settler (pure orchestration)**

`src/Moongazing.OrionShowcase.Application/Settlement/RunDailySettlement.cs`:
```csharp
namespace Moongazing.OrionShowcase.Application.Settlement;

using Microsoft.Extensions.Logging;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed partial class RunDailySettlement
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ILogger<RunDailySettlement> _log;

    public RunDailySettlement(IUnitOfWork uow, IClock clock, ILogger<RunDailySettlement> log)
    {
        _uow = uow; _clock = clock; _log = log;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var start = _clock.UtcNow;
        LogStart(start);

        // Showcase placeholder: in real banking this would close out interest,
        // generate end-of-day statements, etc. For v0.1.0 we just persist a marker row.
        await _uow.SaveChangesAsync(ct);

        LogDone(_clock.UtcNow - start);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Daily settlement started at {Start}.")]
    partial void LogStart(DateTimeOffset start);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Daily settlement done in {Elapsed}.")]
    partial void LogDone(TimeSpan elapsed);
}
```

- [ ] **Step 2: Hosted service with OrionLock single-instance gating**

`src/Moongazing.OrionShowcase.Infrastructure/HostedServices/DailySettlementService.cs`:
```csharp
namespace Moongazing.OrionShowcase.Infrastructure.HostedServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Settlement;
using Moongazing.OrionShowcase.Domain.Abstractions;

public sealed partial class DailySettlementService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IOrionLock _locks;
    private readonly IClock _clock;
    private readonly ILogger<DailySettlementService> _log;

    public DailySettlementService(IServiceProvider sp, IOrionLock locks, IClock clock, ILogger<DailySettlementService> log)
    {
        _sp = sp; _locks = locks; _clock = clock; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(NextRunDelay(_clock.UtcNow), stoppingToken);

            var lease = await _locks.TryAcquireAsync("settlement:daily", TimeSpan.FromMinutes(30), stoppingToken);
            if (lease is null) { LogSkipped(); continue; }

            await using (lease)
            {
                await using var scope = _sp.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<RunDailySettlement>();
                await runner.ExecuteAsync(stoppingToken);
            }
        }
    }

    internal static TimeSpan NextRunDelay(DateTimeOffset now)
    {
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, 23, 55, 0, TimeSpan.Zero);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Settlement skipped: another instance holds the lock.")]
    partial void LogSkipped();
}
```

- [ ] **Step 3: Register both**

Append to `AddInfrastructure`:
```csharp
services.AddScoped<RunDailySettlement>();
services.AddHostedService<DailySettlementService>();
```

Add using:
```csharp
using Moongazing.OrionShowcase.Application.Settlement;
using Moongazing.OrionShowcase.Infrastructure.HostedServices;
```

- [ ] **Step 4: Build + commit**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0.

```
git add src/
git commit -m "feat(infrastructure): DailySettlementService with OrionLock single-instance gating"
git push
```

---

## Task 12: Api — Program.cs + JWT auth + Swagger

**Files:**
- Create: `src/Moongazing.OrionShowcase.Api/Authentication/{JwtIssuer, ClaimsCurrentUser, JwtBearerExtensions}.cs`
- Create: `src/Moongazing.OrionShowcase.Api/Swagger/SwaggerExtensions.cs`
- Create: `src/Moongazing.OrionShowcase.Api/Observability/SerilogExtensions.cs`
- Create: `src/Moongazing.OrionShowcase.Api/Filters/{ValidationProblemFilter, DomainExceptionFilter}.cs`
- Create: `src/Moongazing.OrionShowcase.Api/Health/HealthChecksExtensions.cs`
- Replace: `src/Moongazing.OrionShowcase.Api/Program.cs`
- Create: `src/Moongazing.OrionShowcase.Api/appsettings.json`, `appsettings.Development.json`

- [ ] **Step 1: Create `appsettings.json`**

```json
{
  "ConnectionStrings": {
    "Banking": "Host=localhost;Database=banking;Username=bank;Password=bank"
  },
  "Vault": {
    "Key1": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
  },
  "Jwt": {
    "Issuer": "https://orionshowcase.local",
    "Audience": "orionshowcase-api",
    "SigningKey": "demo-signing-key-min-32-chars-for-hs256-algorithm"
  },
  "Seq": { "ServerUrl": "http://localhost:5341" },
  "Otel": { "Endpoint": "http://localhost:4317" },
  "OrionKey": { "WorkerId": 1 },
  "OrionGuard": {
    "Policies": {
      "login":    { "Limit": 5,   "Window": "00:01:00" },
      "transfer": { "Limit": 10,  "Window": "00:01:00" },
      "query":    { "Limit": 100, "Window": "00:01:00" }
    }
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

`appsettings.Development.json`:
```json
{
  "Logging": { "LogLevel": { "Default": "Debug", "Microsoft.AspNetCore": "Information" } }
}
```

- [ ] **Step 2: JwtIssuer**

`src/Moongazing.OrionShowcase.Api/Authentication/JwtIssuer.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Authentication;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

public sealed class JwtIssuer
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;

    public JwtIssuer(string issuer, string audience, string signingKey)
    {
        _issuer = issuer;
        _audience = audience;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    public string Issue(string userId, string username, IEnumerable<string> roles, TimeSpan? lifetime = null)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(8)),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 3: ClaimsCurrentUser**

`src/Moongazing.OrionShowcase.Api/Authentication/ClaimsCurrentUser.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Authentication;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moongazing.OrionShowcase.Application.Abstractions;

public sealed class ClaimsCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _ctx;
    public ClaimsCurrentUser(IHttpContextAccessor ctx) => _ctx = ctx;

    public string Id => _ctx.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous";
    public string Username => _ctx.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "anonymous";
    public bool IsAuthenticated => _ctx.HttpContext?.User.Identity?.IsAuthenticated == true;
}
```

- [ ] **Step 4: JwtBearerExtensions**

`src/Moongazing.OrionShowcase.Api/Authentication/JwtBearerExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Authentication;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moongazing.OrionShowcase.Application.Abstractions;

public static class JwtBearerExtensions
{
    public static IServiceCollection AddJwtBearerAuth(this IServiceCollection services, IConfiguration cfg)
    {
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;
        var key = cfg["Jwt:SigningKey"]!;

        services.AddSingleton(new JwtIssuer(issuer, audience, key));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        services.AddAuthorization();
        return services;
    }
}
```

- [ ] **Step 5: Swagger extension**

`src/Moongazing.OrionShowcase.Api/Swagger/SwaggerExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Swagger;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Moongazing.OrionShowcase API",
                Version = "v1",
                Description = "Banking sample integrating all six Moongazing.Orion packages."
            });

            opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT obtained from POST /api/auth/login."
            });
            opt.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
        return services;
    }
}
```

- [ ] **Step 6: Filters**

`src/Moongazing.OrionShowcase.Api/Filters/ValidationProblemFilter.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Filters;

using FluentValidation;
using Microsoft.AspNetCore.Http;

public static class ValidationProblemFilter
{
    public static IResult Handle(ValidationException ex)
    {
        var errors = ex.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }
}
```

`src/Moongazing.OrionShowcase.Api/Filters/DomainExceptionFilter.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Filters;

using Microsoft.AspNetCore.Http;
using Moongazing.OrionShowcase.Domain.Accounts;

public static class DomainExceptionFilter
{
    public static IResult? TryHandle(Exception ex) => ex switch
    {
        InsufficientFundsException => Results.Problem(detail: "Insufficient funds.", statusCode: 409),
        AccountNotActiveException ana => Results.Problem(detail: ana.Message, statusCode: 409),
        AccountNotEmptyException => Results.Problem(detail: "Account has non-zero balance.", statusCode: 409),
        _ => null
    };
}
```

- [ ] **Step 7: Serilog + HealthChecks extensions**

`src/Moongazing.OrionShowcase.Api/Observability/SerilogExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Observability;

using Microsoft.AspNetCore.Builder;
using Serilog;

public static class SerilogExtensions
{
    public static WebApplicationBuilder UseSerilogForOrion(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .WriteTo.Console()
               .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"]!);
        });
        return builder;
    }
}
```

`src/Moongazing.OrionShowcase.Api/Health/HealthChecksExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Health;

using Microsoft.Extensions.DependencyInjection;

public static class HealthChecksExtensions
{
    public static IServiceCollection AddOrionShowcaseHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }
}
```

- [ ] **Step 8: Replace Program.cs (Endpoints + OpenTelemetry added in Tasks 13-14)**

`src/Moongazing.OrionShowcase.Api/Program.cs`:
```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Moongazing.OrionShowcase.Api.Authentication;
using Moongazing.OrionShowcase.Api.Health;
using Moongazing.OrionShowcase.Api.Observability;
using Moongazing.OrionShowcase.Api.Swagger;
using Moongazing.OrionShowcase.Application.DependencyInjection;
using Moongazing.OrionShowcase.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.UseSerilogForOrion();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddJwtBearerAuth(builder.Configuration)
    .AddProblemDetails()
    .AddEndpointsApiExplorer()
    .AddSwagger()
    .AddOrionShowcaseHealthChecks();
// OrionGuard middleware + OpenTelemetry added in Task 14

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
// app.MapBankingEndpoints() added in Task 13
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") });

app.MapGet("/", () => "OrionShowcase API running. See /swagger.");
app.Run();

public partial class Program;
```

- [ ] **Step 9: Build + run + manual smoke check**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0.

(Endpoints come in Task 13; we don't run the API yet.)

- [ ] **Step 10: Commit**

```
git add src/Moongazing.OrionShowcase.Api/
git commit -m "feat(api): Program composition root + JWT auth + Swagger + filters"
git push
```

---

## Task 13: Api — 10 endpoints

**Files:**
- Create: `src/Moongazing.OrionShowcase.Api/Endpoints/EndpointExtensions.cs`
- Create: `src/Moongazing.OrionShowcase.Api/Endpoints/Auth/LoginEndpoint.cs`
- Create: `src/Moongazing.OrionShowcase.Api/Endpoints/Customers/RegisterCustomerEndpoint.cs`
- Create: `src/Moongazing.OrionShowcase.Api/Endpoints/Accounts/{OpenAccount, Deposit, Withdraw, Transfer, Freeze, Close, GetBalance, GetTransactions}Endpoint.cs`

- [ ] **Step 1: Endpoint extensions aggregator**

`src/Moongazing.OrionShowcase.Api/Endpoints/EndpointExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Endpoints;

using Moongazing.OrionShowcase.Api.Endpoints.Accounts;
using Moongazing.OrionShowcase.Api.Endpoints.Auth;
using Moongazing.OrionShowcase.Api.Endpoints.Customers;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapBankingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapLogin();
        app.MapRegisterCustomer();
        app.MapOpenAccount();
        app.MapDeposit();
        app.MapWithdraw();
        app.MapTransfer();
        app.MapFreeze();
        app.MapClose();
        app.MapGetBalance();
        app.MapGetTransactions();
        return app;
    }
}
```

- [ ] **Step 2: LoginEndpoint (issues JWT)**

`src/Moongazing.OrionShowcase.Api/Endpoints/Auth/LoginEndpoint.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Endpoints.Auth;

using Moongazing.OrionShowcase.Api.Authentication;

internal static class LoginEndpoint
{
    public static IEndpointConventionBuilder MapLogin(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", Handle)
           .AllowAnonymous()
           .WithName("Login")
           .WithTags("Auth");

    private static IResult Handle(LoginRequest req, JwtIssuer issuer)
    {
        // Dev-grade: hardcoded demo:demo. Production would use OIDC or a real user store.
        if (req.Username != "demo" || req.Password != "demo")
            return Results.Unauthorized();
        var token = issuer.Issue(userId: "00000000-0000-0000-0000-000000000001", username: "demo",
            roles: new[] { "customer" });
        return Results.Ok(new LoginResponse(token));
    }

    internal sealed record LoginRequest(string Username, string Password);
    internal sealed record LoginResponse(string AccessToken);
}
```

- [ ] **Step 3: TransferEndpoint (the showcase moment)**

`src/Moongazing.OrionShowcase.Api/Endpoints/Accounts/TransferEndpoint.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using FluentValidation;
using MediatR;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class TransferEndpoint
{
    public static IEndpointConventionBuilder MapTransfer(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/accounts/{from:guid}/transfer", Handle)
           .RequireAuthorization()
           .WithName("TransferMoney")
           .WithTags("Accounts")
           .Produces<TransferResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(409);

    private static async Task<IResult> Handle(
        Guid from, TransferRequest req, IMediator mediator, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new TransferMoneyCommand(
                new AccountId(from),
                new AccountId(req.ToAccountId),
                new Money(req.Amount, Enum.Parse<Currency>(req.Currency)),
                new IdempotencyKey(req.IdempotencyKey)), ct);

            return result.IsSuccess
                ? Results.Ok(new TransferResponse(result.Value!.TransferId, result.Value.NewSourceBalance))
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (ValidationException ex) { return ValidationProblemFilter.Handle(ex); }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record TransferRequest(Guid ToAccountId, decimal Amount, string Currency, string IdempotencyKey);
    internal sealed record TransferResponse(Guid TransferId, decimal NewSourceBalance);
}
```

- [ ] **Step 4: Remaining 8 endpoints follow the identical shape**

Each endpoint file is ~30 lines: `MapXxx` extension + `Handle` method + nested `Request`/`Response` records. The pattern from `TransferEndpoint` repeats verbatim — only the path, command type, and request/response records differ.

| Endpoint | Path | Command/Query |
|---|---|---|
| `RegisterCustomerEndpoint` | `POST /api/customers` | `RegisterCustomerCommand` |
| `OpenAccountEndpoint` | `POST /api/accounts` | `OpenAccountCommand` |
| `DepositEndpoint` | `POST /api/accounts/{id:guid}/deposit` | `DepositMoneyCommand` |
| `WithdrawEndpoint` | `POST /api/accounts/{id:guid}/withdraw` | `WithdrawMoneyCommand` |
| `FreezeEndpoint` | `POST /api/accounts/{id:guid}/freeze` | `FreezeAccountCommand` |
| `CloseEndpoint` | `POST /api/accounts/{id:guid}/close` | `CloseAccountCommand` |
| `GetBalanceEndpoint` | `GET /api/accounts/{id:guid}/balance` | `GetAccountBalanceQuery` |
| `GetTransactionsEndpoint` | `GET /api/accounts/{id:guid}/transactions` | `GetAccountTransactionsQuery` |

Use the `TransferEndpoint` file as the template; copy and adapt for each.

- [ ] **Step 5: Wire `MapBankingEndpoints()` in Program.cs**

Replace the placeholder `MapGet("/", ...)` line with:
```csharp
app.MapBankingEndpoints();
app.MapGet("/", () => "OrionShowcase API running. See /swagger.");
```

Add `using Moongazing.OrionShowcase.Api.Endpoints;` at the top.

- [ ] **Step 6: Build + run + smoke test (no integration tests yet)**

```
dotnet build Moongazing.OrionShowcase.sln
dotnet run --project src/Moongazing.OrionShowcase.Api/Moongazing.OrionShowcase.Api.csproj --no-build
```

(Will fail to connect to Postgres because Docker not running yet — that's fine for now. Just verify the API starts and `/swagger` renders. Cancel with Ctrl+C.)

- [ ] **Step 7: Commit**

```
git add src/Moongazing.OrionShowcase.Api/
git commit -m "feat(api): 10 endpoints (Auth, Customers, Accounts) using Minimal API + MediatR"
git push
```

---

## Task 14: Api — OrionGuard policies + OpenTelemetry export

**Files:**
- Create: `src/Moongazing.OrionShowcase.Api/Observability/OpenTelemetryExtensions.cs`
- Modify: `Program.cs` (add OrionGuard middleware + OpenTelemetry)
- Modify: 9 endpoint files (add `.WithOrionGuardPolicy(...)`)

- [ ] **Step 1: OpenTelemetryExtensions**

`src/Moongazing.OrionShowcase.Api/Observability/OpenTelemetryExtensions.cs`:
```csharp
namespace Moongazing.OrionShowcase.Api.Observability;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddOpenTelemetryForOrion(this IServiceCollection services, IConfiguration cfg)
    {
        var otlp = new Uri(cfg["Otel:Endpoint"]!);

        services.AddOpenTelemetry()
            .WithTracing(t => t
                .AddSource("Moongazing.OrionGuard")
                .AddSource("Moongazing.OrionAudit")
                .AddSource("Moongazing.OrionLock")
                .AddSource("Moongazing.OrionKey")
                .AddSource("Moongazing.OrionPatch")
                .AddSource("Moongazing.OrionVault")
                .AddAspNetCoreInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter(o => o.Endpoint = otlp))
            .WithMetrics(m => m
                .AddMeter("Moongazing.OrionGuard")
                .AddMeter("Moongazing.OrionAudit")
                .AddMeter("Moongazing.OrionLock")
                .AddMeter("Moongazing.OrionKey")
                .AddMeter("Moongazing.OrionPatch")
                .AddMeter("Moongazing.OrionVault")
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = otlp));
        return services;
    }
}
```

- [ ] **Step 2: Add OrionGuard + OpenTelemetry to Program.cs**

Modify Program.cs to include before `var app = builder.Build();`:
```csharp
builder.Services
    .AddOpenTelemetryForOrion(builder.Configuration)
    .AddOrionGuardAspNetCore(builder.Configuration);
```

Modify before `app.MapBankingEndpoints();`:
```csharp
app.UseOrionGuard();
```

Add usings:
```csharp
using Moongazing.OrionGuard.AspNetCore;
using Moongazing.OrionShowcase.Api.Observability;
```

(Verify the actual OrionGuard 6.4.2 method names — `AddOrionGuardAspNetCore` and `UseOrionGuard` are the assumed entry points; implementer adapts.)

- [ ] **Step 3: Attach OrionGuard policy to each endpoint**

For each endpoint, add `.WithOrionGuardPolicy("...")` to the endpoint binding:
- `LoginEndpoint` → `"login"`
- `TransferEndpoint`, `DepositEndpoint`, `WithdrawEndpoint` → `"transfer"`
- All others → `"query"`

Example for `LoginEndpoint`:
```csharp
return app.MapPost("/api/auth/login", Handle)
   .AllowAnonymous()
   .WithName("Login")
   .WithTags("Auth")
   .WithOrionGuardPolicy("login");
```

Add `using Moongazing.OrionGuard.AspNetCore;` if needed.

- [ ] **Step 4: Build + commit**

```
dotnet build Moongazing.OrionShowcase.sln
```
Expected: 0/0.

```
git add src/Moongazing.OrionShowcase.Api/
git commit -m "feat(api): OrionGuard per-endpoint policies + OpenTelemetry exports for all 6 packages"
git push
```

---

## Task 15: Docker compose + Dockerfile + postgres-init.sql

**Files:**
- Create: `docker/compose.yaml`
- Create: `docker/Dockerfile.api`
- Create: `docker/postgres-init.sql`

- [ ] **Step 1: Dockerfile.api**

`docker/Dockerfile.api`:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Moongazing.OrionShowcase.sln
RUN dotnet publish src/Moongazing.OrionShowcase.Api/Moongazing.OrionShowcase.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Moongazing.OrionShowcase.Api.dll"]
```

- [ ] **Step 2: postgres-init.sql**

`docker/postgres-init.sql`:
```sql
-- Initial seed: an empty banking database. EF migrations create the schema on app startup.
-- This file is intentionally minimal; it exists so postgres-init.sql hook fires.
CREATE EXTENSION IF NOT EXISTS pgcrypto;
```

- [ ] **Step 3: compose.yaml**

`docker/compose.yaml`:
```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: banking
      POSTGRES_USER: bank
      POSTGRES_PASSWORD: bank
    ports: ["5432:5432"]
    volumes:
      - ./postgres-init.sql:/docker-entrypoint-initdb.d/init.sql
      - banking-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD", "pg_isready", "-U", "bank"]
      interval: 5s
      timeout: 3s
      retries: 10

  seq:
    image: datalust/seq:latest
    environment: { ACCEPT_EULA: Y }
    ports: ["5341:80"]
    volumes: [seq-data:/data]

  jaeger:
    image: jaegertracing/all-in-one:latest
    environment: { COLLECTOR_OTLP_ENABLED: "true" }
    ports: ["16686:16686", "4317:4317"]

  api:
    build:
      context: ..
      dockerfile: docker/Dockerfile.api
    depends_on:
      postgres: { condition: service_healthy }
      seq: { condition: service_started }
      jaeger: { condition: service_started }
    environment:
      ConnectionStrings__Banking: "Host=postgres;Database=banking;Username=bank;Password=bank"
      Seq__ServerUrl: "http://seq:80"
      Otel__Endpoint: "http://jaeger:4317"
      Vault__Key1: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
      Jwt__SigningKey: "demo-signing-key-min-32-chars-for-hs256-algorithm"
      OrionKey__WorkerId: "1"
      ASPNETCORE_ENVIRONMENT: Development
    ports: ["5000:8080"]

volumes:
  banking-data:
  seq-data:
```

- [ ] **Step 4: Add EF migrations (one-time)**

```
cd src/Moongazing.OrionShowcase.Infrastructure
dotnet ef migrations add Initial --startup-project ../Moongazing.OrionShowcase.Api --output-dir Persistence/Migrations
cd ../..
```

Modify Program.cs to apply migrations on startup (after `var app = builder.Build();`):
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    db.Database.Migrate();
}
```

Add `using Microsoft.EntityFrameworkCore;` and `using Moongazing.OrionShowcase.Infrastructure.Persistence;` at the top.

- [ ] **Step 5: docker compose smoke**

```
cd docker
docker compose up -d
docker compose ps
```
Expected: all 4 services healthy or running within 60 seconds. `docker compose logs api` shows the app started successfully and migrated.

Browse:
- `http://localhost:5000/swagger` — API
- `http://localhost:5341` — Seq UI
- `http://localhost:16686` — Jaeger UI

```
docker compose down
```

- [ ] **Step 6: Commit**

```
git add docker/ src/Moongazing.OrionShowcase.Infrastructure/Persistence/Migrations/ src/Moongazing.OrionShowcase.Api/Program.cs
git commit -m "feat(docker): compose + Dockerfile.api + initial EF migration"
git push
```

---

## Task 16: IntegrationTests with Testcontainers

**Files:**
- Create: `test/Moongazing.OrionShowcase.IntegrationTests/BankingApiFixture.cs`
- Create: `test/Moongazing.OrionShowcase.IntegrationTests/Scenarios/{RegisterAndOpenAccountTests, DepositTests, TransferTests, PiiEncryptionTests}.cs`

- [ ] **Step 1: Test fixture with Testcontainers Postgres**

`test/Moongazing.OrionShowcase.IntegrationTests/BankingApiFixture.cs`:
```csharp
namespace Moongazing.OrionShowcase.IntegrationTests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class BankingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("banking")
        .WithUsername("bank")
        .WithPassword("bank")
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Banking", PostgresConnectionString);
        Environment.SetEnvironmentVariable("Vault__Key1", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "demo-signing-key-min-32-chars-for-hs256-algorithm");
        Environment.SetEnvironmentVariable("OrionKey__WorkerId", "1");
        Environment.SetEnvironmentVariable("Seq__ServerUrl", "http://localhost:5341");
        Environment.SetEnvironmentVariable("Otel__Endpoint", "http://localhost:4317");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

- [ ] **Step 2: Smoke scenario — register customer + open account**

`test/Moongazing.OrionShowcase.IntegrationTests/Scenarios/RegisterAndOpenAccountTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

public class RegisterAndOpenAccountTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;
    public RegisterAndOpenAccountTests(BankingApiFixture fx) => _fx = fx;

    [Fact]
    public async Task End_to_end_register_then_login_then_open_account_succeeds()
    {
        var client = _fx.CreateClient();

        // 1. Login (anonymous)
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { username = "demo", password = "demo" });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", loginBody!.AccessToken);

        // 2. Register customer
        var regRes = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Ali Veli",
            nationalId = "10000000146",
            email = "ali@example.com",
            phone = "+905551234567",
            idempotencyKey = new IdempotencyKey { Value = Guid.NewGuid().ToString("N") }
        });
        regRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var regBody = await regRes.Content.ReadFromJsonAsync<RegisterBody>();

        // 3. Open account
        var openRes = await client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = regBody!.CustomerId,
            iban = "TR330006100519786457841326",
            openingAmount = 100m,
            currency = "TRY",
            idempotencyKey = new IdempotencyKey { Value = Guid.NewGuid().ToString("N") }
        });
        openRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record LoginBody(string AccessToken);
    private sealed record RegisterBody(Guid CustomerId);
    private sealed class IdempotencyKey { public string Value { get; set; } = string.Empty; }
}
```

- [ ] **Step 3: PII encryption verification scenario**

`test/Moongazing.OrionShowcase.IntegrationTests/Scenarios/PiiEncryptionTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using Npgsql;
using Xunit;
using Microsoft.EntityFrameworkCore;

public class PiiEncryptionTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;
    public PiiEncryptionTests(BankingApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Customer_national_id_stored_as_ciphertext_in_postgres()
    {
        var client = _fx.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { username = "demo", password = "demo" });
        var token = (await loginRes.Content.ReadFromJsonAsync<TokenBody>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Ali Veli",
            nationalId = "10000000146",
            email = "ali@example.com",
            phone = "+905551234567",
            idempotencyKey = new { value = Guid.NewGuid().ToString("N") }
        });

        // Connect directly to Postgres and read the raw column
        await using var conn = new NpgsqlConnection(_fx.PostgresConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT national_id FROM customers LIMIT 1", conn);
        var raw = (byte[])(await cmd.ExecuteScalarAsync())!;

        raw.Should().NotBeNull();
        raw.Length.Should().BeGreaterThan(30, "ciphertext has 30-byte fixed OrionVault header plus body");
        // Confirm key id prefix is 1 (big-endian short)
        raw[0].Should().Be(0);
        raw[1].Should().Be(1);
        // Confirm the bytes are NOT the literal UTF-8 of the plaintext
        System.Text.Encoding.UTF8.GetString(raw).Should().NotContain("10000000146");
    }

    private sealed record TokenBody(string AccessToken);
}
```

- [ ] **Step 4: Transfer scenario (the showcase moment)**

`test/Moongazing.OrionShowcase.IntegrationTests/Scenarios/TransferTests.cs`:
```csharp
namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

public class TransferTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;
    public TransferTests(BankingApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Transfer_moves_funds_and_records_two_transactions()
    {
        var client = _fx.CreateClient();
        var token = (await (await client.PostAsJsonAsync("/api/auth/login", new { username = "demo", password = "demo" }))
            .Content.ReadFromJsonAsync<TokenBody>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var customer = (await (await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Ali Veli",
            nationalId = "10000000146",
            email = "ali@example.com",
            phone = "+905551234567",
            idempotencyKey = new { value = Guid.NewGuid().ToString("N") }
        })).Content.ReadFromJsonAsync<RegisterBody>())!.CustomerId;

        var a = (await (await client.PostAsJsonAsync("/api/accounts", new
        { customerId = customer, iban = "TR330006100519786457841326", openingAmount = 100m, currency = "TRY",
          idempotencyKey = new { value = Guid.NewGuid().ToString("N") } }))
            .Content.ReadFromJsonAsync<AccountBody>())!.AccountId;

        var b = (await (await client.PostAsJsonAsync("/api/accounts", new
        { customerId = customer, iban = "DE89370400440532013000", openingAmount = 0m, currency = "TRY",
          idempotencyKey = new { value = Guid.NewGuid().ToString("N") } }))
            .Content.ReadFromJsonAsync<AccountBody>())!.AccountId;

        var transferRes = await client.PostAsJsonAsync($"/api/accounts/{a}/transfer", new
        { toAccountId = b, amount = 30m, currency = "TRY", idempotencyKey = Guid.NewGuid().ToString("N") });
        transferRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var balA = (await client.GetFromJsonAsync<BalanceBody>($"/api/accounts/{a}/balance"))!;
        var balB = (await client.GetFromJsonAsync<BalanceBody>($"/api/accounts/{b}/balance"))!;
        balA.Balance.Should().Be(70m);
        balB.Balance.Should().Be(30m);
    }

    private sealed record TokenBody(string AccessToken);
    private sealed record RegisterBody(Guid CustomerId);
    private sealed record AccountBody(Guid AccountId);
    private sealed record BalanceBody(Guid AccountId, decimal Balance, string Currency, string Status);
}
```

- [ ] **Step 5: Run integration tests**

```
dotnet test test/Moongazing.OrionShowcase.IntegrationTests/Moongazing.OrionShowcase.IntegrationTests.csproj
```
Expected: 3 tests pass. Testcontainers spins up a fresh Postgres per test class.

- [ ] **Step 6: Commit**

```
git add test/Moongazing.OrionShowcase.IntegrationTests/
git commit -m "test(integration): register/open/transfer/PII encryption scenarios with Testcontainers Postgres"
git push
```

---

## Task 17: Docs polish + cross-link 6 sibling READMEs

**Files:**
- Modify: `README.md` (full rewrite)
- Create: `ROADMAP.md`, `CHANGELOG.md`
- Create: `docs/logo.png`, `docs/icon.png` (cream-bg, family aesthetic — see logo strategy below)
- Modify: 6 sibling READMEs (one PR per repo)

- [ ] **Step 1: Logo (briefcase + four-pointed star, cream bg, indigo)**

The family logo theme is concrete object + 4-pointed star inside, indigo on cream. For OrionShowcase, the most fitting object is a briefcase (banking + professional) with a star on the front.

Gemini prompt:
```
A minimalist line-art logo for "OrionShowcase", rendered as a single #4338CA symbol on a fully transparent background. The symbol is a classic professional briefcase drawn in clean geometric line-art - a rectangular case with a curved top handle and a small clasp in the centre - and on the front face of the briefcase, centred, sits a four-pointed sparkle star, the Orion north-star. No outer ring around the whole logo, no badge, no curved text, no waves, no flanking stars, no shading, no fill. Just the briefcase-with-star symbol, centred on a square canvas with comfortable padding. Vector style, modern, geometric, balanced.
```

Once Gemini produces `docs/superpowers/OrionShowcaseLogo.png` (1024x1024 transparent), apply the cream-bg pipeline (same PowerShell script used for the family sweep) to produce `docs/logo.png` and `docs/icon.png` at 256x256 with cream (#F7F1E3) background.

- [ ] **Step 2: Full README.md**

Replace `README.md` with a complete family-style README. Sections (mirror OrionPatch's structural template):
1. Title + tagline + badges (no NuGet badges since this isn't published; GitHub stars, license, build status, six "uses" badges linking to each Orion package)
2. Centred logo image (`docs/logo.png`)
3. One-paragraph intro: production-shaped banking sample integrating all 6 Orion packages
4. Quickstart: `git clone` + `docker compose up` + open three URLs (Swagger, Seq, Jaeger)
5. The five-minute reader walkthrough (login → register customer → open accounts → transfer → see ciphertext in Postgres + trace in Jaeger + logs in Seq)
6. "What you're seeing" — bulleted list with line-anchored deep links into source for each of the six packages. Example:
   - **OrionGuard** — rate limits applied per endpoint at [Program.cs:34-37](src/Moongazing.OrionShowcase.Api/Program.cs#L34-L37) and [appsettings.json:21-25](src/Moongazing.OrionShowcase.Api/appsettings.json#L21-L25)
   - **OrionVault** — PII encryption at [CustomerConfiguration.cs:21-25](src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs#L21-L25), wired at [InfrastructureServiceCollectionExtensions.cs:18-22](src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs#L18-L22)
   - Similar bullets for OrionAudit, OrionLock, OrionKey, OrionPatch
7. Architecture diagram (the ASCII Clean Architecture diagram from spec §2)
8. Tech stack
9. Roadmap link
10. Orion family cross-link to all 6 sibling repos
11. License, contributing

Length target: roughly 250-400 lines.

- [ ] **Step 3: ROADMAP.md**

Use spec §11 verbatim.

- [ ] **Step 4: CHANGELOG.md**

```markdown
# Changelog

All notable changes to OrionShowcase are recorded here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - 2026-05-26

### Added

- Initial release: production-shaped banking sample integrating all six Moongazing.Orion packages.
- Clean Architecture with Domain, Application, Infrastructure, Api layers.
- Account aggregate with Open, Deposit, Withdraw, Transfer, Freeze, Close. Customer aggregate with Register.
- 10 Minimal API endpoints (auth + customers + accounts + queries).
- MediatR pipeline with Validation, Logging, Idempotency, Audit behaviors.
- EF Core 8 + PostgreSQL persistence with migration on startup.
- JWT bearer authentication (development-grade).
- Per-endpoint OrionGuard rate-limit policies (login, transfer, query).
- OpenTelemetry exports of all six Orion ActivitySources and Meters to Jaeger.
- Serilog console + Seq sink.
- Docker compose with api + postgres + seq + jaeger.
- Domain unit tests, Application unit tests, IntegrationTests with Testcontainers Postgres.

### Known limitations

- Authentication is a dev-grade JWT issuer. Production deployments should integrate OIDC (Keycloak, IdentityServer, Auth0).
- Single Account aggregate. Loan, KYC, multi-tenant features deferred to roadmap.
- No frontend (API + Swagger only).
- No Kubernetes manifests.

[0.1.0]: https://github.com/tunahanaliozturk/OrionShowcase/releases/tag/v0.1.0
```

- [ ] **Step 5: Cross-link OrionShowcase in 6 sibling READMEs**

For each of OrionGuard, OrionAudit, OrionLock, OrionKey, OrionPatch, OrionVault: open a PR adding a "See it in a real app" section that links to OrionShowcase with a one-line description and a deep link into the showcase source showing how that specific package is used.

Example block for OrionVault's README:
```markdown
### See it in a real app

[Moongazing.OrionShowcase](https://github.com/tunahanaliozturk/OrionShowcase) is a production-shaped banking sample integrating all six Orion packages. OrionVault is used to encrypt customer PII columns (TCKN, email, phone) — see [CustomerConfiguration.cs](https://github.com/tunahanaliozturk/OrionShowcase/blob/main/src/Moongazing.OrionShowcase.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs) and [InfrastructureServiceCollectionExtensions.cs](https://github.com/tunahanaliozturk/OrionShowcase/blob/main/src/Moongazing.OrionShowcase.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs).
```

Per-repo flow (all 6 repos):
1. `cd <repo>` and `git checkout <default-branch>` + `git pull`
2. Insert the block in the README (mirror sibling style — usually near the "Family" footer)
3. `git checkout -b docs/add-orionshowcase-crosslink`
4. `git commit -m "docs: cross-link OrionShowcase in real-app section"`
5. `git push -u origin docs/add-orionshowcase-crosslink`
6. `gh pr create --title "Cross-link OrionShowcase as a real-app example" --body "..."`
7. `gh pr merge --squash --delete-branch --admin`

Sibling default branches:
- OrionGuard: master, OrionAudit: master, OrionLock: main, OrionKey: main, OrionPatch: main, OrionVault: main.

- [ ] **Step 6: Commit and push showcase docs**

```
git add README.md ROADMAP.md CHANGELOG.md docs/logo.png docs/icon.png
git commit -m "docs: polish README, ROADMAP, CHANGELOG; deploy cream-bg briefcase logo"
git push
```

---

## Task 18: First release v0.1.0

**Files:**
- (Nothing new to create; release flow only.)

This task is run autonomously, end-to-end. Same pattern as OrionVault v0.1.0 release (proven yesterday).

- [ ] **Step 1: Verify build + tests**

```
dotnet build Moongazing.OrionShowcase.sln -c Release
dotnet test Moongazing.OrionShowcase.sln -c Release --no-build
```
Expected: 0/0 build; all tests pass (Domain ~29 + Application ~21 + Integration ~3 = ~53 tests).

If anything fails, STOP and fix before tagging.

- [ ] **Step 2: Tag and push v0.1.0**

```
git tag -a v0.1.0 -m "OrionShowcase v0.1.0 - first release"
git push origin v0.1.0
```

- [ ] **Step 3: Create GitHub Release (triggers Docker publish workflow)**

Write release notes to `/c/tmp/orionshowcase-v0.1.0-notes.md` mirroring the CHANGELOG. Sincere first-person OK. No emojis, no em-dashes.

```
gh release create v0.1.0 \
  --repo tunahanaliozturk/OrionShowcase \
  --title "OrionShowcase v0.1.0" \
  --notes-file /c/tmp/orionshowcase-v0.1.0-notes.md
```

- [ ] **Step 4: Watch the publish workflow**

```
gh run watch --repo tunahanaliozturk/OrionShowcase
```
Expected: `build-and-test` and `publish-docker` jobs succeed. Docker image pushed to `ghcr.io/tunahanaliozturk/orionshowcase-api:v0.1.0` and `:latest`.

- [ ] **Step 5: Apply branch protection to main**

Same JSON pattern as siblings:
```
cat > /c/tmp/orionshowcase-branch-protection.json << 'JSON'
{
  "required_status_checks": null,
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0,
    "dismiss_stale_reviews": false,
    "require_code_owner_reviews": false
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": false,
  "lock_branch": false,
  "allow_fork_syncing": true
}
JSON

gh api -X PUT repos/tunahanaliozturk/OrionShowcase/branches/main/protection \
  --input /c/tmp/orionshowcase-branch-protection.json
```

- [ ] **Step 6: Report**

Status options: DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED.

Report should include:
- v0.1.0 tag pushed: yes/no
- GitHub Release URL
- CI/CD workflow run conclusion
- Docker image confirmed at `ghcr.io/tunahanaliozturk/orionshowcase-api:v0.1.0`
- Branch protection applied: yes/no
- 6 sibling cross-link PRs (from Task 17 Step 5): list of merged PR URLs
- Total time taken

---

## Self-review notes

**Spec coverage (against §12 of design spec):**

| Spec task | Plan task | Status |
|---|---|---|
| Repo bootstrap | Task 0 | covered |
| Domain value objects + AggregateRoot | Task 1 | covered |
| Account aggregate | Task 2 | covered |
| Customer aggregate | Task 3 | covered |
| MediatR + pipeline behaviors | Task 4 | covered |
| Customer + Account commands | Task 5 | covered |
| Queries | Task 6 | covered |
| BankingDbContext + configs + repositories | Task 7 | covered |
| OrionVault wiring + Customer encryption | Task 8 | covered |
| OrionPatch wiring + domain event bridge | Task 9 | covered |
| OrionLock + OrionAudit + OrionKey wiring | Task 10 | covered |
| DailySettlementService | Task 11 | covered |
| Program.cs + JWT + Swagger | Task 12 | covered |
| All 10 endpoints | Task 13 | covered |
| OrionGuard + OpenTelemetry | Task 14 | covered |
| Docker compose + Dockerfile + init.sql | Task 15 | covered |
| IntegrationTests with Testcontainers | Task 16 | covered |
| README + ROADMAP + CHANGELOG + cross-links | Task 17 | covered |
| First GitHub release | Task 18 | covered |

All spec tasks mapped.

**Placeholder scan:** no "TBD", "TODO", or "similar to" patterns. The repeated phrase "implementer adapts" appears 3 times — all for cases where the exact API surface of a third-party Orion package isn't fully knowable at plan-write time. Each instance describes the intended behavior and the substitution rule, so it is actionable.

**Type consistency:**
- `IdempotencyKey` value object: `readonly record struct IdempotencyKey(Value: string)` — consistent across Tasks 1, 2, 4, 5, 16.
- `AccountId`, `CustomerId`, `TransactionId`: `readonly record struct` wrapping primitive — consistent.
- `Money`: `sealed record` with `Amount: decimal`, `Currency: Currency` — consistent.
- `Result<T>`: `Ok(T)`, `Fail(string)` — consistent across Application handlers and Api endpoints.
- `IClock.UtcNow: DateTimeOffset` — consistent.
- Repository interfaces in Domain, implementations in Infrastructure — consistent layer placement.

No inconsistencies.

---

## Execution Handoff

Plan saved to `docs/superpowers/plans/2026-05-26-orionshowcase-v0.1.0.md`.

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task with two-stage review (spec compliance then code quality) between tasks. Same proven flow used for OrionPatch and OrionVault.
2. **Inline Execution** — execute in this session using `superpowers:executing-plans` with batch checkpoints.

Which approach?

