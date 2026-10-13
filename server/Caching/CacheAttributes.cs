using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace server.Caching;

/// <summary>
/// Кешує успішну (200) JSON-відповідь GET-запиту. Ключ = шлях + query-рядок.
/// Заголовки відповіді: X-Cache: HIT | MISS та X-Cache-Provider: Memory | Redis.
/// Фільтр виконується ПІСЛЯ авторизації, тому кеш не обходить [Authorize].
/// Якщо кеш недоступний (наприклад, Redis вимкнено), запит просто виконується без кешу.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CacheResponseAttribute : Attribute, IAsyncActionFilter
{
    private const string JsonContentType = "application/json; charset=utf-8";

    public CacheResponseAttribute(string region, int seconds = 60)
    {
        Region = region;
        Seconds = seconds;
    }

    public string Region { get; }

    public int Seconds { get; }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var http = context.HttpContext;
        var cache = http.RequestServices.GetService<ICacheService>();

        if (cache == null || !HttpMethods.IsGet(http.Request.Method))
        {
            await next();
            return;
        }

        var logger = http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("ResponseCache");
        var key = $"{http.Request.Path}{http.Request.QueryString}";

        string? cached = null;
        try
        {
            cached = await cache.GetAsync(Region, key);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Кеш недоступний (читання), виконуємо запит без кешу");
        }

        if (cached != null)
        {
            SetHeaders(http, "HIT", cache);
            context.Result = new ContentResult
            {
                Content = cached,
                ContentType = JsonContentType,
                StatusCode = StatusCodes.Status200OK,
            };
            return;
        }

        var executed = await next();
        SetHeaders(http, "MISS", cache);

        if (
            executed.Exception == null
            && executed.Result is ObjectResult { Value: not null } objectResult
            && (objectResult.StatusCode ?? StatusCodes.Status200OK) == StatusCodes.Status200OK
        )
        {
            // Серіалізуємо тими самими налаштуваннями, що й MVC (camelCase, IgnoreCycles),
            // тож HIT і MISS повертають однаковий JSON.
            var jsonOptions = http
                .RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
                .Value.JsonSerializerOptions;
            var json = JsonSerializer.Serialize(
                objectResult.Value,
                objectResult.Value.GetType(),
                jsonOptions
            );

            executed.Result = new ContentResult
            {
                Content = json,
                ContentType = JsonContentType,
                StatusCode = StatusCodes.Status200OK,
            };

            try
            {
                await cache.SetAsync(Region, key, json, TimeSpan.FromSeconds(Seconds));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Кеш недоступний (запис), відповідь віддано без кешування");
            }
        }
    }

    private static void SetHeaders(HttpContext http, string state, ICacheService cache)
    {
        http.Response.Headers["X-Cache"] = state;
        http.Response.Headers["X-Cache-Provider"] = cache.ProviderName;
    }
}

/// <summary>
/// Після успішного виконання дії (POST/PUT/DELETE) очищає вказані регіони кешу,
/// щоб наступний GET отримав свіжі дані. При помилці (>= 400 або виняток) кеш не чіпає.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class InvalidateCacheAttribute : Attribute, IAsyncActionFilter
{
    public InvalidateCacheAttribute(params string[] regions)
    {
        Regions = regions;
    }

    public string[] Regions { get; }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var executed = await next();

        if (executed.Exception != null && !executed.ExceptionHandled)
        {
            return;
        }

        var status = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;
        if (status >= StatusCodes.Status400BadRequest)
        {
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetService<ICacheService>();
        if (cache == null)
        {
            return;
        }

        foreach (var region in Regions)
        {
            try
            {
                await cache.InvalidateAsync(region);
            }
            catch (Exception ex)
            {
                services
                    .GetService<ILoggerFactory>()
                    ?.CreateLogger("ResponseCache")
                    .LogWarning(ex, "Не вдалося очистити регіон кешу {Region}", region);
            }
        }
    }
}
