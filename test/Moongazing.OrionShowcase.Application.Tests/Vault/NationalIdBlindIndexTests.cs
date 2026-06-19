namespace Moongazing.OrionShowcase.Application.Tests.Vault;

using System.Text;
using FluentAssertions;
using Moongazing.OrionVault.BlindIndex;
using Xunit;

/// <summary>
/// Feature C: OrionVault 0.3 searchable encryption via the deterministic blind index. Proves the
/// guarantees the customer national-id lookup relies on: equal plaintexts produce a byte-identical
/// index (so an equality predicate finds the row), different plaintexts produce different indexes,
/// and the index reveals nothing resembling the plaintext.
/// </summary>
public class NationalIdBlindIndexTests
{
    private const string TcknA = "10000000146";
    private const string TcknB = "29000000004";

    // Demo-only HMAC index key, 32 fake bytes. Mirrors the configured Vault:BlindIndexKey1 shape.
    private static HmacBlindIndexProvider NewProvider()
    {
        var key = new byte[32];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = (byte)'B';
        }

        var keys = new Dictionary<short, byte[]> { [1] = key };
        return new HmacBlindIndexProvider(keys, activeVersion: 1, BlindIndexNormalization.None);
    }

    [Fact]
    public void Equal_national_ids_share_a_byte_identical_index()
    {
        var provider = NewProvider();

        var first = provider.Compute(TcknA).Bytes;
        var second = provider.Compute(TcknA).Bytes;

        second.Should().Equal(first, "equality lookups depend on equal plaintexts hashing identically");
    }

    [Fact]
    public void Different_national_ids_produce_different_indexes()
    {
        var provider = NewProvider();

        var a = provider.Compute(TcknA).Bytes;
        var b = provider.Compute(TcknB).Bytes;

        b.Should().NotEqual(a);
    }

    [Fact]
    public void Matches_resolves_the_probe_against_a_stored_index()
    {
        var provider = NewProvider();
        var stored = provider.Compute(TcknA).Bytes;

        provider.Matches(TcknA, stored).Should().BeTrue();
        provider.Matches(TcknB, stored).Should().BeFalse();
    }

    [Fact]
    public void Index_does_not_leak_the_plaintext()
    {
        var provider = NewProvider();

        var bytes = provider.Compute(TcknA).Bytes;

        Encoding.UTF8.GetString(bytes).Should().NotContain(TcknA);
    }

    [Fact]
    public void ComputeAllVersions_includes_the_active_version_for_lookup()
    {
        var provider = NewProvider();

        var probes = provider.ComputeAllVersions(TcknA);
        var active = provider.Compute(TcknA).Bytes;

        probes.Should().NotBeEmpty();
        probes.Select(p => p.Bytes).Should().ContainEquivalentOf(active);
    }
}
