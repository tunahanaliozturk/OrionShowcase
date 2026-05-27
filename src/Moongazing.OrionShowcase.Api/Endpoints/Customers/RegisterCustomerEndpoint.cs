namespace Moongazing.OrionShowcase.Api.Endpoints.Customers;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class RegisterCustomerEndpoint
{
    public static IEndpointConventionBuilder MapRegisterCustomer(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/customers", Handle)
           .RequireAuthorization()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("RegisterCustomer")
           .WithTags("Customers")
           .Produces<RegisterCustomerResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        RegisterCustomerRequest req, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(new RegisterCustomerCommand(
                req.FullName,
                req.NationalId,
                req.Email,
                req.Phone,
                new IdempotencyKey(req.IdempotencyKey)), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(new RegisterCustomerResponse(result.Value!.CustomerId))
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record RegisterCustomerRequest(string FullName, string NationalId, string Email, string Phone, string IdempotencyKey);
    internal sealed record RegisterCustomerResponse(Guid CustomerId);
}
