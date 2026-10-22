using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using Xunit;

namespace server.Tests;

public class StatsControllerTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetCatalogStats_TotalsMatchTheDatabase()
    {
        var db = GetTestDb();
        var controller = new StatsController(db);

        var result = await controller.GetCatalogStats();

        Assert.NotNull(result.Value);
        Assert.Equal(await db.Products.CountAsync(), result.Value!.TotalProducts);
        Assert.Equal(await db.Categories.CountAsync(), result.Value.TotalCategories);
        Assert.Equal(await db.Showrooms.CountAsync(), result.Value.TotalShowrooms);
        Assert.Equal(result.Value.TotalCategories, result.Value.ProductsPerCategory.Count);
        Assert.Equal(result.Value.TotalShowrooms, result.Value.Showrooms.Count);
    }

    [Fact]
    public async Task GetCatalogStats_ProductCountsAddUpToTheTotal()
    {
        var db = GetTestDb();
        var controller = new StatsController(db);

        var result = await controller.GetCatalogStats();

        Assert.Equal(result.Value!.TotalProducts, result.Value.ProductsPerCategory.Sum(c => c.Count));
        Assert.Equal(result.Value.TotalProducts, result.Value.Showrooms.Sum(s => s.ProductCount));
    }

    [Theory]
    [InlineData(7, 7)]
    [InlineData(30, 30)]
    [InlineData(0, 1)] 
    [InlineData(100000, 365)]
    public async Task GetSalesStats_ReturnsOnePointPerDay(int requestedDays, int expectedPoints)
    {
        var db = GetTestDb();
        var controller = new StatsController(db);

        var result = await controller.GetSalesStats(requestedDays);

        Assert.Equal(expectedPoints, result.Value!.Days);
        Assert.Equal(expectedPoints, result.Value.Daily.Count);
    }

    [Fact]
    public async Task GetSalesStats_RevenueEqualsSumOfDailyRevenue()
    {
        var db = GetTestDb();
        var controller = new StatsController(db);

        var result = await controller.GetSalesStats(30);

        Assert.Equal(result.Value!.Revenue, result.Value.Daily.Sum(d => d.Revenue));
    }

    [Fact]
    public void SalesStats_RequiresAdminRole()
    {
        var attribute = typeof(StatsController)
            .GetMethod("GetSalesStats")!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Admin", attribute!.Roles);
    }

    [Fact]
    public void CatalogStats_IsPublic()
    {
        var method = typeof(StatsController).GetMethod("GetCatalogStats")!;

        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(StatsController).GetCustomAttribute<AuthorizeAttribute>());
    }
}
