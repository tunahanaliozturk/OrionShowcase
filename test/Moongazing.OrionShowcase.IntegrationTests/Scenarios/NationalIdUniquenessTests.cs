namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

/// <summary>
/// End-to-end demonstration of Feature A (OrionGuard async validation) over Feature C (OrionVault
/// blind index). Registering a second customer with a national id already on file is rejected by
/// the async uniqueness rule, which resolves the duplicate through the deterministic blind index
/// without decrypting any row.
/// </summary>
public class NationalIdUniquenessTests : IClassFixture<BankingApiFixture>
{
    // Valid TCKN reserved for this scenario so it does not collide with other integration tests
    // that share the fixture database.
    private const string NationalId = "11111111110";

    private readonly BankingApiFixture _fx;

    public NationalIdUniquenessTests(BankingApiFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task Registering_a_duplicate_national_id_is_rejected_by_the_blind_index_lookup()
    {
        var client = _fx.CreateClient();

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { username = "demo", password = "demo" });
        var token = (await loginRes.Content.ReadFromJsonAsync<TokenBody>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var firstSuffix = Guid.NewGuid().ToString("N")[..8];
        var firstRes = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "First Holder",
            nationalId = NationalId,
            email = string.Format(CultureInfo.InvariantCulture, "first-{0}@example.com", firstSuffix),
            phone = "+905551110000",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        firstRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Same national id, everything else distinct (and a fresh idempotency key so the idempotency
        // layer does not short-circuit). The uniqueness rule must still reject it.
        var secondSuffix = Guid.NewGuid().ToString("N")[..8];
        var secondRes = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Second Holder",
            nationalId = NationalId,
            email = string.Format(CultureInfo.InvariantCulture, "second-{0}@example.com", secondSuffix),
            phone = "+905552220000",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });

        secondRes.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    private sealed record TokenBody(string AccessToken);
}
