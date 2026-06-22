namespace Moongazing.OrionShowcase.Application.Accounts.Sagas;

using Microsoft.Extensions.Logging;
using Moongazing.OrionSaga.Orchestration;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Drives account opening as an OrionSaga in-process saga so that each step's side effect
/// is paired with a compensating action. If a later step throws, OrionSaga rolls back the
/// completed steps in reverse order.
/// </summary>
/// <remarks>
/// <para>Steps and their compensations:</para>
/// <list type="number">
///   <item><description><c>validate-customer</c> - confirms the customer exists. Read-only, no compensation.</description></item>
///   <item><description><c>create-account</c> - opens the <see cref="Account"/> aggregate and persists it (which emits
///     <see cref="AccountOpened"/> through the outbox). Compensation closes the account so a partial open never lingers.</description></item>
///   <item><description><c>set-initial-limit</c> - assigns the initial daily transfer limit via
///     <see cref="IAccountLimitRegistry"/>. Compensation removes the limit.</description></item>
/// </list>
/// <para>
/// The deliberate failure hook (<see cref="AccountOpeningContext.ForceFailureAfterLimit"/>) exists so the
/// compensation path can be exercised end to end from a test or a demo request without corrupting real data.
/// </para>
/// </remarks>
public sealed partial class AccountOpeningSaga
{
    private readonly IAccountRepository _accounts;
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _uow;
    private readonly IAccountLimitRegistry _limits;
    private readonly IClock _clock;
    private readonly ILogger<AccountOpeningSaga> _log;

    // Initial daily transfer limit assigned to every newly opened account.
    private const decimal DefaultDailyLimit = 10_000m;

    // Per-step budget for the customer-existence check. If the lookup overruns this, OrionSaga
    // cancels the step and rolls back, reporting a distinct timeout outcome (not a business failure)
    // so a slow dependency is handled as an operational signal rather than a rejected open.
    private static readonly TimeSpan ValidateCustomerTimeout = TimeSpan.FromSeconds(5);

    public AccountOpeningSaga(
        IAccountRepository accounts,
        ICustomerRepository customers,
        IUnitOfWork uow,
        IAccountLimitRegistry limits,
        IClock clock,
        ILogger<AccountOpeningSaga> log)
    {
        _accounts = accounts;
        _customers = customers;
        _uow = uow;
        _limits = limits;
        _clock = clock;
        _log = log;
    }

    /// <summary>Runs the account-opening saga and returns the saga outcome plus the opened account context.</summary>
    public async Task<(SagaResult Result, AccountOpeningContext Context)> RunAsync(
        AccountOpeningContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var saga = new SagaBuilder<AccountOpeningContext>()
            .AddStep(
                name: "validate-customer",
                execute: ValidateCustomerAsync,
                compensate: NoCompensation,
                timeout: context.ValidateCustomerTimeout ?? ValidateCustomerTimeout)
            .AddStep(
                name: "create-account",
                execute: CreateAccountAsync,
                compensate: CompensateCreateAccountAsync)
            .AddStep(
                name: "set-initial-limit",
                execute: SetInitialLimitAsync,
                compensate: CompensateInitialLimitAsync)
            .Build();

        var result = await saga.RunAsync(context, cancellationToken).ConfigureAwait(false);

        // Log the three terminal outcomes distinctly. A per-step timeout or a cancellation is an
        // operational signal (slow dependency, shutdown, client abort), NOT a business rejection, so
        // it must not be logged or surfaced as a generic failure. OrionSaga 0.2 separates these via
        // SagaResult.TimedOut / Cancelled / Failed.
        if (result.Succeeded)
        {
            LogSucceeded(context.CustomerId.Value);
        }
        else if (result.TimedOut)
        {
            LogTimedOut(result.FailedStep, context.CustomerId.Value);
        }
        else if (result.Cancelled)
        {
            LogCancelled(result.FailedStep, context.CustomerId.Value);
        }
        else
        {
            LogFailed(result.Failure, result.FailedStep, context.CustomerId.Value);
        }

        return (result, context);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Account-opening saga succeeded for customer {CustomerId}.")]
    private partial void LogSucceeded(Guid customerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account-opening saga timed out at step '{Step}' for customer {CustomerId}; completed steps were rolled back.")]
    private partial void LogTimedOut(string? step, Guid customerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account-opening saga was cancelled at step '{Step}' for customer {CustomerId}; completed steps were rolled back.")]
    private partial void LogCancelled(string? step, Guid customerId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Account-opening saga failed at step '{Step}' for customer {CustomerId}; completed steps were rolled back.")]
    private partial void LogFailed(Exception? failure, string? step, Guid customerId);

    private async Task ValidateCustomerAsync(AccountOpeningContext ctx, CancellationToken ct)
    {
        // Demo/test hook: an injected delay simulates a slow lookup. It observes the step's
        // cancellation token, which OrionSaga links to the per-step timeout, so an overrun trips the
        // timeout and the saga reports it distinctly rather than as a business failure.
        if (ctx.ValidateCustomerDelay is { } delay && delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }

        var customer = await _customers.GetAsync(ctx.CustomerId, ct).ConfigureAwait(false);
#pragma warning disable CA1849
        Console.Error.WriteLine($"[DIAG-SAGA] validate customer id={ctx.CustomerId.Value} found={customer is not null}");
#pragma warning restore CA1849
        if (customer is null)
        {
            throw new InvalidOperationException($"Customer '{ctx.CustomerId.Value}' was not found.");
        }
    }

    private async Task CreateAccountAsync(AccountOpeningContext ctx, CancellationToken ct)
    {
        var iban = new Iban(ctx.Iban);
        var opening = new Money(ctx.OpeningAmount, ctx.Currency);
        var account = Account.Open(ctx.CustomerId, iban, opening, _clock);

        await _accounts.AddAsync(account, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        ctx.AccountId = account.Id;
    }

    private async Task CompensateCreateAccountAsync(AccountOpeningContext ctx, CancellationToken ct)
    {
        if (ctx.AccountId is null)
        {
            return;
        }

        var account = await _accounts.GetAsync(ctx.AccountId.Value, ct).ConfigureAwait(false);
        if (account is null)
        {
            return;
        }

        // Closing the just-opened account is the inverse of create-account. Close() refuses a
        // non-zero balance, so a non-zero opening deposit is withdrawn first to leave it empty.
        if (account.Balance.Amount != 0m)
        {
            account.Withdraw(account.Balance, ctx.IdempotencyKey, _clock);
        }
        account.Close(_clock);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private Task SetInitialLimitAsync(AccountOpeningContext ctx, CancellationToken ct)
    {
        _limits.SetDailyLimit(ctx.AccountId!.Value, new Money(DefaultDailyLimit, ctx.Currency));

        // Demo hook: lets a caller force the compensation path after the limit step succeeds.
        if (ctx.ForceFailureAfterLimit)
        {
            throw new InvalidOperationException("Forced failure after set-initial-limit (saga compensation demo).");
        }

        return Task.CompletedTask;
    }

    private Task CompensateInitialLimitAsync(AccountOpeningContext ctx, CancellationToken ct)
    {
        _limits.RemoveLimit(ctx.AccountId!.Value);
        return Task.CompletedTask;
    }

    private static Task NoCompensation(AccountOpeningContext ctx, CancellationToken ct) => Task.CompletedTask;
}
