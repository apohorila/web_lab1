using server.Stats;
using Xunit;

namespace server.Tests;

public class StatsCalculatorTests
{
    private static readonly CategoryRow[] Categories =
    {
        new(1, "Сумки"),
        new(2, "Рюкзаки"),
        new(3, "Гаманці"),
    };

    private static readonly ShowroomRow[] Showrooms =
    {
        new(10, "Київ", "м. Київ, вул. А, 1", 50.45, 30.52),
        new(20, "Львів", "м. Львів, вул. Б, 2", 49.84, 24.03),
    };

    [Fact]
    public void Catalog_CountsProductsPerCategoryAndShowroom()
    {
        var products = new ProductRow[]
        {
            new(1, 1000m, 1, 10),
            new(2, 2000m, 1, 10),
            new(3, 3000m, 2, 10),
            new(4, 4000m, 2, 20),
        };

        var stats = StatsCalculator.Catalog(products, Categories, Showrooms);

        Assert.Equal(4, stats.TotalProducts);
        Assert.Equal(3, stats.TotalCategories);
        Assert.Equal(2, stats.TotalShowrooms);
        Assert.Equal(2, stats.ProductsPerCategory.Single(c => c.Name == "Рюкзаки").Count);
        Assert.Equal(2, stats.ProductsPerCategory.Single(c => c.Name == "Сумки").Count);
        Assert.Equal("Гаманці", stats.ProductsPerCategory.Last().Name); // найменше товарів — в кінці
        Assert.Equal(0, stats.ProductsPerCategory.Single(c => c.Name == "Гаманці").Count);

        var kyiv = stats.Showrooms.Single(s => s.Id == 10);
        Assert.Equal(3, kyiv.ProductCount);
        Assert.Equal(2, kyiv.ByCategory.Single(c => c.Name == "Сумки").Count);
        Assert.Equal(1, kyiv.ByCategory.Single(c => c.Name == "Рюкзаки").Count);
        Assert.Equal(1, stats.Showrooms.Single(s => s.Id == 20).ProductCount);
    }

    [Fact]
    public void Catalog_ComputesPriceStatistics()
    {
        var products = new ProductRow[] { new(1, 1000m, 1, 10), new(2, 2000m, 1, 10), new(3, 3000m, 1, 10) };

        var stats = StatsCalculator.Catalog(products, Categories, Showrooms);

        Assert.Equal(2000m, stats.AveragePrice);
        Assert.Equal(1000m, stats.MinPrice);
        Assert.Equal(3000m, stats.MaxPrice);
    }

    [Fact]
    public void Catalog_WithNoProducts_ReturnsZerosInsteadOfThrowing()
    {
        var stats = StatsCalculator.Catalog(Array.Empty<ProductRow>(), Categories, Showrooms);

        Assert.Equal(0, stats.TotalProducts);
        Assert.Equal(0m, stats.AveragePrice);
        Assert.Equal(0m, stats.MinPrice);
        Assert.Equal(0m, stats.MaxPrice);
        Assert.Equal(2, stats.Showrooms.Count);
    }

    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Sales_ExcludesCancelledOrdersFromRevenue()
    {
        var orders = new OrderRow[]
        {
            new(Start.AddHours(10), 1000m, "Pending"),
            new(Start.AddHours(12), 500m, "Pending"),
            new(Start.AddHours(13), 9999m, "Cancelled"),
        };

        var stats = StatsCalculator.Sales(orders, Array.Empty<OrderItemRow>(), new Dictionary<int, string>(), Start, 7);

        Assert.Equal(3, stats.TotalOrders);
        Assert.Equal(1, stats.CancelledOrders);
        Assert.Equal(1500m, stats.Revenue);
        Assert.Equal(750m, stats.AverageOrderValue);
    }

    [Fact]
    public void Sales_ReturnsOneZeroFilledPointPerDay()
    {
        var orders = new OrderRow[]
        {
            new(Start.AddDays(1).AddHours(9), 100m, "Pending"),
            new(Start.AddDays(1).AddHours(18), 200m, "Pending"),
            new(Start.AddDays(3), 50m, "Pending"),
        };

        var stats = StatsCalculator.Sales(orders, Array.Empty<OrderItemRow>(), new Dictionary<int, string>(), Start, 5);

        Assert.Equal(5, stats.Daily.Count);
        Assert.Equal("2026-10-01", stats.Daily[0].Date);
        Assert.Equal("2026-10-05", stats.Daily[4].Date);
        Assert.Equal(0, stats.Daily[0].Orders);
        Assert.Equal(2, stats.Daily[1].Orders);
        Assert.Equal(300m, stats.Daily[1].Revenue);
        Assert.Equal(1, stats.Daily[3].Orders);
        Assert.Equal(0m, stats.Daily[4].Revenue);
    }

    [Fact]
    public void Sales_TopProducts_AreSortedByQuantityAndLimitedToFive()
    {
        var items = new OrderItemRow[]
        {
            new(1, 2, 100m),
            new(1, 3, 100m), // товар 1: 5 шт
            new(2, 1, 900m), // товар 2: 1 шт
            new(3, 4, 50m), // товар 3: 4 шт
            new(4, 1, 10m),
            new(5, 1, 10m),
            new(6, 1, 10m),
        };
        var names = new Dictionary<int, string> { [1] = "Сумка", [3] = "Гаманець" };

        var stats = StatsCalculator.Sales(Array.Empty<OrderRow>(), items, names, Start, 7);

        Assert.Equal(5, stats.TopProducts.Count);
        Assert.Equal("Сумка", stats.TopProducts[0].Name);
        Assert.Equal(5, stats.TopProducts[0].Quantity);
        Assert.Equal(500m, stats.TopProducts[0].Revenue);
        Assert.Equal("Гаманець", stats.TopProducts[1].Name);
        Assert.Contains(stats.TopProducts, t => t.Name == "Товар #2"); // немає в словнику — запасна назва
    }

    [Fact]
    public void Sales_GroupsOrdersByStatus()
    {
        var orders = new OrderRow[]
        {
            new(Start, 1m, "Pending"),
            new(Start, 1m, "Pending"),
            new(Start, 1m, "Cancelled"),
        };

        var stats = StatsCalculator.Sales(orders, Array.Empty<OrderItemRow>(), new Dictionary<int, string>(), Start, 3);

        Assert.Equal(2, stats.OrdersByStatus.Single(s => s.Name == "Pending").Count);
        Assert.Equal(1, stats.OrdersByStatus.Single(s => s.Name == "Cancelled").Count);
    }

    [Fact]
    public void Sales_WithNoOrders_ReturnsEmptyButCompleteResult()
    {
        var stats = StatsCalculator.Sales(Array.Empty<OrderRow>(), Array.Empty<OrderItemRow>(), new Dictionary<int, string>(), Start, 30);

        Assert.Equal(0, stats.TotalOrders);
        Assert.Equal(0m, stats.AverageOrderValue);
        Assert.Equal(30, stats.Daily.Count);
        Assert.Empty(stats.TopProducts);
    }
}
