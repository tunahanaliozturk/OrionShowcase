namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Npgsql;
using Xunit;

public class PiiEncryptionTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;

    public PiiEncryptionTests(BankingApiFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task Customer_national_id_is_stored_as_ciphertext_in_postgres()
    {
        var client = _fx.CreateClient();

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { username = "demo", password = "demo" });
        var token = (await loginRes.Content.ReadFromJsonAsync<TokenBody>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var unique = Guid.NewGuid().ToString("N")[..8];
        var fullName = string.Format(CultureInfo.InvariantCulture, "Ali Veli {0}", unique);
        var email = string.Format(CultureInfo.InvariantCulture, "ali-{0}@example.com", unique);
        var regRes = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName,
            nationalId = "10000000146",
            email,
            phone = "+905551234567",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        regRes.EnsureSuccessStatusCode();

        await using var conn = new NpgsqlConnection(_fx.PostgresConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT national_id FROM customers WHERE full_name = @name LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@name", fullName);
        var storedNationalId = (string?)await cmd.ExecuteScalarAsync();

        storedNationalId.Should().NotBeNullOrEmpty();

        // OrionVault EncryptedStringConverter stores ciphertext as a Base64 string in the
        // configured column. The on-disk payload must not equal the plaintext, must decode
        // as Base64, and the decoded bytes carry the OrionVault header (keyId + nonce + tag)
        // followed by the encrypted body.
        storedNationalId.Should().NotBe("10000000146");
        storedNationalId.Should().NotContain("10000000146");

        var rawBytes = Convert.FromBase64String(storedNationalId!);
        rawBytes.Length.Should().BeGreaterThan(
            30,
            "OrionVault prepends a header (keyId + nonce + tag) of at least 30 bytes before the ciphertext body");

        // KeyId is written big-endian as the first 2 bytes of the header. The fixture
        // configures a single key with id = 1.
        rawBytes[0].Should().Be(0);
        rawBytes[1].Should().Be(1);

        // Sanity check: the raw payload as UTF-8 must not leak the plaintext anywhere.
        Encoding.UTF8.GetString(rawBytes).Should().NotContain("10000000146");
    }

    private sealed record TokenBody(string AccessToken);
}
