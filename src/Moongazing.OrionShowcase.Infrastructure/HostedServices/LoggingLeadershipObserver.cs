namespace Moongazing.OrionShowcase.Infrastructure.HostedServices;

using Microsoft.Extensions.Logging;
using Moongazing.OrionBeacon.Leasing;
using Moongazing.OrionBeacon.Observers;

/// <summary>
/// Logs OrionBeacon leadership transitions so operators can see which replica currently owns
/// the leader-only settlement job.
/// </summary>
public sealed partial class LoggingLeadershipObserver : ILeadershipObserver
{
    private readonly ILogger<LoggingLeadershipObserver> _log;

    public LoggingLeadershipObserver(ILogger<LoggingLeadershipObserver> log) => _log = log;

    public void OnElected(Lease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        LogElected(lease.Resource, lease.HolderId, lease.FencingToken);
    }

    public void OnDeposed(string resource) => LogDeposed(resource);

    [LoggerMessage(EventId = 100, Level = LogLevel.Information,
        Message = "OrionBeacon: elected leader for {Resource} as {HolderId} (fencing token {FencingToken}).")]
    partial void LogElected(string resource, string holderId, long fencingToken);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information,
        Message = "OrionBeacon: deposed as leader for {Resource}.")]
    partial void LogDeposed(string resource);
}
