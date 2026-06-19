namespace Moongazing.OrionShowcase.Infrastructure.HostedServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moongazing.OrionPatch.Abstractions;

/// <summary>
/// Periodically reaps successfully dispatched outbox rows past the retention window via the
/// OrionPatch 0.3 <see cref="IOutboxArchivalStore"/>, moving them out of the hot
/// <c>orion_patch_outbox</c> table into the archive. Nothing in OrionPatch itself drives archival,
/// so the showcase supplies this host. The reap is idempotent and incremental, so running it is
/// safe even if more than one replica ticks; pending and dead-lettered rows are never touched.
/// </summary>
public sealed partial class OutboxArchivalService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly OutboxArchivalOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxArchivalService> _log;

    public OutboxArchivalService(
        IServiceProvider sp,
        OutboxArchivalOptions options,
        TimeProvider timeProvider,
        ILogger<OutboxArchivalService> log)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(log);
        _sp = sp;
        _options = options;
        _timeProvider = timeProvider;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.SweepInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var scope = _sp.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var archival = scope.ServiceProvider.GetRequiredService<IOutboxArchivalStore>();
                    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    var moved = await archival
                        .ArchiveProcessedAsync(_options.Retention, nowUtc, stoppingToken)
                        .ConfigureAwait(false);
                    if (moved > 0)
                    {
                        LogArchived(moved);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // A sweep fault must not stop the loop; log and retry on the next tick.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogSweepFailed(ex);
            }
        }
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Information,
        Message = "OrionPatch archival moved {Moved} processed outbox row(s) to the archive.")]
    partial void LogArchived(int moved);

    [LoggerMessage(EventId = 301, Level = LogLevel.Warning,
        Message = "OrionPatch archival sweep failed; will retry on the next tick.")]
    partial void LogSweepFailed(Exception exception);
}

/// <summary>Tuning for <see cref="OutboxArchivalService"/>.</summary>
public sealed class OutboxArchivalOptions
{
    /// <summary>How long a processed row is retained in the hot outbox after dispatch before archival.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How often the archival sweep runs.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(1);
}
