namespace Moongazing.OrionShowcase.Api.Filters;

using FluentValidation;
using Microsoft.AspNetCore.Http;

public static class ValidationProblemFilter
{
    public static IResult Handle(ValidationException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var errors = ex.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }
}
