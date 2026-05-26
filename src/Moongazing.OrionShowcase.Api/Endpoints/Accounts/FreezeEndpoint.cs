namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Application.Accounts.Commands.FreezeAccount;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class FreezeEndpoint
{
    public static IEndpointConventionBuilder MapFreeze(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/accounts/{id:guid}/freeze", Handle)
           .RequireAuthorization()
           .WithName("FreezeAccount")
           .WithTags("Accounts")
           .Produces(204)
           .ProducesValidationProblem()
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        Guid id, FreezeRequest req, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(
                new FreezeAccountCommand(new AccountId(id), req.Reason), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (ValidationException ex) { return ValidationProblemFilter.Handle(ex); }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record FreezeRequest(string Reason);
}
