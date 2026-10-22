using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using server.Caching;
using Xunit;

namespace server.Tests;

public class CachingTests
{
   
    private static ICacheService[] Providers() =>
        new ICacheService[]
        {
            new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())),
            new DistributedCacheService(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
                "Redis"
            ),
        };

    [Fact]
    public void ProviderNames_AreMemoryAndRedis()
    {
        var names = Providers().Select(p => p.ProviderName).ToArray();

        Assert.Equal(new[] { "Memory", "Redis" }, names);
    }

    [Fact]
    public async Task Set_ThenGet_ReturnsStoredValue()
    {
        foreach (var cache in Providers())
        {
            await cache.SetAsync("products", "/api/products", "[1,2,3]", TimeSpan.FromMinutes(1));

            Assert.Equal("[1,2,3]", await cache.GetAsync("products", "/api/products"));
        }
    }

    [Fact]
    public async Task Get_UnknownKey_ReturnsNull()
    {
        foreach (var cache in Providers())
        {
            Assert.Null(await cache.GetAsync("products", "/api/never-stored"));
        }
    }

    [Fact]
    public async Task DifferentKeys_AreStoredSeparately()
    {
        foreach (var cache in Providers())
        {
            await cache.SetAsync("products", "/api/products/1", "one", TimeSpan.FromMinutes(1));
            await cache.SetAsync("products", "/api/products/2", "two", TimeSpan.FromMinutes(1));

            Assert.Equal("one", await cache.GetAsync("products", "/api/products/1"));
            Assert.Equal("two", await cache.GetAsync("products", "/api/products/2"));
        }
    }

    [Fact]
    public async Task Invalidate_RemovesOnlyThatRegion()
    {
        foreach (var cache in Providers())
        {
            await cache.SetAsync("products", "k", "p", TimeSpan.FromMinutes(1));
            await cache.SetAsync("categories", "k", "c", TimeSpan.FromMinutes(1));

            await cache.InvalidateAsync("products");

            Assert.Null(await cache.GetAsync("products", "k"));
            Assert.Equal("c", await cache.GetAsync("categories", "k"));
        }
    }

    [Fact]
    public async Task Region_CanBeFilledAgain_AfterInvalidate()
    {
        foreach (var cache in Providers())
        {
            await cache.SetAsync("products", "k", "old", TimeSpan.FromMinutes(1));
            await cache.InvalidateAsync("products");

            await cache.SetAsync("products", "k", "new", TimeSpan.FromMinutes(1));

            Assert.Equal("new", await cache.GetAsync("products", "k"));
        }
    }

    [Fact]
    public async Task Entry_Expires_AfterTtl()
    {
        foreach (var cache in Providers())
        {
            await cache.SetAsync("products", "k", "short-lived", TimeSpan.FromMilliseconds(100));
            Assert.Equal("short-lived", await cache.GetAsync("products", "k"));

            await Task.Delay(500);

            Assert.Null(await cache.GetAsync("products", "k"));
        }
    }



    private sealed class Calls
    {
        public int Count;
    }

    private sealed class ThrowingCache : ICacheService
    {
        public string ProviderName => "Broken";

        public Task<string?> GetAsync(string region, string key) =>
            throw new InvalidOperationException("Redis is down");

        public Task SetAsync(string region, string key, string value, TimeSpan ttl) =>
            throw new InvalidOperationException("Redis is down");

        public Task InvalidateAsync(string region) =>
            throw new InvalidOperationException("Redis is down");
    }

    private sealed record FilterRun(HttpContext Http, IActionResult? Result);

    private static async Task<FilterRun> RunAsync(
        IAsyncActionFilter filter,
        ICacheService? cache,
        string method,
        string path,
        Func<IActionResult> action,
        Calls calls,
        string query = ""
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(_ => { });
        if (cache != null)
        {
            services.AddSingleton<ICacheService>(cache);
        }

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Method = method;
        http.Request.Path = path;
        http.Request.QueryString = new QueryString(query);

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object()
        );

        ActionExecutedContext? executed = null;
        ActionExecutionDelegate next = () =>
        {
            calls.Count++;
            executed = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object())
            {
                Result = action(),
            };
            return Task.FromResult(executed);
        };

        await filter.OnActionExecutionAsync(executing, next);

        return new FilterRun(http, executing.Result ?? executed?.Result);
    }

    private static string Body(FilterRun run) => Assert.IsType<ContentResult>(run.Result).Content!;

    private static string Header(FilterRun run, string name) => run.Http.Response.Headers[name].ToString();

    private static IActionResult Products() => new OkObjectResult(new[] { new { id = 1, name = "Bag" } });

    [Fact]
    public async Task CacheResponse_FirstCallIsMiss_SecondCallIsHit()
    {
        foreach (var cache in Providers())
        {
            var filter = new CacheResponseAttribute("products", 60);
            var calls = new Calls();

            var first = await RunAsync(filter, cache, "GET", "/api/products", Products, calls);
            var second = await RunAsync(filter, cache, "GET", "/api/products", Products, calls);

            Assert.Equal("MISS", Header(first, "X-Cache"));
            Assert.Equal("HIT", Header(second, "X-Cache"));
            Assert.Equal(cache.ProviderName, Header(second, "X-Cache-Provider"));
            Assert.Equal(1, calls.Count); 
            Assert.Equal("[{\"id\":1,\"name\":\"Bag\"}]", Body(first));
            Assert.Equal(Body(first), Body(second));
        }
    }

    [Fact]
    public async Task CacheResponse_DifferentQueryString_UsesSeparateEntries()
    {
        var cache = Providers()[0];
        var filter = new CacheResponseAttribute("stats", 60);
        var calls = new Calls();

        await RunAsync(filter, cache, "GET", "/api/stats/sales", Products, calls, "?days=7");
        var other = await RunAsync(filter, cache, "GET", "/api/stats/sales", Products, calls, "?days=30");
        var sameAgain = await RunAsync(filter, cache, "GET", "/api/stats/sales", Products, calls, "?days=7");

        Assert.Equal("MISS", Header(other, "X-Cache"));
        Assert.Equal("HIT", Header(sameAgain, "X-Cache"));
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task CacheResponse_DoesNotCache_NotFoundResults()
    {
        var cache = Providers()[0];
        var filter = new CacheResponseAttribute("products", 60);
        var calls = new Calls();

        await RunAsync(filter, cache, "GET", "/api/products/999", () => new NotFoundResult(), calls);
        var second = await RunAsync(filter, cache, "GET", "/api/products/999", () => new NotFoundResult(), calls);

        Assert.Equal(2, calls.Count);
        Assert.IsType<NotFoundResult>(second.Result);
    }

    [Fact]
    public async Task CacheResponse_DoesNotCache_NonGetRequests()
    {
        var cache = Providers()[0];
        var filter = new CacheResponseAttribute("products", 60);
        var calls = new Calls();

        await RunAsync(filter, cache, "POST", "/api/products", Products, calls);
        await RunAsync(filter, cache, "POST", "/api/products", Products, calls);

        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task CacheResponse_WithoutCacheService_JustRunsTheAction()
    {
        var filter = new CacheResponseAttribute("products", 60); // Cache:Provider = None
        var calls = new Calls();

        var run = await RunAsync(filter, cache: null, "GET", "/api/products", Products, calls);

        Assert.Equal(1, calls.Count);
        Assert.IsType<OkObjectResult>(run.Result);
    }

    [Fact]
    public async Task CacheResponse_WhenCacheIsDown_StillReturnsTheResponse()
    {
        
        var filter = new CacheResponseAttribute("products", 60);
        var calls = new Calls();

        var run = await RunAsync(filter, new ThrowingCache(), "GET", "/api/products", Products, calls);

        Assert.Equal(1, calls.Count);
        Assert.Equal("[{\"id\":1,\"name\":\"Bag\"}]", Body(run));
    }

    [Fact]
    public async Task InvalidateCache_AfterSuccessfulWrite_NextGetIsMiss()
    {
        foreach (var cache in Providers())
        {
            var read = new CacheResponseAttribute("products", 60);
            var write = new InvalidateCacheAttribute("products");
            var calls = new Calls();
            var writeCalls = new Calls();

            await RunAsync(read, cache, "GET", "/api/products", Products, calls);
            var hit = await RunAsync(read, cache, "GET", "/api/products", Products, calls);
            await RunAsync(write, cache, "POST", "/api/products", () => new CreatedResult("/x", new { }), writeCalls);
            var afterWrite = await RunAsync(read, cache, "GET", "/api/products", Products, calls);

            Assert.Equal("HIT", Header(hit, "X-Cache"));
            Assert.Equal("MISS", Header(afterWrite, "X-Cache")); // кеш очищено — дані свіжі
            Assert.Equal(2, calls.Count);
        }
    }

    [Fact]
    public async Task InvalidateCache_WhenWriteFails_KeepsCache()
    {
        var cache = Providers()[0];
        var read = new CacheResponseAttribute("products", 60);
        var write = new InvalidateCacheAttribute("products");
        var calls = new Calls();

        await RunAsync(read, cache, "GET", "/api/products", Products, calls);
        await RunAsync(write, cache, "POST", "/api/products", () => new BadRequestObjectResult("bad"), new Calls());
        var after = await RunAsync(read, cache, "GET", "/api/products", Products, calls);

        Assert.Equal("HIT", Header(after, "X-Cache"));
        Assert.Equal(1, calls.Count);
    }

    [Fact]
    public async Task InvalidateCache_ClearsOnlyListedRegions()
    {
        var cache = Providers()[0];
        var products = new CacheResponseAttribute("products", 60);
        var showrooms = new CacheResponseAttribute("showrooms", 60);
        var write = new InvalidateCacheAttribute("showrooms");
        var calls = new Calls();

        await RunAsync(products, cache, "GET", "/api/products", Products, calls);
        await RunAsync(showrooms, cache, "GET", "/api/showrooms", Products, calls);
        await RunAsync(write, cache, "PUT", "/api/showrooms/1", () => new NoContentResult(), new Calls());

        var productsAfter = await RunAsync(products, cache, "GET", "/api/products", Products, calls);
        var showroomsAfter = await RunAsync(showrooms, cache, "GET", "/api/showrooms", Products, calls);

        Assert.Equal("HIT", Header(productsAfter, "X-Cache"));
        Assert.Equal("MISS", Header(showroomsAfter, "X-Cache"));
    }

    [Fact]
    public async Task InvalidateCache_WhenCacheIsDown_DoesNotBreakTheWrite()
    {
        var write = new InvalidateCacheAttribute("products");

        var run = await RunAsync(write, new ThrowingCache(), "DELETE", "/api/products/1", () => new NoContentResult(), new Calls());

        Assert.IsType<NoContentResult>(run.Result);
    }
}
