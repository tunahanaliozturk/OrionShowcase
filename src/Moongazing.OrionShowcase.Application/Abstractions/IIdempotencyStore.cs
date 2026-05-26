namespace Moongazing.OrionShowcase.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken cancellationToken);
    Task<string?> GetCachedResponseAsync(string key, CancellationToken cancellationToken);
    Task StoreResponseAsync(string key, string serialisedResponse, CancellationToken cancellationToken);
}
