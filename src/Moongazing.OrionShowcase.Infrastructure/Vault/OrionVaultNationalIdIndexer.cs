namespace Moongazing.OrionShowcase.Infrastructure.Vault;

using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Moongazing.OrionVault.Abstractions;

/// <summary>
/// Infrastructure adapter from the Application <see cref="INationalIdIndexer"/> port to OrionVault's
/// HMAC-SHA256 <see cref="IBlindIndexProvider"/>. The provider is configured (key material + active
/// version) in <c>AddInfrastructure</c> via <c>OrionVaultOptions.UseBlindIndex</c> and resolved as a
/// singleton, so this adapter is a thin, allocation-light delegation.
/// </summary>
public sealed class OrionVaultNationalIdIndexer : INationalIdIndexer
{
    private readonly IBlindIndexProvider _provider;

    public OrionVaultNationalIdIndexer(IBlindIndexProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    public byte[] Compute(Tckn nationalId)
    {
        ArgumentNullException.ThrowIfNull(nationalId);
        return _provider.Compute(nationalId.Value).Bytes;
    }

    public IReadOnlyList<byte[]> ComputeAllVersions(Tckn nationalId)
    {
        ArgumentNullException.ThrowIfNull(nationalId);
        var results = _provider.ComputeAllVersions(nationalId.Value);
        var probes = new byte[results.Count][];
        for (var i = 0; i < results.Count; i++)
        {
            probes[i] = results[i].Bytes;
        }

        return probes;
    }
}
