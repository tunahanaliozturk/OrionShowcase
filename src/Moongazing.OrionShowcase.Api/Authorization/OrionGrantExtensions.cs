namespace Moongazing.OrionShowcase.Api.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionGrant;

/// <summary>
/// Registration of OrionGrant for the banking domain: the role-to-permission map enforced on top
/// of the existing JWT authentication.
/// </summary>
public static class OrionGrantExtensions
{
    /// <summary>
    /// Register <see cref="IGrantAuthorizer"/> with the banking roles. The <c>customer</c> role is
    /// the one minted by the demo login and is granted the self-service permissions
    /// (read/write own customer record, open and transfer on own accounts). Additional roles model
    /// staff and partner access for completeness.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddBankingAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOrionGrant(grant =>
        {
            // The demo login issues the "customer" role; it must retain access to the customer-write
            // and transfer endpoints that OrionGrant now guards.
            grant.AddRole(
                "customer",
                BankingPermissions.CustomersRead,
                BankingPermissions.CustomersWrite,
                BankingPermissions.AccountsOpen,
                BankingPermissions.AccountsTransfer);

            // A back-office teller can manage customers but not move customer money.
            grant.AddRole(
                "teller",
                BankingPermissions.CustomersRead,
                BankingPermissions.CustomersWrite,
                BankingPermissions.AccountsOpen);

            // A read-only partner/service identity (also used by API-key callers).
            grant.AddRole("partner", BankingPermissions.CustomersRead);
        });

        return services;
    }
}
