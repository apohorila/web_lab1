using Microsoft.Extensions.Caching.Distributed;

namespace server.Caching;

/// <summary>
/// Кеш поверх IDistributedCache. У робочому режимі це Redis
/// (AddStackExchangeRedisCache), тому кеш спільний для всіх екземплярів API
/// і переживає перезапуск сервера.
///
/// IDistributedCache не вміє видаляти за префіксом, тому інвалідація працює через "версію регіону":
/// ключ запису = region:версія:key. InvalidateAsync записує нову випадкову версію,
/// і всі старі записи стають недосяжними та самі зникають по TTL.
/// </summary>
public class DistributedCacheService : ICacheService
{
    private static readonly DistributedCacheEntryOptions VersionOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
    };

    private readonly IDistributedCache _cache;

    public DistributedCacheService(IDistributedCache cache, string providerName = "Redis")
    {
        _cache = cache;
        ProviderName = providerName;
    }

    public string ProviderName { get; }

    public async Task<string?> GetAsync(string region, string key)
    {
        var version = await GetVersionAsync(region);
        return await _cache.GetStringAsync(FullKey(region, version, key));
    }

    public async Task SetAsync(string region, string key, string value, TimeSpan ttl)
    {
        var version = await GetVersionAsync(region);
        await _cache.SetStringAsync(
            FullKey(region, version, key),
            value,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }
        );
    }

    public Task InvalidateAsync(string region) =>
        _cache.SetStringAsync(VersionKey(region), NewVersion(), VersionOptions);

    private async Task<string> GetVersionAsync(string region)
    {
        var version = await _cache.GetStringAsync(VersionKey(region));
        if (version == null)
        {
            version = NewVersion();
            await _cache.SetStringAsync(VersionKey(region), version, VersionOptions);
        }
        return version;
    }

    private static string NewVersion() => Guid.NewGuid().ToString("N");

    private static string VersionKey(string region) => $"ver:{region}";

    private static string FullKey(string region, string version, string key) =>
        $"{region}:{version}:{key}";
}
