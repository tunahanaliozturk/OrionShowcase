namespace Moongazing.OrionShowcase.Api.Streaming;

using System.Globalization;
using System.Text.Json;
using Moongazing.OrionStream.Streaming;

/// <summary>
/// Helpers for publishing account-activity events to the OrionStream SSE hub. Topics are keyed
/// per account (<c>account:{id}</c>) so a subscriber only receives events for the account it watches.
/// </summary>
public static class AccountActivityPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Builds the per-account SSE topic name.</summary>
    public static string TopicFor(Guid accountId) =>
        "account:" + accountId.ToString("N", CultureInfo.InvariantCulture);

    /// <summary>
    /// Publishes a <c>transfer.posted</c> activity event to both the source and destination
    /// account topics so either subscriber sees the balance movement in real time.
    /// </summary>
    public static void PublishTransferPosted(
        ISseHub hub,
        Guid transferId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency,
        decimal newSourceBalance)
    {
        ArgumentNullException.ThrowIfNull(hub);

        var debit = new
        {
            type = "transfer.posted",
            direction = "debit",
            transferId,
            accountId = fromAccountId,
            counterpartyId = toAccountId,
            amount,
            currency,
            balanceAfter = newSourceBalance,
            at = DateTimeOffset.UtcNow,
        };

        var credit = new
        {
            type = "transfer.posted",
            direction = "credit",
            transferId,
            accountId = toAccountId,
            counterpartyId = fromAccountId,
            amount,
            currency,
            at = DateTimeOffset.UtcNow,
        };

        hub.Publish(TopicFor(fromAccountId), ToEvent(transferId, debit));
        hub.Publish(TopicFor(toAccountId), ToEvent(transferId, credit));
    }

    private static ServerSentEvent ToEvent(Guid transferId, object payload) => new()
    {
        EventName = "activity",
        Id = transferId.ToString("N", CultureInfo.InvariantCulture),
        Data = JsonSerializer.Serialize(payload, JsonOptions),
    };
}
