namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using Npgsql;
using Xunit;

public class PiiEncryptionTests : IClassFixture<BankingApiFixture>
{
    private const string PlaintextNationalId = "10000000146";

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
            nationalId = PlaintextNationalId,
            email,
            phone = "+905551234567",
            idempotencyKey = Guid.NewGuid().ToString("N"),
        });
        regRes.EnsureSuccessStatusCode();

        // The national_id column is bytea: OrionVault's EncryptedStringConverter (composed on top of
        // the Tckn<->string converter) maps the value to byte[], persisted as raw bytes. Read those
        // bytes straight off the column - there is no Base64 layer; the on-disk value IS the
        // OrionVault envelope.
        await using var conn = new NpgsqlConnection(_fx.PostgresConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT national_id FROM customers WHERE full_name = @name LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@name", fullName);
        var stored = await cmd.ExecuteScalarAsync();

        stored.Should().BeOfType<byte[]>("the national_id column is bytea, so Npgsql returns raw bytes");
        var rawBytes = (byte[])stored!;

        // The ciphertext envelope is [keyId:2 BE][nonce:12][tag:16][body:N]: a 30-byte header before
        // the encrypted body. For an 11-character national id the payload is 41 bytes total, so it
        // must comfortably exceed the 30-byte minimum and must not be the plaintext.
        rawBytes.Length.Should().BeGreaterThan(
            30,
            "OrionVault prepends a 30-byte header (keyId + nonce + tag) before the ciphertext body");

        var plaintextBytes = Encoding.UTF8.GetBytes(PlaintextNationalId);
        rawBytes.Should().NotEqual(plaintextBytes, "the value must be encrypted at rest, not stored as plaintext");

        // KeyId is written big-endian as the first 2 bytes of the header. The fixture configures a
        // single key with id = 1.
        rawBytes[0].Should().Be(0);
        rawBytes[1].Should().Be(1);

        // The raw payload, decoded as UTF-8, must not leak the plaintext national id anywhere.
        Encoding.UTF8.GetString(rawBytes).Should().NotContain(PlaintextNationalId);

        // Round-trip: reading the row back through the app's DbContext runs OrionVault's decrypting
        // converter, so a correctly encrypted value is fully recoverable as the original plaintext.
        // This proves the bytes above are genuine ciphertext, not just opaque bytes.
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        // Materialize the entity (NationalId is a value-converted Tckn, so the decryption runs as the
        // row is read) and inspect the recovered plaintext in memory rather than projecting the
        // converted member in SQL.
        var customer = await db.Customers
            .AsNoTracking()
            .SingleAsync(c => c.FullName == fullName);

        customer.NationalId.Value.Should().Be(
            PlaintextNationalId,
            "the encrypted national id must decrypt back to the original");
    }

    private sealed record TokenBody(string AccessToken);
}
