using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Moongazing.OrionShowcase.Application.Pipeline;

public sealed partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _log;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> log) => _log = log;

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cross-cutting behavior must observe all exceptions then rethrow.")]
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive evaluation of arguments to log methods", Justification = "Stopwatch.GetElapsedTime is allocation-free arithmetic; cost is negligible compared to log level check.")]
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.GetTimestamp();
        LogStart(name);
        try
        {
            var response = await next().ConfigureAwait(false);
            LogEnd(name, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            LogFailure(name, Stopwatch.GetElapsedTime(sw).TotalMilliseconds, ex);
            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Handling {RequestName}.")]
    partial void LogStart(string requestName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs} ms.")]
    partial void LogEnd(string requestName, double elapsedMs);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed {RequestName} after {ElapsedMs} ms.")]
    partial void LogFailure(string requestName, double elapsedMs, Exception ex);
}
