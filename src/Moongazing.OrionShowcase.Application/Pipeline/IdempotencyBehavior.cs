using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Moongazing.OrionShowcase.Application.Abstractions;

namespace Moongazing.OrionShowcase.Application.Pipeline;

public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand
{
    private readonly IIdempotencyStore _store;

    public IdempotencyBehavior(IIdempotencyStore store) => _store = store;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var key = request.IdempotencyKey.Value;
        var hash = ComputeHash(request);

        var cached = await _store.GetCachedResponseAsync(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            var deserialised = JsonSerializer.Deserialize<TResponse>(cached)
                ?? throw new InvalidOperationException("Cached response deserialised to null.");
            return deserialised;
        }

        if (!await _store.TryClaimAsync(key, hash, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Idempotency key '{key}' is in flight with a different request.");
        }

        var response = await next().ConfigureAwait(false);
        await _store.StoreResponseAsync(key, JsonSerializer.Serialize(response), cancellationToken).ConfigureAwait(false);
        return response;
    }

    private static string ComputeHash(TRequest request)
    {
        var json = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}
