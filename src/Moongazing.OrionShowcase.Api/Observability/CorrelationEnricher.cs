namespace Moongazing.OrionShowcase.Api.Observability;

using Moongazing.OrionLens.Context;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Serilog enricher that stamps every log event with the ambient OrionLens correlation id (and,
/// when present, the tenant baggage value) so logs emitted anywhere in the request flow can be
/// correlated back to one logical operation. The ambient context is established by
/// <c>app.UseOrionLens()</c> early in the pipeline.
/// </summary>
public sealed class CorrelationEnricher : ILogEventEnricher
{
    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var context = OrionContext.Current;
        if (context is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("CorrelationId", context.CorrelationId));

        var tenant = context.GetBaggage("tenant");
        if (!string.IsNullOrEmpty(tenant))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Tenant", tenant));
        }
    }
}
