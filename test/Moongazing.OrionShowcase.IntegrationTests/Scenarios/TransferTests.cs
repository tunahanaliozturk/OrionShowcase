namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

public class TransferTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;

    public TransferTests(BankingApiFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task Transfer_moves_funds_and_updates_balances()
    {
        var client = _fx.CreateClient();

        var loginRes = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "demo", password = "demo" });
        var token = (await loginRes.Content.ReadFromJsonAsync<TokenBody>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var unique = Guid.NewGuid().ToString("N")[..8];
        var fullName = string.Format(CultureInfo.InvariantCulture, "Veli {0}", unique);
        var email = string.Format(CultureInfo.InvariantCulture, "veli-{0}@x.com", Guid.NewGuid().ToString("N"));

        var regRes = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName,
            nationalId = "10000000146",
            email,
            phone = "+905551234567",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        regRes.EnsureSuccessStatusCode();
        var customer = (await regRes.Content.ReadFromJsonAsync<RegisterBody>())!.CustomerId;

        var openA = await client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = customer,
            iban = "TR330006100519786457841326",
            openingAmount = 100m,
            currency = "TRY",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        openA.EnsureSuccessStatusCode();
        var a = (await openA.Content.ReadFromJsonAsync<AccountBody>())!.AccountId;

        var openB = await client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = customer,
            iban = "DE89370400440532013000",
            openingAmount = 0m,
            currency = "TRY",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        openB.EnsureSuccessStatusCode();
        var b = (await openB.Content.ReadFromJsonAsync<AccountBody>())!.AccountId;

        var transferUrl = string.Format(CultureInfo.InvariantCulture, "/api/accounts/{0}/transfer", a);
        var transferRes = await client.PostAsJsonAsync(transferUrl, new
        {
            toAccountId = b,
            amount = 30m,
            currency = "TRY",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        transferRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var balAUrl = string.Format(CultureInfo.InvariantCulture, "/api/accounts/{0}/balance", a);
        var balBUrl = string.Format(CultureInfo.InvariantCulture, "/api/accounts/{0}/balance", b);
        var balA = await client.GetFromJsonAsync<BalanceBody>(balAUrl);
        var balB = await client.GetFromJsonAsync<BalanceBody>(balBUrl);
        balA!.Balance.Should().Be(70m);
        balB!.Balance.Should().Be(30m);
    }

    private sealed record TokenBody(string AccessToken);
    private sealed record RegisterBody(Guid CustomerId);
    private sealed record AccountBody(Guid AccountId);
    private sealed record BalanceBody(Guid AccountId, decimal Balance, string Currency, string Status);
}
