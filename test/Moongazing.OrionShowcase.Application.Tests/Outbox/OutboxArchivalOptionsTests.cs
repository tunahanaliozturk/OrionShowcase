namespace Moongazing.OrionShowcase.Application.Tests.Outbox;

using FluentAssertions;
using Moongazing.OrionShowcase.Infrastructure.HostedServices;
using Xunit;

/// <summary>
/// Validates the archival config bounds enforced at startup by <see cref="OutboxArchivalOptions.Create"/>.
/// A negative retention would move the cutoff into the future and reap rows still inside their intended
/// window; a zero/negative sweep interval makes <see cref="System.Threading.PeriodicTimer"/> throw and
/// would otherwise hot-loop the sweep. The factory fails fast so a bad appsettings value stops startup
/// with a clear message instead of corrupting data or spinning a background loop.
/// </summary>
public sealed class OutboxArchivalOptionsTests
{
    [Fact]
    public void Create_accepts_a_positive_retention_and_sweep_interval()
    {
        var options = OutboxArchivalOptions.Create(TimeSpan.FromDays(7), TimeSpan.FromMinutes(60));

        options.Retention.Should().Be(TimeSpan.FromDays(7));
        options.SweepInterval.Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void Create_accepts_zero_retention_archive_as_soon_as_processed()
    {
        // Zero retention is a legitimate "archive as soon as a row is processed" policy.
        var options = OutboxArchivalOptions.Create(TimeSpan.Zero, TimeSpan.FromMinutes(1));

        options.Retention.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Create_rejects_a_negative_retention()
    {
        var act = () => OutboxArchivalOptions.Create(TimeSpan.FromDays(-1), TimeSpan.FromMinutes(60));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("retention");
    }

    [Fact]
    public void Create_rejects_a_zero_sweep_interval()
    {
        var act = () => OutboxArchivalOptions.Create(TimeSpan.FromDays(7), TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("sweepInterval");
    }

    [Fact]
    public void Create_rejects_a_negative_sweep_interval()
    {
        var act = () => OutboxArchivalOptions.Create(TimeSpan.FromDays(7), TimeSpan.FromMinutes(-1));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("sweepInterval");
    }
}
