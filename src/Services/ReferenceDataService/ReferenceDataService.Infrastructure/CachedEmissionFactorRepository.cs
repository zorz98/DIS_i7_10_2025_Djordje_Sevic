using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ReferenceDataService.Domain;
using StackExchange.Redis;

namespace ReferenceDataService.Infrastructure;

/// <summary>
/// Cache-aside decorator around <see cref="EmissionFactorRepository"/>. Emission
/// factors are static seed data with no runtime write path anywhere in this
/// service, so a generous TTL is safe — it exists only to avoid special-casing
/// "how would this ever refresh" rather than to track real invalidation.
/// Redis is a pure best-effort accelerator here: any cache failure is caught and
/// treated as a miss, falling through to the database, which is always correct.
/// </summary>
public sealed class CachedEmissionFactorRepository(
    IEmissionFactorRepository inner,
    IDistributedCache cache,
    ILogger<CachedEmissionFactorRepository> logger) : IEmissionFactorRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private const string AllKey = "emission-factors:all";
    private const string CategoryKeyPrefix = "emission-factor:";

    private sealed record CachedFactor(int Id, string Category, decimal Co2FactorPerEur)
    {
        public EmissionFactor ToDomain() => new() { Id = Id, Category = Category, Co2FactorPerEur = Co2FactorPerEur };

        public static CachedFactor FromDomain(EmissionFactor factor) => new(factor.Id, factor.Category, factor.Co2FactorPerEur);
    }

    public async Task<IReadOnlyList<EmissionFactor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cached = await TryGetAsync<List<CachedFactor>>(AllKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Select(f => f.ToDomain()).ToList();
        }

        var factors = await inner.GetAllAsync(cancellationToken);
        await TrySetAsync(AllKey, factors.Select(CachedFactor.FromDomain).ToList(), cancellationToken);
        return factors;
    }

    public async Task<EmissionFactor?> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var key = CategoryKeyPrefix + category;
        var cached = await TryGetAsync<CachedFactor>(key, cancellationToken);
        if (cached is not null)
        {
            return cached.ToDomain();
        }

        var factor = await inner.GetByCategoryAsync(category, cancellationToken);
        if (factor is not null)
        {
            await TrySetAsync(key, CachedFactor.FromDomain(factor), cancellationToken);
        }

        return factor;
    }

    private async Task<T?> TryGetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken);
            return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis cache read failed for key {Key}; falling back to the database", key);
            return null;
        }
    }

    private async Task TrySetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await cache.SetAsync(
                key,
                bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
                cancellationToken);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis cache write failed for key {Key}; continuing without caching this result", key);
        }
    }
}
