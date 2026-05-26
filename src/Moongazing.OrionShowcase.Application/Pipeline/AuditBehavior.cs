using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MediatR;
using Moongazing.OrionShowcase.Application.Abstractions;

namespace Moongazing.OrionShowcase.Application.Pipeline;

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuditableCommand
{
    private readonly IAuditWriter _audit;
    private readonly ICurrentUser _user;

    public AuditBehavior(IAuditWriter audit, ICurrentUser user)
    {
        _audit = audit;
        _user = user;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cross-cutting behavior must observe all exceptions then rethrow.")]
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        var actor = _user.IsAuthenticated ? _user.Username : "anonymous";
        var action = typeof(TRequest).Name;
        var requestJson = JsonSerializer.Serialize(request);
        try
        {
            var response = await next().ConfigureAwait(false);
            await _audit.WriteAsync(actor, action, requestJson, JsonSerializer.Serialize(response), true, null, cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            await _audit.WriteAsync(actor, action, requestJson, null, false, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
