namespace Moongazing.OrionShowcase.Infrastructure.Time;

using Moongazing.Orion.Abstractions.Time;
using Moongazing.OrionShowcase.Domain.Abstractions;

/// <summary>
/// Single wall-clock implementation that satisfies both the domain's <see cref="IClock"/> and
/// Orion's <see cref="IOrionClock"/> contract, so the showcase and the Orion packages read time
/// from the same source.
/// </summary>
public sealed class SystemClock : IClock, IOrionClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

    public TimeSpan GetElapsedTime(long startingTimestamp) =>
        System.Diagnostics.Stopwatch.GetElapsedTime(startingTimestamp);
}
