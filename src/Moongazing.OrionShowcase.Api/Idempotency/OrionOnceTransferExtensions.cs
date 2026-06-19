namespace Moongazing.OrionShowcase.Api.Idempotency;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionOnce;
using Moongazing.OrionOnce.Storage;

/// <summary>
/// Wiring for OrionOnce 0.2 explicit idempotent execution of money movements.
/// </summary>
/// <remarks>
/// The transfer endpoint runs the transfer through an <see cref="IdempotentExecutor"/> keyed on the
/// caller's <c>Idempotency-Key</c> header. The executor runs the operation once per key and replays
/// the captured result on any retry carrying the same key and request fingerprint, so a
/// double-submitted transfer (network retry, impatient client) is applied exactly once and the
/// original response (including the generated transfer id) is returned again.
/// <para>
/// The backing store here is the process-local <see cref="InMemoryIdempotencyStore"/>, which is
/// sufficient for this sample and for a single instance. A durable, shared store (Postgres / Redis)
/// is the production choice for a multi-replica deployment so replay survives restarts and spans
/// instances; swap the registration below for that store without touching the endpoint.
/// </para>
/// </remarks>
public static class OrionOnceTransferExtensions
{
    /// <summary>
    /// Default replay-retention window for captured transfer results.
    /// </summary>
    public static TimeSpan Retention { get; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Register the shared in-memory idempotency store and the <see cref="IdempotentExecutor"/>
    /// used by the transfer endpoint.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddTransferIdempotency(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Singleton so captured results are visible across requests for the lifetime of the process.
        // Production note: replace with a durable IIdempotencyStore for multi-instance replay.
        services.AddSingleton<IIdempotencyStore>(new InMemoryIdempotencyStore(Retention));
        services.AddSingleton<IdempotentExecutor>();

        return services;
    }
}
