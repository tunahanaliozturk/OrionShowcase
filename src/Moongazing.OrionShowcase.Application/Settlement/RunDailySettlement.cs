namespace Moongazing.OrionShowcase.Application.Settlement;

using Microsoft.Extensions.Logging;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed partial class RunDailySettlement
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ILogger<RunDailySettlement> _log;

    public RunDailySettlement(IUnitOfWork uow, IClock clock, ILogger<RunDailySettlement> log)
    {
        _uow = uow; _clock = clock; _log = log;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var start = _clock.UtcNow;
        LogStart(start);

        // Showcase placeholder: in real banking this would close out interest,
        // generate end-of-day statements, etc. For v0.1.0 we just persist a marker save.
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogDone(_clock.UtcNow - start);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Daily settlement started at {Start}.")]
    partial void LogStart(DateTimeOffset start);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Daily settlement done in {Elapsed}.")]
    partial void LogDone(TimeSpan elapsed);
}
