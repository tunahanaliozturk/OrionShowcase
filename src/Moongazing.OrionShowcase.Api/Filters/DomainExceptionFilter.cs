namespace Moongazing.OrionShowcase.Api.Filters;

using Microsoft.AspNetCore.Http;
using Moongazing.OrionShowcase.Domain.Accounts;

public static class DomainExceptionFilter
{
    public static IResult? TryHandle(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ex switch
        {
            InsufficientFundsException => Results.Problem(detail: "Insufficient funds.", statusCode: 409),
            AccountNotActiveException ana => Results.Problem(detail: ana.Message, statusCode: 409),
            AccountNotEmptyException => Results.Problem(detail: "Account has non-zero balance.", statusCode: 409),
            _ => null
        };
    }
}
