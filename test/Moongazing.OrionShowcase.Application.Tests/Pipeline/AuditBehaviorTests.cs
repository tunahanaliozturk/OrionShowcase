using FluentAssertions;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Pipeline;
using Xunit;

namespace Moongazing.OrionShowcase.Application.Tests.Pipeline;

public class AuditBehaviorTests
{
    public sealed record SampleCommand(string X) : IAuditableCommand;

    private sealed class CapturingAudit : IAuditWriter
    {
        public List<(string actor, string action, string req, string? res, bool ok, string? err)> Records { get; } = new();

        public Task WriteAsync(string actor, string action, string requestJson, string? responseJson, bool succeeded, string? errorMessage, CancellationToken cancellationToken)
        {
            Records.Add((actor, action, requestJson, responseJson, succeeded, errorMessage));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string Id => "1";
        public string Username => "demo";
        public bool IsAuthenticated => true;
    }

    [Fact]
    public async Task Writes_success_record_when_handler_returns()
    {
        var audit = new CapturingAudit();
        var sut = new AuditBehavior<SampleCommand, string>(audit, new FixedUser());
        await sut.Handle(new SampleCommand("hello"), () => Task.FromResult("ok"), default);

        audit.Records.Should().ContainSingle();
        var rec = audit.Records[0];
        rec.actor.Should().Be("demo");
        rec.action.Should().Be("SampleCommand");
        rec.ok.Should().BeTrue();
    }

    [Fact]
    public async Task Writes_failure_record_when_handler_throws()
    {
        var audit = new CapturingAudit();
        var sut = new AuditBehavior<SampleCommand, string>(audit, new FixedUser());
        var act = async () => await sut.Handle(new SampleCommand("hello"), () => throw new InvalidOperationException("boom"), default);
        await act.Should().ThrowAsync<InvalidOperationException>();

        audit.Records.Should().ContainSingle();
        audit.Records[0].ok.Should().BeFalse();
        audit.Records[0].err.Should().Be("boom");
    }
}
