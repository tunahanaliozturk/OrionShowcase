namespace Moongazing.OrionShowcase.Infrastructure.Time;

using Moongazing.OrionShowcase.Domain.Abstractions;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
