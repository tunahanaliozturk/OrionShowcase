namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionRelay.Delivery;
using Moongazing.OrionShowcase.Api.Endpoints.Admin;
using Xunit;

/// <summary>
/// Unit-level coverage (no host / no Postgres) for the admin dead-letter diagnostics endpoint handler.
/// When Relay:CaptureDeadLetters is disabled, AddPartnerWebhooks does NOT register the
/// <see cref="InMemoryDeadLetterSink"/>, so the handler must resolve it OPTIONALLY and degrade to a
/// well-formed empty 200 response instead of throwing on a missing required service. The populated-sink
/// projection path is already covered end-to-end via the real dispatcher in the Application test suite
/// (WebhookDeadLetterTests).
/// </summary>
public sealed class WebhookDeadLettersEndpointTests
{
    [Fact]
    public void Handle_returns_an_empty_200_when_no_in_memory_sink_is_registered()
    {
        // Capture disabled: an empty provider has no InMemoryDeadLetterSink registration.
        using var provider = new ServiceCollection().BuildServiceProvider();

        var result = WebhookDeadLettersEndpoint.Handle(provider);

        var ok = result.Should().BeOfType<Ok<WebhookDeadLettersEndpoint.WebhookDeadLetterDto[]>>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeEmpty("the diagnostics endpoint degrades gracefully when capture is disabled");
    }
}
