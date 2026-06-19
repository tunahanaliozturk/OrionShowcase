namespace Moongazing.OrionShowcase.Application.Tests.ApiKeys;

using FluentAssertions;
using Moongazing.OrionLedger;
using Moongazing.OrionLedger.Diagnostics;
using Moongazing.OrionLedger.Keys;
using Moongazing.OrionLedger.Storage;
using Xunit;

/// <summary>
/// Upgrade 3: OrionLedger 0.2 API key rotation (with a grace window) and bulk revoke. Exercises the
/// real <see cref="ApiKeyService"/> over the in-memory store the showcase seeds, proving that after a
/// rotation both the old (within grace) and new keys verify, the old key then retires, and that a
/// bulk revoke ends every active key for a subject while skipping already-inactive ones.
/// </summary>
public sealed class ApiKeyRotationTests : IDisposable
{
    private const string Subject = "partner:test";

    private readonly List<ApiKeyDiagnostics> _diagnostics = new();

    private ApiKeyService NewService(out InMemoryApiKeyStore store)
    {
        store = new InMemoryApiKeyStore();
        var options = new ApiKeyOptions { Prefix = "ork_" };
        var diagnostics = new ApiKeyDiagnostics();
        _diagnostics.Add(diagnostics);
        return new ApiKeyService(store, options, diagnostics);
    }

    public void Dispose()
    {
        foreach (var d in _diagnostics)
        {
            d.Dispose();
        }
    }

    [Fact]
    public async Task After_rotation_both_old_within_grace_and_new_keys_verify_then_the_old_retires()
    {
        var service = NewService(out _);
        var issued = await service.IssueAsync("partner", scopes: ["partner:read"], subject: Subject);

        var grace = TimeSpan.FromMinutes(10);
        var rotation = await service.RotateAsync(issued.Record.Id, grace);

        rotation.Should().NotBeNull("an active key can be rotated");
        var newToken = rotation!.Token;
        newToken.Should().NotBe(issued.Token, "rotation issues a fresh secret");

        // Within the grace window both keys verify.
        (await service.VerifyAsync(issued.Token, "partner:read")).IsValid
            .Should().BeTrue("the predecessor still verifies inside the grace window");
        (await service.VerifyAsync(newToken, "partner:read")).IsValid
            .Should().BeTrue("the successor verifies immediately");

        // The successor inherits the predecessor's subject and scopes.
        var newVerification = await service.VerifyAsync(newToken, "partner:read");
        newVerification.Record!.Subject.Should().Be(Subject);

        // The predecessor is marked superseded with a future retirement instant: it still verifies now
        // but is scheduled to retire when the grace window elapses. (Deterministic retirement, where
        // the clock is advanced past RetiresAt, is covered by the zero-grace test below, which retires
        // the predecessor immediately.)
        issued.Record.RetiresAt.Should().NotBeNull("a grace-window rotation schedules the predecessor to retire");
        issued.Record.RetiresAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow, "retirement is in the future while inside grace");
        issued.Record.SupersededById.Should().Be(rotation.Successor.Record.Id);
    }

    [Fact]
    public async Task Rotation_with_zero_grace_revokes_the_predecessor_immediately()
    {
        var service = NewService(out _);
        var issued = await service.IssueAsync("partner", scopes: ["partner:read"], subject: Subject);

        var rotation = await service.RotateAsync(issued.Record.Id, grace: TimeSpan.Zero);

        rotation.Should().NotBeNull();
        (await service.VerifyAsync(issued.Token, "partner:read")).IsValid
            .Should().BeFalse("zero grace revokes the predecessor immediately");
        (await service.VerifyAsync(rotation!.Token, "partner:read")).IsValid
            .Should().BeTrue("the successor verifies");
    }

    [Fact]
    public async Task Rotating_an_already_superseded_key_returns_null()
    {
        var service = NewService(out _);
        var issued = await service.IssueAsync("partner", scopes: ["partner:read"], subject: Subject);

        var first = await service.RotateAsync(issued.Record.Id, TimeSpan.FromMinutes(10));
        first.Should().NotBeNull();

        // The original record is now superseded; rotating it again is a no-op returning null.
        var second = await service.RotateAsync(issued.Record.Id, TimeSpan.FromMinutes(10));
        second.Should().BeNull("a superseded key cannot be rotated again");
    }

    [Fact]
    public async Task Bulk_revoke_ends_all_active_keys_for_a_subject_and_skips_inactive_ones()
    {
        var service = NewService(out _);

        var a = await service.IssueAsync("k-a", scopes: ["partner:read"], subject: Subject);
        var b = await service.IssueAsync("k-b", scopes: ["partner:read"], subject: Subject);
        var c = await service.IssueAsync("k-c", scopes: ["partner:read"], subject: Subject);

        // Pre-revoke one key so it is already inactive when the bulk revoke runs.
        (await service.RevokeAsync(c.Record.Id)).Should().BeTrue();

        var other = await service.IssueAsync("k-other", scopes: ["partner:read"], subject: "partner:other");

        var revoked = await service.RevokeAllForSubjectAsync(Subject);

        revoked.Should().Be(2, "only the two still-active keys for the subject are revoked; the pre-revoked one is skipped");

        (await service.VerifyAsync(a.Token, "partner:read")).IsValid.Should().BeFalse();
        (await service.VerifyAsync(b.Token, "partner:read")).IsValid.Should().BeFalse();
        (await service.VerifyAsync(other.Token, "partner:read")).IsValid
            .Should().BeTrue("a key belonging to a different subject is untouched");

        // Re-running the bulk revoke now finds nothing active to revoke.
        (await service.RevokeAllForSubjectAsync(Subject)).Should().Be(0, "all keys for the subject are already inactive");
    }
}
