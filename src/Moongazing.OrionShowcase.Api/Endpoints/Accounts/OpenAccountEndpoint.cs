namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class OpenAccountEndpoint
{
    public static IEndpointConventionBuilder MapOpenAccount(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/accounts", Handle)
           .RequireAuthorization()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("OpenAccount")
           .WithTags("Accounts")
           .Produces<OpenAccountResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        OpenAccountRequest req, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(new OpenAccountCommand(
                new CustomerId(req.CustomerId),
                req.Iban,
                req.OpeningAmount,
                Enum.Parse<Currency>(req.Currency),
                new IdempotencyKey(req.IdempotencyKey)), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(new OpenAccountResponse(result.Value!.AccountId))
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (ValidationException ex) { return ValidationProblemFilter.Handle(ex); }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record OpenAccountRequest(Guid CustomerId, string Iban, decimal OpeningAmount, string Currency, string IdempotencyKey);
    internal sealed record OpenAccountResponse(Guid AccountId);
}
