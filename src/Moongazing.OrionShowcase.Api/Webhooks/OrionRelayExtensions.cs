namespace Moongazing.OrionShowcase.Api.Webhooks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moongazing.OrionRelay;
using Moongazing.OrionRelay.Delivery;
using Moongazing.OrionRelay.Observers;

/// <summary>
/// Registers OrionRelay (signed outbound webhooks) plus the showcase's partner-notification
/// glue: a typed notifier and, when no real partner is configured, a stub transport so the
/// app never fails dispatching to a missing endpoint.
/// </summary>
public static class OrionRelayExtensions
{
    // OrionRelay's typed HttpClient is registered under this name; the stub handler targets it.
    private const string WebhookClientName = "WebhookDispatcher";

    public static IServiceCollection AddPartnerWebhooks(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(PartnerWebhookOptions.SectionName);
        services.Configure<PartnerWebhookOptions>(section);

        var options = section.Get<PartnerWebhookOptions>() ?? new PartnerWebhookOptions();
        var signingSecret = string.IsNullOrWhiteSpace(options.SigningSecret)
            ? "orionshowcase-demo-webhook-secret"
            : options.SigningSecret;

        // OrionRelay 0.2 dead-lettering: opt in to the bounded in-memory sink BEFORE AddOrionRelay so
        // the dispatcher binds to it instead of the no-op NullDeadLetterSink default. A delivery that
        // exhausts its retry budget (or hits a fatal non-retryable status) is routed here exactly once
        // with its final failure context, instead of being lost. The sink is bounded (capacity) so a
        // prolonged partner outage cannot grow the working set without bound; a durable sink fits
        // production. Registered as a concrete singleton too so a diagnostics endpoint can read it.
        if (options.CaptureDeadLetters)
        {
            var sink = new InMemoryDeadLetterSink(
                options.DeadLetterCapacity > 0 ? options.DeadLetterCapacity : 256);
            services.AddSingleton(sink);
            services.TryAddSingleton<IDeadLetterSink>(sink);

            // Structured log line when a delivery is abandoned, in addition to the captured entry.
            services.TryAddSingleton<IWebhookDeliveryObserver, WebhookDeliveryLogObserver>();
        }

        // OrionRelay: registers IWebhookDispatcher (HMAC signing + retry policy) over a named HttpClient.
        // AddOrionRelay resolves IDeadLetterSink and IWebhookDeliveryObserver from DI (via GetService),
        // so the registrations above are picked up; absent them it falls back to the no-op defaults.
        services.AddOrionRelay(signingSecret, delivery =>
        {
            delivery.MaxAttempts = 3;
            delivery.RequestTimeout = TimeSpan.FromSeconds(5);
        });

        // No real receiver in the showcase: short-circuit the dispatcher's transport with a stub
        // that logs the signed request and returns 200. Active when no endpoint is set or stub is forced.
        var useStub = options.UseStubTransport || string.IsNullOrWhiteSpace(options.Endpoint);
        if (useStub)
        {
            services.AddTransient<StubWebhookHandler>();
            services.AddHttpClient(WebhookClientName)
                .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<StubWebhookHandler>());
        }

        services.AddScoped<PartnerWebhookNotifier>();
        return services;
    }
}
