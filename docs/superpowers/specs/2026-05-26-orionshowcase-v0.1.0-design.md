# Moongazing.OrionShowcase v0.1.0 — Design Specification

**Date:** 2026-05-26
**Status:** Approved for implementation
**Target version:** 0.1.0
**Family:** Orion (showcase consumer of OrionGuard, OrionAudit, OrionLock, OrionKey, OrionPatch, OrionVault)

---

## 1. Purpose

A production-shaped sample application that integrates all six Moongazing.Orion NuGet packages in one cohesive, layered Clean Architecture codebase. The domain is core retail banking (account open / deposit / withdraw / transfer / freeze / close, plus a daily settlement job). The audience is senior .NET developers evaluating whether to adopt the Orion family.

**Primary value:** seeing six packages compose into a real workflow is qualitatively different from reading six README quickstarts in isolation. A reader can open the repo, run `docker compose up`, hit `/swagger`, perform a transfer, and watch the contributions of every package in Jaeger traces and Seq logs side by side.

**Cross-link strategy:** every Orion package README gains a new section linking back to this showcase with deep file-line references showing exactly where that package fits.

**Out of scope for v0.1.0:**
- Production OIDC (Keycloak, IdentityServer). Showcase uses a development-grade JWT issuer endpoint.
- Frontend (Blazor / Angular / React). API + Swagger UI only.
- Loan aggregate, KYC workflow, multi-tenant slice — all deferred to roadmap.
- gRPC variant (deferred to v0.4 roadmap).
- Kubernetes manifests / Helm. Single `docker compose` file only.
- NuGet publish. This is an application, not a library.

---

## 2. High-level architecture

Standard four-layer Clean Architecture with one-way dependency flow:

```
┌─────────────────────────────────────────────────────────────┐
│  Api          Minimal API endpoints, JWT, Swagger,          │
│               OrionGuard.AspNetCore middleware              │
├─────────────────────────────────────────────────────────────┤
│  Application  MediatR commands/queries/handlers,            │
│               pipeline behaviors (Validation, Logging,      │
│               OrionAudit, OrionKey idempotency)             │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure  EF Core + Postgres, OrionVault wiring,     │
│                  OrionPatch outbox, OrionLock backend,      │
│                  OrionAudit repositories, OrionKey store,   │
│                  hosted services (OutboxDispatcher,         │
│                  DailySettlement)                           │
├─────────────────────────────────────────────────────────────┤
│  Domain       Aggregates, value objects, domain events.     │
│               Pure C#, no framework references.             │
└─────────────────────────────────────────────────────────────┘

Dependency direction:  Api → Application → Infrastructure → Domain
                                                  ↑___________|
```

Test projects:
- `OrionShowcase.Domain.Tests` (xunit, fast, no infrastructure)
- `OrionShowcase.Application.Tests` (xunit + in-memory fakes for repositories/locks/idempotency)
- `OrionShowcase.IntegrationTests` (xunit + `WebApplicationFactory` + Testcontainers Postgres + real package wiring)

## 3. Where each Orion package fits

| Package | Layer | Concrete usage |
|---|---|---|
| **OrionGuard.AspNetCore** | Api | `app.UseOrionGuard()` middleware. Per-endpoint policies (`login`, `transfer`, `query`) bound via `.WithOrionGuardPolicy("...")`. |
| **OrionAudit** | Application + Infrastructure | `AuditBehavior<TReq,TRes>` MediatR pipeline behavior captures every command marked `IAuditableCommand` with actor, request payload, outcome. Audit rows persisted via Infrastructure repository. |
| **OrionLock.Postgres** | Infrastructure | `TransferMoneyHandler` acquires locks on both source and target account ids (sorted to prevent deadlock). `DailySettlementService` acquires a single global lock so only one instance runs even with multiple replicas. |
| **OrionKey** | Application + Infrastructure | `IdempotencyBehavior<TReq,TRes>` MediatR pipeline behavior; commands implementing `IIdempotentCommand` are deduplicated. Infrastructure provides `OrionKeyIdempotencyStore` implementing `IIdempotencyStore`. Snowflake IDs used for `TransferId`/`TransactionId`. |
| **OrionPatch.EntityFrameworkCore** | Infrastructure | `UseOrionPatch(sp)` attaches `SaveChangesInterceptor` to `BankingDbContext`. Domain events on aggregates auto-flushed to `outbox_messages` in the same transaction. `OutboxDispatcherService` (auto-registered) publishes to sinks. |
| **OrionVault.EntityFrameworkCore** | Infrastructure | `UseOrionVault(sp)` + `[Encrypted]`/`IsEncrypted()` on `Customer.NationalId`, `Customer.Email`, `Customer.Phone`. AES-256-GCM ciphertext on disk; plaintext transparently in code. |

The package-to-layer mapping is the central pedagogical claim of the showcase: a reader can point to any package and find a single place in the codebase where it is wired up, plus an obvious set of places where it is used.

---

## 4. Domain layer

### Aggregates

```csharp
public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected init; } = default!;
    private readonly List<object> _domainEvents = new();
    public IReadOnlyList<object> DomainEvents => _domainEvents;
    protected void Raise(object @event) => _domainEvents.Add(@event);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class Account : AggregateRoot<AccountId>
{
    public CustomerId CustomerId { get; private set; }
    public Iban Iban { get; private set; }
    public Money Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    private readonly List<Transaction> _transactions = new();
    public IReadOnlyList<Transaction> Transactions => _transactions;

    public static Account Open(CustomerId customer, Iban iban, Money opening, IClock clock);
    public void Deposit(Money amount, IdempotencyKey key, IClock clock);
    public void Withdraw(Money amount, IdempotencyKey key, IClock clock);
    public void RecordTransfer(AccountId counterparty, Money amount, IdempotencyKey key, IClock clock);
    public void Freeze(string reason, IClock clock);
    public void Close(IClock clock);
}

public sealed class Customer : AggregateRoot<CustomerId>
{
    public string FullName { get; private set; }   // plaintext (name is acceptable in logs)
    public Tckn NationalId { get; private set; }   // [Encrypted] in EF config
    public string Email { get; private set; }      // [Encrypted] in EF config
    public string Phone { get; private set; }      // [Encrypted] in EF config
    public DateTimeOffset RegisteredAt { get; private set; }

    public static Customer Register(string fullName, Tckn nationalId, string email, string phone, IClock clock);
}

public sealed class Transaction
{
    public TransactionId Id { get; init; }
    public TransactionKind Kind { get; init; }   // Deposit, Withdrawal, TransferOut, TransferIn
    public Money Amount { get; init; }
    public Money BalanceAfter { get; init; }
    public IdempotencyKey IdempotencyKey { get; init; }
    public DateTimeOffset At { get; init; }
}
```

### Value objects

All immutable records/structs, validated in constructor.

```csharp
public readonly record struct AccountId(Guid Value);
public readonly record struct CustomerId(Guid Value);
public readonly record struct TransactionId(long Value);   // Snowflake ID from OrionKey
public readonly record struct IdempotencyKey(string Value);

public sealed record Money(decimal Amount, Currency Currency)
{
    public Money { /* Amount >= 0, decimal scale matches Currency */ }
    public static Money operator +(Money a, Money b);   // same currency check, throws otherwise
    public static Money operator -(Money a, Money b);   // same currency, result >= 0
}

public sealed record Iban(string Value)
{
    public Iban { /* IBAN format + mod-97 validation */ }
    public string CountryCode => Value[..2];
}

public sealed record Tckn(string Value)
{
    public Tckn { /* 11 digit + Turkish national ID checksum */ }
}

public enum Currency { TRY, USD, EUR }
public enum AccountStatus { Active, Frozen, Closed }
public enum TransactionKind { Deposit, Withdrawal, TransferOut, TransferIn }
```

### Domain events

```csharp
public sealed record AccountOpened(AccountId AccountId, CustomerId CustomerId, Iban Iban, Money Opening, DateTimeOffset At);
public sealed record MoneyDeposited(AccountId AccountId, Money Amount, Money NewBalance, IdempotencyKey Key, DateTimeOffset At);
public sealed record MoneyWithdrawn(AccountId AccountId, Money Amount, Money NewBalance, IdempotencyKey Key, DateTimeOffset At);
public sealed record TransferCompleted(AccountId From, AccountId To, Money Amount, IdempotencyKey Key, DateTimeOffset At);
public sealed record AccountFrozen(AccountId AccountId, string Reason, DateTimeOffset At);
public sealed record AccountClosed(AccountId AccountId, DateTimeOffset At);
public sealed record CustomerRegistered(CustomerId CustomerId, DateTimeOffset At);
```

### Domain invariants (in aggregate methods)

- `Account.Withdraw`: balance >= amount, else `InsufficientFundsException`
- `Account.Deposit`/`Withdraw`: amount > 0, same currency as balance
- `Account.Freeze`: only when `Status == Active`
- `Account.Close`: only when balance equals zero and `Status == Active`
- `Money + Money` / `Money - Money`: same currency required
- `Tckn` constructor: 11-digit, passes Turkish national ID checksum
- `Iban` constructor: format + mod-97 validation

### Domain abstractions (interfaces only, implemented in Infrastructure)

```csharp
public interface IClock { DateTimeOffset UtcNow { get; } }
public interface IAccountRepository
{
    Task<Account?> GetAsync(AccountId id, CancellationToken ct);
    Task AddAsync(Account account, CancellationToken ct);
}
public interface ICustomerRepository
{
    Task<Customer?> GetAsync(CustomerId id, CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
}
public interface IUnitOfWork { Task<int> SaveChangesAsync(CancellationToken ct); }
```

### Domain test coverage (Domain.Tests)

- Aggregate behavior: open/deposit/withdraw/transfer/freeze/close happy paths and invariant violations
- Value objects: each constructor validation case (negative amount, mismatched currency, invalid IBAN, invalid TCKN)
- Domain events emitted match expected list per operation

---

## 5. Application layer

### Folder structure (per-feature)

```
Application/
├── Accounts/
│   ├── Commands/
│   │   ├── OpenAccount/{OpenAccountCommand.cs, OpenAccountHandler.cs, OpenAccountValidator.cs}
│   │   ├── DepositMoney/...
│   │   ├── WithdrawMoney/...
│   │   ├── TransferMoney/...
│   │   ├── FreezeAccount/...
│   │   └── CloseAccount/...
│   └── Queries/
│       ├── GetAccountBalance/...
│       └── GetAccountTransactions/...
├── Customers/
│   └── Commands/RegisterCustomer/...
├── Settlement/
│   └── RunDailySettlement.cs                     # invoked by hosted service, not MediatR
├── Abstractions/
│   ├── IAccountRepository.cs                     # re-exported from Domain for clarity
│   ├── ICustomerRepository.cs
│   ├── IUnitOfWork.cs
│   ├── ICurrentUser.cs                           # resolved from JWT claims
│   └── IIdempotencyStore.cs                      # implemented in Infrastructure via OrionKey
└── Pipeline/
    ├── ValidationBehavior.cs                     # FluentValidation; throws → 400 in Api
    ├── LoggingBehavior.cs                        # source-gen [LoggerMessage]
    ├── IdempotencyBehavior.cs                    # OrionKey-backed via IIdempotencyStore
    └── AuditBehavior.cs                          # OrionAudit
```

### MediatR pipeline order

Outermost → innermost, unwinds in reverse:

```
Endpoint → Mediator.Send(command)
   └─ LoggingBehavior      (start log + correlation id)
      └─ ValidationBehavior   (FluentValidation, throws → 400)
         └─ IdempotencyBehavior   (OrionKey: replay cached response if key claimed before)
            └─ AuditBehavior          (capture command + actor before handler)
               └─ Handler                  (domain logic + repository save)
                  └─ SaveChanges
                     └─ OrionPatch interceptor   (collect domain events to outbox)
                     └─ OrionVault converter      (encrypt PII columns)
            <─ AuditBehavior      (capture response + outcome after handler)
         <─ IdempotencyBehavior   (store response under key, TTL 24h)
      <─ ValidationBehavior
   <─ LoggingBehavior      (end log + duration)
```

### Marker interfaces for opt-in behaviors

```csharp
public interface IAuditableCommand { }                // AuditBehavior only applies if marked
public interface IIdempotentCommand                   // IdempotencyBehavior only applies if marked
{
    IdempotencyKey IdempotencyKey { get; }
}
```

### Commands inventory

| Command | Handler responsibility | Pipeline behaviors |
|---|---|---|
| `RegisterCustomerCommand` | `Customer.Register` + add + save | Validation, Audit, Idempotency |
| `OpenAccountCommand` | `Account.Open` + add + save | Validation, Audit, Idempotency |
| `DepositMoneyCommand` | `Account.Deposit` + save | Validation, Audit, Idempotency |
| `WithdrawMoneyCommand` | `Account.Withdraw` + save | Validation, Audit, Idempotency |
| `TransferMoneyCommand` | Acquire 2 locks, mutate both accounts, save | Validation, Audit, Idempotency |
| `FreezeAccountCommand` | `Account.Freeze` + save | Validation, Audit |
| `CloseAccountCommand` | `Account.Close` + save | Validation, Audit |

### Queries inventory

| Query | Handler responsibility | Pipeline behaviors |
|---|---|---|
| `GetAccountBalanceQuery` | EF projection to `BalanceDto` | Logging only |
| `GetAccountTransactionsQuery` | EF paginated projection to `TransactionDto[]` | Logging only |

### TransferMoneyHandler (the showcase moment)

```csharp
public class TransferMoneyHandler : IRequestHandler<TransferMoneyCommand, TransferResult>
{
    private readonly IOrionLock _locks;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public async Task<TransferResult> Handle(TransferMoneyCommand cmd, CancellationToken ct)
    {
        // Sort to prevent deadlock — both replicas acquire locks in the same order
        var (lower, higher) = cmd.From.Value.CompareTo(cmd.To.Value) < 0
            ? (cmd.From, cmd.To)
            : (cmd.To, cmd.From);

        await using var lockA = await _locks.AcquireAsync($"account:{lower.Value}", TimeSpan.FromSeconds(30), ct);
        await using var lockB = await _locks.AcquireAsync($"account:{higher.Value}", TimeSpan.FromSeconds(30), ct);

        var from = await _accounts.GetAsync(cmd.From, ct) ?? throw new NotFoundException(cmd.From);
        var to   = await _accounts.GetAsync(cmd.To, ct)   ?? throw new NotFoundException(cmd.To);

        from.Withdraw(cmd.Amount, cmd.IdempotencyKey, _clock);
        to.Deposit(cmd.Amount, cmd.IdempotencyKey, _clock);
        from.RecordTransfer(to.Id, cmd.Amount, cmd.IdempotencyKey, _clock);   // raises TransferCompleted event

        await _uow.SaveChangesAsync(ct);                                       // OrionPatch flushes events to outbox
        return TransferResult.Ok(new TransferId(/* snowflake from OrionKey */));
    }
}
```

### Application test coverage (Application.Tests)

- Each handler tested with in-memory fakes (`FakeAccountRepository`, `FakeOrionLock`, `FakeIdempotencyStore`, `TestClock`)
- `TransferMoneyHandler_acquires_locks_in_sorted_order_to_prevent_deadlock` (assert via `FakeOrionLock.AcquisitionLog`)
- `IdempotencyBehavior_returns_cached_response_on_replay_with_same_key`
- `IdempotencyBehavior_runs_handler_on_first_call_with_new_key`
- `AuditBehavior_records_success_outcome`
- `AuditBehavior_records_failure_outcome_when_handler_throws`
- `ValidationBehavior_throws_ValidationException_when_validator_fails`

---

## 6. Infrastructure layer

### Folder structure

```
Infrastructure/
├── Persistence/
│   ├── BankingDbContext.cs
│   ├── Configurations/{AccountConfiguration, CustomerConfiguration, TransactionConfiguration}.cs
│   ├── Migrations/
│   ├── Repositories/{AccountRepository, CustomerRepository}.cs
│   └── EfUnitOfWork.cs
├── Vault/
│   └── (DI extension only — OrionVault is the implementation)
├── Outbox/
│   ├── DomainEventOutboxAdapter.cs          # bridges AggregateRoot<>.DomainEvents → OrionPatch
│   └── OrionPatchExtensions.cs
├── Locking/
│   └── OrionLockExtensions.cs               # AddOrionLock().UsePostgres(connStr)
├── Audit/
│   └── OrionAuditExtensions.cs              # AddOrionAudit().UseEntityFrameworkCore<DbContext>
├── Idempotency/
│   ├── OrionKeyIdempotencyStore.cs          # implements IIdempotencyStore via OrionKey
│   └── OrionKeyExtensions.cs
├── Time/
│   └── SystemClock.cs                       # implements IClock with DateTimeOffset.UtcNow
├── HostedServices/
│   └── DailySettlementService.cs            # OrionLock-gated single-instance run
└── DependencyInjection/
    └── InfrastructureServiceCollectionExtensions.cs
```

### `AddInfrastructure(IConfiguration)`

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
{
    services.AddDbContext<BankingDbContext>((sp, opt) =>
    {
        opt.UseNpgsql(cfg.GetConnectionString("Banking"));
        opt.UseOrionVault(sp);     // attach value converters
        opt.UseOrionPatch(sp);     // attach SaveChangesInterceptor
    });

    services.AddOrionVault(o =>
    {
        o.UseStaticKeys(k => k.Add(keyId: 1, base64Key: cfg["Vault:Key1"]!));
        o.ActiveKeyId = 1;
    }).UseEntityFrameworkCore<BankingDbContext>();

    services.AddOrionPatch().UseEntityFrameworkCore<BankingDbContext>();

    services.AddOrionLock().UsePostgres(cfg.GetConnectionString("Banking")!);

    services.AddOrionAudit().UseEntityFrameworkCore<BankingDbContext>();

    services.AddOrionKey(o => o.WorkerId = cfg.GetValue<int>("OrionKey:WorkerId"));
    services.AddScoped<IIdempotencyStore, OrionKeyIdempotencyStore>();

    services.AddScoped<IAccountRepository, AccountRepository>();
    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<IUnitOfWork, EfUnitOfWork>();

    services.AddSingleton<IClock, SystemClock>();
    services.AddHostedService<DailySettlementService>();
    // OrionPatch dispatcher hosted service is auto-registered by AddOrionPatch.

    return services;
}
```

### CustomerConfiguration (OrionVault encryption in action)

```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasConversion(id => id.Value, v => new CustomerId(v));
        b.Property(c => c.FullName).HasMaxLength(200).IsRequired();

        b.Property(c => c.NationalId)
            .HasConversion(v => v.Value, s => new Tckn(s))
            .IsEncrypted();           // OrionVault: ciphertext on disk
        b.Property(c => c.Email).IsEncrypted();
        b.Property(c => c.Phone).IsEncrypted();
        b.Property(c => c.RegisteredAt);
    }
}
```

### DailySettlementService (OrionLock single-instance)

```csharp
public class DailySettlementService : BackgroundService
{
    private readonly IOrionLock _locks;
    private readonly IServiceProvider _sp;
    private readonly ILogger<DailySettlementService> _log;
    private readonly IClock _clock;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(NextRunDelay(_clock.UtcNow), stoppingToken);

            await using var lease = await _locks.TryAcquireAsync("settlement:daily",
                TimeSpan.FromMinutes(30), stoppingToken);
            if (lease is null)
            {
                LogSkipped();
                continue;   // another instance is running settlement
            }

            await using var scope = _sp.CreateAsyncScope();
            var settler = scope.ServiceProvider.GetRequiredService<RunDailySettlement>();
            await settler.ExecuteAsync(stoppingToken);
        }
    }

    private static TimeSpan NextRunDelay(DateTimeOffset now)
    {
        // every day at 23:55 UTC
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, 23, 55, 0, TimeSpan.Zero);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Settlement skipped: another instance holds the lock.")]
    partial void LogSkipped();
}
```

### Domain event → OrionPatch bridge

`OrionPatch`'s `SaveChangesInterceptor` looks for entities whose tracked changes include a `DomainEvents` collection. The bridge:

1. `AggregateRoot<TId>` exposes `IReadOnlyList<object> DomainEvents`.
2. `Infrastructure/Outbox/DomainEventOutboxAdapter.cs` registers a callback so OrionPatch reads from this property.
3. After save, OrionPatch clears events on each aggregate.

This is configured in `AddOrionPatch().UseEntityFrameworkCore<BankingDbContext>()` via a small adapter — no per-aggregate boilerplate.

### `OrionKeyIdempotencyStore` (bridges Application abstraction to OrionKey)

```csharp
internal sealed class OrionKeyIdempotencyStore : IIdempotencyStore
{
    private readonly IIdempotencyKeyService _orionKey;

    public Task<bool> TryClaimAsync(IdempotencyKey key, string requestHash, CancellationToken ct)
        => _orionKey.TryClaimAsync(key.Value, requestHash, TimeSpan.FromHours(24), ct);

    public Task<string?> GetCachedResponseAsync(IdempotencyKey key, CancellationToken ct)
        => _orionKey.GetResponseAsync(key.Value, ct);

    public Task StoreResponseAsync(IdempotencyKey key, string serialisedResponse, CancellationToken ct)
        => _orionKey.StoreResponseAsync(key.Value, serialisedResponse, TimeSpan.FromHours(24), ct);
}
```

### Infrastructure test coverage (covered indirectly via IntegrationTests)

Infrastructure has no dedicated unit test project. It is exercised entirely by `IntegrationTests`, which spin up a Testcontainers Postgres, register the full Infrastructure stack, and exercise commands end to end.

---

## 7. Api layer

### Folder structure

```
Api/
├── Program.cs                              # composition root
├── appsettings.json
├── appsettings.Development.json
├── Endpoints/
│   ├── EndpointExtensions.cs               # MapBankingEndpoints()
│   ├── Auth/LoginEndpoint.cs
│   ├── Customers/RegisterCustomerEndpoint.cs
│   └── Accounts/{OpenAccount, Deposit, Withdraw, Transfer, Freeze, Close, GetBalance, GetTransactions}Endpoint.cs
├── Authentication/
│   ├── JwtIssuer.cs                        # symmetric HS256, dev-grade
│   ├── ClaimsCurrentUser.cs                # ICurrentUser implementation
│   └── JwtBearerExtensions.cs
├── Filters/
│   ├── ValidationProblemFilter.cs          # FluentValidationException → 400 ProblemDetails
│   └── DomainExceptionFilter.cs            # domain exceptions → 4xx ProblemDetails
├── Swagger/
│   └── SwaggerExtensions.cs                # OpenAPI doc + JWT bearer scheme
├── Observability/
│   ├── OpenTelemetryExtensions.cs          # 6 ActivitySources + 6 Meters, OTLP → Jaeger
│   └── SerilogExtensions.cs                # console + Seq sink
└── Health/
    └── HealthChecksExtensions.cs           # /health/live and /health/ready
```

### `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"]!));

builder.Services
    .AddApplication()                                       // MediatR + behaviors + validators
    .AddInfrastructure(builder.Configuration)               // EF + 6 Orion packages
    .AddJwtBearerAuth(builder.Configuration)
    .AddProblemDetails()
    .AddEndpointsApiExplorer()
    .AddSwagger()
    .AddOpenTelemetryForOrion(builder.Configuration)        // exports to Jaeger
    .AddHealthChecks().Services
    .AddOrionGuardAspNetCore(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseOrionGuard();
app.MapBankingEndpoints();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") });
app.Run();

public partial class Program;                               // for WebApplicationFactory
```

### Endpoint pattern (one example; all others identical shape)

```csharp
internal static class TransferEndpoint
{
    public static IEndpointConventionBuilder MapTransfer(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/accounts/{from:guid}/transfer", Handle)
           .RequireAuthorization()
           .WithName("TransferMoney")
           .WithTags("Accounts")
           .WithOrionGuardPolicy("transfer")
           .Produces<TransferResponse>(StatusCodes.Status200OK)
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<IResult> Handle(
        Guid from,
        TransferRequest req,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new TransferMoneyCommand(
            From: new AccountId(from),
            To: new AccountId(req.ToAccountId),
            Amount: new Money(req.Amount, Enum.Parse<Currency>(req.Currency)),
            IdempotencyKey: new IdempotencyKey(req.IdempotencyKey)), ct);

        return result.IsSuccess
            ? Results.Ok(new TransferResponse(result.TransferId))
            : Results.Problem(detail: result.Error, statusCode: 409);
    }
}

internal sealed record TransferRequest(Guid ToAccountId, decimal Amount, string Currency, string IdempotencyKey);
internal sealed record TransferResponse(long TransferId);
```

### Endpoint inventory

| Method | Path | Auth | OrionGuard policy |
|---|---|---|---|
| POST | `/api/auth/login` | none | `login` |
| POST | `/api/customers` | bearer | `query` |
| POST | `/api/accounts` | bearer | `query` |
| POST | `/api/accounts/{id}/deposit` | bearer | `transfer` |
| POST | `/api/accounts/{id}/withdraw` | bearer | `transfer` |
| POST | `/api/accounts/{from}/transfer` | bearer | `transfer` |
| POST | `/api/accounts/{id}/freeze` | bearer | `query` |
| POST | `/api/accounts/{id}/close` | bearer | `query` |
| GET  | `/api/accounts/{id}/balance` | bearer | `query` |
| GET  | `/api/accounts/{id}/transactions` | bearer | `query` |

Plus `/swagger`, `/health/live`, `/health/ready`.

### JWT (dev-grade only)

`LoginEndpoint` issues a JWT for a hardcoded demo user (`demo:demo`) seeded into the database. Signs with a symmetric HS256 key from `appsettings.json`. JWT bearer middleware validates incoming requests. Claims: `sub` (user id), `customer_id`, `roles`.

The README is explicit: this is showcase-grade authentication, not production. Real adopters swap in OIDC (Keycloak / IdentityServer / Auth0).

### OrionGuard policies (per-endpoint)

```json
"OrionGuard": {
  "Policies": {
    "login":    { "Limit": 5,   "Window": "00:01:00" },
    "transfer": { "Limit": 10,  "Window": "00:01:00" },
    "query":    { "Limit": 100, "Window": "00:01:00" }
  }
}
```

`.WithOrionGuardPolicy("transfer")` on each endpoint selects the policy.

### OpenTelemetry wiring

`AddOpenTelemetryForOrion()` registers all six Orion `ActivitySource` and `Meter` names, plus ASP.NET Core, EF Core, Npgsql instrumentation, and exports via OTLP gRPC to Jaeger:

```csharp
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
        .AddOtlpExporter(o => o.Endpoint = new Uri(cfg["Otel:Endpoint"]!)))
    .WithMetrics(m => m
        .AddMeter("Moongazing.OrionGuard")
        .AddMeter("Moongazing.OrionAudit")
        .AddMeter("Moongazing.OrionLock")
        .AddMeter("Moongazing.OrionKey")
        .AddMeter("Moongazing.OrionPatch")
        .AddMeter("Moongazing.OrionVault")
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(cfg["Otel:Endpoint"]!)));
```

A single Jaeger trace for one transfer request will show spans from all six packages alongside HTTP and EF spans. This is the central evidence the showcase exists to provide.

---

## 8. Solution structure

```
Moongazing.OrionShowcase/
├── Moongazing.OrionShowcase.sln                  # 7 entries (4 src + 3 test)
├── Directory.Build.props
├── Directory.Packages.props
├── .gitignore, LICENSE (MIT)
├── README.md, ROADMAP.md, CHANGELOG.md
├── docs/
│   ├── logo.png, icon.png                        # cream-bg, family aesthetic
│   └── superpowers/
│       ├── specs/2026-05-26-orionshowcase-v0.1.0-design.md
│       └── plans/2026-05-26-orionshowcase-v0.1.0.md
├── .github/workflows/
│   ├── ci.yml                                    # build + test on PR
│   └── ci-cd.yml                                 # release-triggered, NO NuGet push
├── docker/
│   ├── compose.yaml
│   ├── Dockerfile.api
│   └── postgres-init.sql                         # demo seed
├── src/
│   ├── Moongazing.OrionShowcase.Domain/
│   ├── Moongazing.OrionShowcase.Application/
│   ├── Moongazing.OrionShowcase.Infrastructure/
│   └── Moongazing.OrionShowcase.Api/
└── test/
    ├── Moongazing.OrionShowcase.Domain.Tests/
    ├── Moongazing.OrionShowcase.Application.Tests/
    └── Moongazing.OrionShowcase.IntegrationTests/
```

### Target framework

`net8.0` only (single target). This is an application, not a library; multi-target would force EF Core / hosting multi-target with no benefit.

### Dependencies (Directory.Packages.props)

| Package | Version | Reason |
|---|---|---|
| `Moongazing.OrionGuard.AspNetCore` | 6.4.2 | Rate-limit middleware |
| `Moongazing.OrionAudit` | 0.6.1 | Audit log |
| `Moongazing.OrionLock.Postgres` | 0.2.1 | Distributed locks |
| `Moongazing.OrionKey` | 0.4.1 | Idempotency + snowflake IDs |
| `Moongazing.OrionPatch.EntityFrameworkCore` | 0.1.1 | Transactional outbox |
| `Moongazing.OrionVault.EntityFrameworkCore` | 0.1.1 | Column encryption |
| `MediatR` | 12.4.x | CQRS |
| `FluentValidation`, `FluentValidation.DependencyInjectionExtensions` | 11.x | Validation |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.x | Migrations |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 8.0.x | Postgres EF provider |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.x | JWT validation |
| `Microsoft.IdentityModel.Tokens`, `System.IdentityModel.Tokens.Jwt` | 7.x or 8.x | JWT issuing |
| `Swashbuckle.AspNetCore` | 6.7.x | Swagger |
| `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.Seq` | latest 8.x | Logging |
| `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `Npgsql.OpenTelemetry` | latest 1.x | Tracing/metrics |
| `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` | latest | Tests |
| `Testcontainers.PostgreSql` | latest 3.x | Integration test Postgres |
| `Microsoft.AspNetCore.Mvc.Testing` | 8.0.x | `WebApplicationFactory` |

---

## 9. Docker Compose

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
    environment: { COLLECTOR_OTLP_ENABLED: true }
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
      Jwt__SigningKey: "demo-signing-key-min-32-chars-for-hs256"
      OrionKey__WorkerId: 1
      ASPNETCORE_ENVIRONMENT: Development
    ports: ["5000:8080"]

volumes:
  banking-data:
  seq-data:
```

### Five-minute reader experience

```
git clone https://github.com/tunahanaliozturk/OrionShowcase
cd OrionShowcase
docker compose -f docker/compose.yaml up -d
```

Within ~60 seconds, 4 services are healthy. Reader opens:
- `http://localhost:5000/swagger` (API)
- `http://localhost:5341` (Seq logs)
- `http://localhost:16686` (Jaeger traces)

Then in Swagger:
1. `POST /api/auth/login` → bearer token.
2. Authorize in Swagger with token.
3. `POST /api/customers` → register "Ali Veli". TCKN/Email/Phone arrive plaintext, are written to Postgres as ciphertext (`SELECT national_id FROM customers` in psql shows binary garbage).
4. `POST /api/accounts` twice → two accounts.
5. `POST /api/accounts/{from}/transfer` → transfer.
   - Jaeger trace shows: HTTP span → OrionGuard rate-limit span → JWT auth → MediatR pipeline (Logging, Validation, Idempotency claim span from OrionKey, Audit span from OrionAudit) → handler → 2 OrionLock acquire spans → EF queries → SaveChanges → OrionPatch outbox insert span → OrionVault decrypt spans on the customer read.
   - Seq shows structured logs from all six packages with the same trace id.
   - `SELECT * FROM outbox_messages` shows the `TransferCompleted` event waiting for dispatch.

This integrated trace is the central showcase artefact.

---

## 10. CI/CD

`.github/workflows/ci.yml`: build + test on every push and PR (Postgres service container for integration tests).

`.github/workflows/ci-cd.yml`: release-triggered. Builds Docker image and pushes to GHCR (GitHub Container Registry). **No NuGet push step**; OrionShowcase is not a library.

Branch protection on `main` (same JSON pattern as siblings): PR required, no force push, no deletion.

After v0.1.0 release: cross-link from all six Orion sibling READMEs to OrionShowcase, including line-anchored deep links into the relevant source files.

---

## 11. Roadmap

```
v0.1.0 — 2026-Q2 (this release)
  Single Account aggregate, 10 endpoints, all 6 Orion packages integrated
  Clean Architecture (Domain / Application / Infrastructure / Api)
  Docker compose: api + postgres + seq + jaeger
  ~40-60 tests across Domain, Application, IntegrationTests
  Cross-link from all 6 Orion READMEs

v0.2 — Loan aggregate
  Loan application workflow, KYC step, cross-aggregate coordination
  Foundation for a future OrionFlow saga library

v0.3 — Multi-tenant slice
  Per-tenant key partitioning (requires OrionVault v0.4)
  Tenant-scoped OrionGuard policies

v0.4 — gRPC variant
  Add gRPC endpoints alongside REST, demonstrating OrionGuard.Grpc

v1.0 — production-shaped
  OIDC integration (Keycloak or IdentityServer)
  Blazor admin frontend
  Kubernetes manifests + Helm chart
```

---

## 12. Estimated implementation effort

Approximately 15-18 tasks via the same subagent-driven-development flow that built OrionVault:

| # | Task |
|---|---|
| 0 | Repo bootstrap + solution + GitHub repo + CI workflow |
| 1 | Domain: value objects + AggregateRoot base + Domain.Tests setup |
| 2 | Domain: Account aggregate + events + tests |
| 3 | Domain: Customer aggregate + tests |
| 4 | Application: MediatR setup + Pipeline behaviors skeleton + tests |
| 5 | Application: Customer + Account commands + handlers + validators + tests |
| 6 | Application: Queries + tests |
| 7 | Infrastructure: BankingDbContext + EF configurations + repositories |
| 8 | Infrastructure: OrionVault wiring + Customer encryption verification |
| 9 | Infrastructure: OrionPatch wiring + domain event bridge |
| 10 | Infrastructure: OrionLock + OrionAudit + OrionKey wiring |
| 11 | Infrastructure: DailySettlementService hosted service |
| 12 | Api: Program.cs + JWT auth + Swagger |
| 13 | Api: All 10 endpoints |
| 14 | Api: OrionGuard policies + OpenTelemetry export |
| 15 | Docker compose + Dockerfile + postgres-init.sql |
| 16 | IntegrationTests with Testcontainers — happy paths for register/open/deposit/transfer |
| 17 | Docs polish (README, ROADMAP, CHANGELOG) + cross-link all 6 sibling READMEs |
| 18 | First GitHub release v0.1.0 (no NuGet) |

---

## 13. Open questions / deferred decisions

None blocking implementation. Items deliberately deferred (with v0.x targets) are listed in §1 "Out of scope" and §11 "Roadmap".
