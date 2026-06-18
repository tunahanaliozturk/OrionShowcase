namespace Moongazing.OrionShowcase.Api.Endpoints;

using Moongazing.OrionShowcase.Api.Endpoints.Accounts;
using Moongazing.OrionShowcase.Api.Endpoints.Auth;
using Moongazing.OrionShowcase.Api.Endpoints.Customers;
using Moongazing.OrionShowcase.Api.Endpoints.Partner;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapBankingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapLogin();
        app.MapRegisterCustomer();
        app.MapOpenAccount();
        app.MapDeposit();
        app.MapWithdraw();
        app.MapTransfer();
        app.MapFreeze();
        app.MapClose();
        app.MapGetBalance();
        app.MapGetTransactions();
        app.MapAccountActivityStream();
        app.MapPartnerBalance();
        return app;
    }
}
