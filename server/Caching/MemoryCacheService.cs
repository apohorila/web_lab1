using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace server.Caching;

/// <summary>
/// Кеш у пам'яті процесу (IMemoryCache). Швидкий, але власний для кожного екземпляра API
/// і зникає при перезапуску. Інвалідація регіону — через CancellationToken: усі записи
/// регіону прив'язані до одного токена, його скасування одразу видаляє їх з кешу.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _regions = new();

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string ProviderName => "Memory";

    public Task<string?> GetAsync(string region, string key)
    {
        _cache.TryGetValue(FullKey(region, key), out string? value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string region, string key, string value, TimeSpan ttl)
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(ttl)
            .AddExpirationToken(new CancellationChangeToken(RegionSource(region).Token));

        _cache.Set(FullKey(region, key), value, options);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string region)
    {
        if (_regions.TryRemove(region, out var source))
        {
            source.Cancel(); // усі записи регіону виселяються з кешу
            source.Dispose();
        }
        return Task.CompletedTask;
    }

    private CancellationTokenSource RegionSource(string region) =>
        _regions.GetOrAdd(region, _ => new CancellationTokenSource());

    private static string FullKey(string region, string key) => $"{region}|{key}";
}
