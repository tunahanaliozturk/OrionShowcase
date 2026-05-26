using Moongazing.OrionShowcase.Domain.ValueObjects;

namespace Moongazing.OrionShowcase.Application.Pipeline;

public interface IIdempotentCommand
{
    IdempotencyKey IdempotencyKey { get; }
}
