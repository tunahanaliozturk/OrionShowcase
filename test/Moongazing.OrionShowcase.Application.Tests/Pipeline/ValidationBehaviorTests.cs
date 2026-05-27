using FluentAssertions;
using MediatR;
using Moongazing.OrionGuard.Compatibility;
using Moongazing.OrionGuard.Core;
using Moongazing.OrionGuard.DependencyInjection;
using Moongazing.OrionShowcase.Application.Pipeline;
using Xunit;

namespace Moongazing.OrionShowcase.Application.Tests.Pipeline;

public class ValidationBehaviorTests
{
    public sealed record SampleRequest(string Name);

    private sealed class FailingValidator : FluentStyleValidator<SampleRequest>
    {
        public FailingValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    [Fact]
    public async Task Throws_AggregateValidationException_when_request_invalid()
    {
        var sut = new ValidationBehavior<SampleRequest, Unit>(new IValidator<SampleRequest>[] { new FailingValidator() });
        var act = async () => await sut.Handle(new SampleRequest(""), () => Task.FromResult(Unit.Value), default);
        await act.Should().ThrowAsync<AggregateValidationException>();
    }

    [Fact]
    public async Task Calls_next_when_request_valid()
    {
        var sut = new ValidationBehavior<SampleRequest, Unit>(new IValidator<SampleRequest>[] { new FailingValidator() });
        var result = await sut.Handle(new SampleRequest("ali"), () => Task.FromResult(Unit.Value), default);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Skips_validation_when_no_validators_registered()
    {
        var sut = new ValidationBehavior<SampleRequest, Unit>(Array.Empty<IValidator<SampleRequest>>());
        var result = await sut.Handle(new SampleRequest(""), () => Task.FromResult(Unit.Value), default);
        result.Should().Be(Unit.Value);
    }
}
