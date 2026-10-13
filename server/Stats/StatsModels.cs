namespace server.Stats;

// ───────── Вхідні рядки (те, що контролер читає з БД) ─────────
public record ProductRow(int Id, decimal Price, int CategoryId, int ShowroomId);

public record CategoryRow(int Id, string Name);

public record ShowroomRow(int Id, string Name, string Address, double Latitude, double Longitude);

public record OrderRow(DateTime CreatedAt, decimal TotalAmount, string Status);

public record OrderItemRow(int ProductId, int Quantity, decimal UnitPrice);

// ───────── Відповіді API ─────────
public record NamedCount(string Name, int Count);

public record ShowroomStats(
    int Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int ProductCount,
    List<NamedCount> ByCategory
);

public record CatalogStatsDto(
    int TotalProducts,
    int TotalCategories,
    int TotalShowrooms,
    decimal AveragePrice,
    decimal MinPrice,
    decimal MaxPrice,
    List<NamedCount> ProductsPerCategory,
    List<ShowroomStats> Showrooms
);

public record DailyPoint(string Date, int Orders, decimal Revenue);

public record TopProduct(int ProductId, string Name, int Quantity, decimal Revenue);

public record SalesStatsDto(
    int Days,
    int TotalOrders,
    int CancelledOrders,
    decimal Revenue,
    decimal AverageOrderValue,
    List<DailyPoint> Daily,
    List<TopProduct> TopProducts,
    List<NamedCount> OrdersByStatus
);
