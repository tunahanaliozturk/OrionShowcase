namespace Moongazing.OrionShowcase.Application.Accounts.Sagas;

using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Mutable state threaded through the <see cref="AccountOpeningSaga"/> steps. The execute steps
/// populate <see cref="AccountId"/>; the compensations read it to undo their work.
/// </summary>
public sealed class AccountOpeningContext
{
    public AccountOpeningContext(
        CustomerId customerId,
        string iban,
        decimal openingAmount,
        Currency currency,
        IdempotencyKey idempotencyKey,
        bool forceFailureAfterLimit = false,
        TimeSpan? validateCustomerDelay = null,
        TimeSpan? validateCustomerTimeout = null)
    {
        CustomerId = customerId;
        Iban = iban;
        OpeningAmount = openingAmount;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
        ForceFailureAfterLimit = forceFailureAfterLimit;
        ValidateCustomerDelay = validateCustomerDelay;
        ValidateCustomerTimeout = validateCustomerTimeout;
    }

    public CustomerId CustomerId { get; }
    public string Iban { get; }
    public decimal OpeningAmount { get; }
    public Currency Currency { get; }
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>When <see langword="true"/>, the saga throws after the limit step to demonstrate compensation.</summary>
    public bool ForceFailureAfterLimit { get; }

    /// <summary>
    /// Optional artificial delay injected into the <c>validate-customer</c> step. When it exceeds that
    /// step's per-step timeout the saga cancels the step and rolls back, reporting a distinct
    /// timeout outcome rather than a business failure. Exists so the per-step timeout path can be
    /// exercised from a test or demo without depending on a genuinely slow dependency.
    /// </summary>
    public TimeSpan? ValidateCustomerDelay { get; }

    /// <summary>
    /// Optional override for the <c>validate-customer</c> step's per-step timeout budget. Null uses the
    /// saga's production default. Exists so a test can pin a small budget and exercise the timeout path
    /// deterministically without waiting out the production budget.
    /// </summary>
    public TimeSpan? ValidateCustomerTimeout { get; }

    /// <summary>Set by the create-account step; consumed by the compensations and the handler result.</summary>
    public AccountId? AccountId { get; set; }
}
