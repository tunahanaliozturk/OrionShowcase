namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

public class RegisterAndOpenAccountTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;

    public RegisterAndOpenAccountTests(BankingApiFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task End_to_end_register_then_open_account_succeeds()
    {
        var client = _fx.CreateClient();

        // 1. Login (anonymous)
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { username = "demo", password = "demo" });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        // 2. Register customer
        var regRes = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Ali Veli",
            nationalId = "10000000146",
            email = "ali@example.com",
            phone = "+905551234567",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        regRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var regBody = await regRes.Content.ReadFromJsonAsync<RegisterBody>();
        regBody!.CustomerId.Should().NotBeEmpty();

        // 3. Open account
        var openRes = await client.PostAsJsonAsync("/api/accounts", new
        {
            customerId = regBody.CustomerId,
            iban = "TR330006100519786457841326",
            openingAmount = 100m,
            currency = "TRY",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        openRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var openBody = await openRes.Content.ReadFromJsonAsync<AccountBody>();
        openBody!.AccountId.Should().NotBeEmpty();
    }

    private sealed record LoginBody(string AccessToken);
    private sealed record RegisterBody(Guid CustomerId);
    private sealed record AccountBody(Guid AccountId);
}
