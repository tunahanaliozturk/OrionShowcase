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
        bool forceFailureAfterLimit = false)
    {
        CustomerId = customerId;
        Iban = iban;
        OpeningAmount = openingAmount;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
        ForceFailureAfterLimit = forceFailureAfterLimit;
    }

    public CustomerId CustomerId { get; }
    public string Iban { get; }
    public decimal OpeningAmount { get; }
    public Currency Currency { get; }
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>When <see langword="true"/>, the saga throws after the limit step to demonstrate compensation.</summary>
    public bool ForceFailureAfterLimit { get; }

    /// <summary>Set by the create-account step; consumed by the compensations and the handler result.</summary>
    public AccountId? AccountId { get; set; }
}
