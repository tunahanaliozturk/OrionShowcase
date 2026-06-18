namespace Moongazing.OrionShowcase.Api.Endpoints.Customers;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moongazing.OrionShade;
using Moongazing.OrionShowcase.Api.Authorization;
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
           .RequirePermission(BankingPermissions.CustomersWrite)
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("RegisterCustomer")
           .WithTags("Customers")
           .Produces<RegisterCustomerResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(403)
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        RegisterCustomerRequest req,
        IMediator mediator,
        IRedactor redactor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        // OrionShade: redact PII before it reaches the log sink. The national id and phone are
        // masked by their field name; the email is caught by the built-in email pattern as well.
        var logger = loggerFactory.CreateLogger("Customers.Register");
        logger.LogInformation(
            "Registering customer name={Name} nationalId={NationalId} email={Email} phone={Phone}",
            redactor.Redact(req.FullName),
            redactor.RedactValue("nationalId", req.NationalId),
            redactor.RedactValue("email", req.Email),
            redactor.RedactValue("phone", req.Phone));

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
