namespace server.Caching;

/// <summary>
/// Спільний інтерфейс для двох реалізацій кешу: пам'ять процесу (IMemoryCache)
/// та Redis (IDistributedCache). Значення зберігаються як рядки (готовий JSON відповіді).
/// Кеш поділений на "регіони" (products, categories, ...): інвалідація очищає лише один регіон.
/// </summary>
public interface ICacheService
{
    /// <summary>"Memory" або "Redis" — повертається клієнту в заголовку X-Cache-Provider.</summary>
    string ProviderName { get; }

    Task<string?> GetAsync(string region, string key);

    Task SetAsync(string region, string key, string value, TimeSpan ttl);

    /// <summary>Миттєво робить недійсними всі записи регіону.</summary>
    Task InvalidateAsync(string region);
}
