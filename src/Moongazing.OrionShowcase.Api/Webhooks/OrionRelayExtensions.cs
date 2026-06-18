namespace Moongazing.OrionShowcase.Api.Webhooks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionRelay;

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

        // OrionRelay: registers IWebhookDispatcher (HMAC signing + retry policy) over a named HttpClient.
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
