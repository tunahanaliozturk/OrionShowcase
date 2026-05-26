namespace Moongazing.OrionShowcase.Infrastructure.HostedServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Settlement;
using Moongazing.OrionShowcase.Domain.Abstractions;

public sealed partial class DailySettlementService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IDistributedLock _locks;
    private readonly IClock _clock;
    private readonly ILogger<DailySettlementService> _log;

    public DailySettlementService(IServiceProvider sp, IDistributedLock locks, IClock clock, ILogger<DailySettlementService> log)
    {
        _sp = sp; _locks = locks; _clock = clock; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(NextRunDelay(_clock.UtcNow), stoppingToken).ConfigureAwait(false);

            var lease = await _locks.TryAcquireAsync("settlement:daily", null, stoppingToken).ConfigureAwait(false);
            if (lease is null) { LogSkipped(); continue; }

            await using (lease.ConfigureAwait(false))
            {
                var scope = _sp.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var runner = scope.ServiceProvider.GetRequiredService<RunDailySettlement>();
                    await runner.ExecuteAsync(stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }

    internal static TimeSpan NextRunDelay(DateTimeOffset now)
    {
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, 23, 55, 0, TimeSpan.Zero);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Settlement skipped: another instance holds the lock.")]
    partial void LogSkipped();
}
