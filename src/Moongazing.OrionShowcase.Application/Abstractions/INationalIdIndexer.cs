namespace Moongazing.OrionShowcase.Application.Abstractions;

using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Application port over the OrionVault deterministic blind index for a customer's national id.
/// The Infrastructure adapter delegates to OrionVault's <c>IBlindIndexProvider</c> (HMAC-SHA256).
/// Keeping the port in the Application layer lets handlers and validators stay unit-testable
/// without referencing the cryptographic provider, and keeps the OrionVault key material wiring
/// in Infrastructure where the rest of the encryption configuration lives.
/// </summary>
public interface INationalIdIndexer
{
    /// <summary>
    /// Computes the blind index for <paramref name="nationalId"/> under the active key version.
    /// Equal national ids always yield byte-identical output, which is what an equality lookup
    /// on the stored index column relies on. Used when writing a new customer row.
    /// </summary>
    byte[] Compute(Tckn nationalId);

    /// <summary>
    /// Computes the blind index for an equality probe under every registered key version
    /// (newest first), so a lookup matches rows written under the current key as well as rows
    /// still carrying an index from a retained older key.
    /// </summary>
    IReadOnlyList<byte[]> ComputeAllVersions(Tckn nationalId);
}
