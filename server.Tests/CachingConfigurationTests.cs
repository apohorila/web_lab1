using System.Reflection;
using server.Caching;
using server.Controllers;
using Xunit;

namespace server.Tests;

/// <summary>
/// Перевіряють, що кешування правильно "підключено" до реальних контролерів:
/// публічні GET кешуються, зміни очищають потрібні регіони, а персональні дані не кешуються ніколи.
/// </summary>
public class CachingConfigurationTests
{
    [Theory]
    [InlineData(typeof(ProductsController), "GetProducts", "products")]
    [InlineData(typeof(ProductsController), "GetProduct", "products")]
    [InlineData(typeof(CategoriesController), "GetCategories", "categories")]
    [InlineData(typeof(CategoriesController), "GetCategory", "categories")]
    [InlineData(typeof(ShowroomsController), "GetShowrooms", "showrooms")]
    [InlineData(typeof(ShowroomsController), "GetShowroom", "showrooms")]
    [InlineData(typeof(StatsController), "GetCatalogStats", "stats")]
    [InlineData(typeof(StatsController), "GetSalesStats", "stats")]
    public void ReadActions_AreCachedInTheirRegion(Type controller, string action, string region)
    {
        var attribute = controller.GetMethod(action)!.GetCustomAttribute<CacheResponseAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(region, attribute!.Region);
        Assert.True(attribute.Seconds > 0);
    }

    [Theory]
    [InlineData(typeof(ProductsController), "CreateProduct", "products")]
    [InlineData(typeof(ProductsController), "UpdateProduct", "products")]
    [InlineData(typeof(ProductsController), "DeleteProduct", "products")]
    [InlineData(typeof(CategoriesController), "CreateCategory", "categories")]
    [InlineData(typeof(CategoriesController), "UpdateCategory", "categories")]
    [InlineData(typeof(CategoriesController), "DeleteCategory", "categories")]
    [InlineData(typeof(ShowroomsController), "CreateShowroom", "showrooms")]
    [InlineData(typeof(ShowroomsController), "UpdateShowroom", "showrooms")]
    [InlineData(typeof(ShowroomsController), "DeleteShowroom", "showrooms")]
    public void WriteActions_InvalidateTheirRegionProductsAndStats(Type controller, string action, string region)
    {
        var attribute = controller.GetMethod(action)!.GetCustomAttribute<InvalidateCacheAttribute>();

        Assert.NotNull(attribute);
        Assert.Contains(region, attribute!.Regions);
        // відповіді товарів містять категорію та шоурум, а статистика залежить від усіх трьох
        Assert.Contains("products", attribute.Regions);
        Assert.Contains("stats", attribute.Regions);
    }

    [Theory]
    [InlineData("CreateOrder")]
    [InlineData("CancelOrder")]
    public void OrderChanges_InvalidateStats(string action)
    {
        var attribute = typeof(OrdersController).GetMethod(action)!.GetCustomAttribute<InvalidateCacheAttribute>();

        Assert.NotNull(attribute);
        Assert.Contains("stats", attribute!.Regions);
    }

    [Theory]
    [InlineData(typeof(OrdersController), "GetOrders")]
    [InlineData(typeof(OrdersController), "GetOrder")]
    [InlineData(typeof(OrdersController), "GetMyOrders")]
    [InlineData(typeof(CartController), "GetCart")]
    public void PersonalData_IsNeverCached(Type controller, string action)
    {
        // кеш не залежить від користувача, тож персональні відповіді кешувати не можна
        Assert.Null(controller.GetMethod(action)!.GetCustomAttribute<CacheResponseAttribute>());
    }
}
