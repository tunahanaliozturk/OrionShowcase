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

        hub.Publish(TopicFor(fromAccountId), ToEvent(WireId(transferId, "debit"), debit));
        hub.Publish(TopicFor(toAccountId), ToEvent(WireId(transferId, "credit"), credit));
    }

    /// <summary>
    /// Builds the stable, resume-usable wire id stamped on an emitted event (the SSE <c>id:</c>
    /// field). OrionStream 0.2 resume matches a reconnecting client's <c>Last-Event-ID</c> against
    /// this exact value, so it must be stable (the same logical event always renders the same id)
    /// and unique within a topic. The transfer id plus the leg direction satisfies both: a single
    /// account that is both source and destination of the same transfer (a self-transfer) would
    /// otherwise see two events with the same id on one topic, which would make the resume cursor
    /// ambiguous.
    /// </summary>
    private static string WireId(Guid transferId, string leg) =>
        transferId.ToString("N", CultureInfo.InvariantCulture) + ":" + leg;

    private static ServerSentEvent ToEvent(string wireId, object payload) => new()
    {
        EventName = "activity",
        // Producer-supplied id. OrionStream uses it verbatim as the wire id and as the resume
        // cursor; it takes precedence over the hub's monotonic sequence.
        Id = wireId,
        Data = JsonSerializer.Serialize(payload, JsonOptions),
    };
}
