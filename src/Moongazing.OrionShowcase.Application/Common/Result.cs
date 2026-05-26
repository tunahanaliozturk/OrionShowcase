using System.Diagnostics.CodeAnalysis;

namespace Moongazing.OrionShowcase.Application.Common;

[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory methods are idiomatic for a Result<T> discriminated union.")]
public sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
