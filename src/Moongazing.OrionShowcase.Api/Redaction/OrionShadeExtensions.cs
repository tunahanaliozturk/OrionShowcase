namespace Moongazing.OrionShowcase.Api.Redaction;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShade;
using Moongazing.OrionShade.Redaction;

/// <summary>
/// Registration of OrionShade for the banking domain. Starts from the built-in rules (email, card,
/// JWT) and adds banking-specific PII patterns: the Turkish national id (TCKN) and phone numbers.
/// </summary>
public static class OrionShadeExtensions
{
    /// <summary>
    /// Register <see cref="IRedactor"/> with the default rules plus TCKN and phone patterns, and
    /// the sensitive key names used by the customer-data log sites.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddBankingRedaction(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOrionShade(shade => shade
            .UseDefaults()
            // TCKN: exactly 11 digits, anchored at word boundaries so it does not swallow longer
            // numeric runs (account numbers, amounts). Keep the last two digits for support triage.
            .AddRule("tckn", @"\b\d{11}\b", Masks.KeepLast(2, '*'))
            // Turkish phone numbers in +90 / 0 prefixed forms; masked entirely.
            .AddRule("phone", @"\b(?:\+90|0)\d{10}\b", Masks.Full())
            // Mask values wholesale when they arrive under one of these field names, regardless of
            // whether their content matches a pattern (e.g. a name field never matches a pattern).
            .AddSensitiveKeys("nationalId", "tckn", "phone", "email"));

        return services;
    }
}
