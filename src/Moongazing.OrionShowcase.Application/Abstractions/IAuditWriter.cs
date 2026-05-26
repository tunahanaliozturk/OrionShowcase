namespace Moongazing.OrionShowcase.Application.Abstractions;

public interface IAuditWriter
{
    Task WriteAsync(string actor, string action, string requestJson, string? responseJson, bool succeeded, string? errorMessage, CancellationToken cancellationToken);
}
