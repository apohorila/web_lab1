namespace server.Stats;

/// <summary>
/// Чиста логіка агрегації статистики (без EF/БД), щоб її можна було тестувати окремо.
/// </summary>
public static class StatsCalculator
{
    public static CatalogStatsDto Catalog(
        IReadOnlyList<ProductRow> products,
        IReadOnlyList<CategoryRow> categories,
        IReadOnlyList<ShowroomRow> showrooms
    )
    {
        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
        string CategoryName(int id) =>
            categoryNames.TryGetValue(id, out var name) ? name : "Без категорії";

        var perCategory = categories
            .Select(c => new NamedCount(c.Name, products.Count(p => p.CategoryId == c.Id)))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .ToList();

        var showroomStats = showrooms
            .Select(s =>
            {
                var inShowroom = products.Where(p => p.ShowroomId == s.Id).ToList();
                var byCategory = inShowroom
                    .GroupBy(p => CategoryName(p.CategoryId))
                    .Select(g => new NamedCount(g.Key, g.Count()))
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Name)
                    .ToList();

                return new ShowroomStats(
                    s.Id,
                    s.Name,
                    s.Address,
                    s.Latitude,
                    s.Longitude,
                    inShowroom.Count,
                    byCategory
                );
            })
            .ToList();

        var hasProducts = products.Count > 0;

        return new CatalogStatsDto(
            products.Count,
            categories.Count,
            showrooms.Count,
            hasProducts ? Math.Round(products.Average(p => p.Price), 2) : 0m,
            hasProducts ? products.Min(p => p.Price) : 0m,
            hasProducts ? products.Max(p => p.Price) : 0m,
            perCategory,
            showroomStats
        );
    }

    /// <param name="orders">Усі замовлення за період (разом зі скасованими).</param>
    /// <param name="items">Позиції лише НЕскасованих замовлень.</param>
    /// <param name="startDate">Перший день періоду (UTC).</param>
    public static SalesStatsDto Sales(
        IReadOnlyList<OrderRow> orders,
        IReadOnlyList<OrderItemRow> items,
        IReadOnlyDictionary<int, string> productNames,
        DateTime startDate,
        int days
    )
    {
        var active = orders.Where(o => o.Status != "Cancelled").ToList();
        var byDate = active.GroupBy(o => o.CreatedAt.Date).ToDictionary(g => g.Key, g => g.ToList());

        var daily = Enumerable
            .Range(0, days)
            .Select(i =>
            {
                var date = startDate.Date.AddDays(i);
                byDate.TryGetValue(date, out var ofDay);
                ofDay ??= new List<OrderRow>();
                return new DailyPoint(
                    date.ToString("yyyy-MM-dd"),
                    ofDay.Count,
                    ofDay.Sum(o => o.TotalAmount)
                );
            })
            .ToList();

        var revenue = active.Sum(o => o.TotalAmount);

        var topProducts = items
            .GroupBy(i => i.ProductId)
            .Select(g =>
                new TopProduct(
                    g.Key,
                    productNames.TryGetValue(g.Key, out var name) ? name : $"Товар #{g.Key}",
                    g.Sum(i => i.Quantity),
                    g.Sum(i => i.Quantity * i.UnitPrice)
                )
            )
            .OrderByDescending(t => t.Quantity)
            .ThenByDescending(t => t.Revenue)
            .Take(5)
            .ToList();

        var byStatus = orders
            .GroupBy(o => o.Status)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .ToList();

        return new SalesStatsDto(
            days,
            orders.Count,
            orders.Count - active.Count,
            revenue,
            active.Count > 0 ? Math.Round(revenue / active.Count, 2) : 0m,
            daily,
            topProducts,
            byStatus
        );
    }
}
