using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ReportService.Api;

/// <summary>
/// Short-TTL cache-aside helper for the company report endpoint. Unlike
/// ReferenceDataService's static emission factors, report data changes
/// continuously as new events arrive — so this uses a short TTL plus explicit
/// invalidation from the event consumers instead of a long-lived cache. Redis is
/// still a best-effort accelerator: any failure is caught and treated as a miss.
/// </summary>
public static class ReportCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(45);

    public static string KeyFor(int companyId, int year, int month) => $"report:{companyId}:{year}:{month}";

    public static async Task<byte[]?> TryGetAsync(IDistributedCache cache, string key, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis cache read failed for key {Key}; falling back to the database", key);
            return null;
        }
    }

    public static async Task TrySetAsync(IDistributedCache cache, string key, byte[] value, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis cache write failed for key {Key}; continuing without caching this result", key);
        }
    }

    public static async Task InvalidateAsync(IDistributedCache cache, int companyId, int year, int month, CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveAsync(KeyFor(companyId, year, month), cancellationToken);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or TimeoutException)
        {
            // Best-effort invalidation: if Redis is unreachable there's nothing stale to
            // worry about (nothing could have been cached either), so just move on.
        }
    }
}
