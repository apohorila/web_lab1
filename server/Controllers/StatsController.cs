using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Caching;
using server.Data;
using server.Stats;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StatsController(AppDbContext context)
    {
        _context = context;
    }

    // Публічна статистика каталогу (для міні-діаграм на мапі шоурумів)
    [HttpGet("catalog")]
    [CacheResponse("stats", 120)]
    public async Task<ActionResult<CatalogStatsDto>> GetCatalogStats()
    {
        var products = await _context
            .Products.AsNoTracking()
            .Select(p => new ProductRow(p.Id, p.Price, p.CategoryId, p.ShowroomId))
            .ToListAsync();

        var categories = await _context
            .Categories.AsNoTracking()
            .Select(c => new CategoryRow(c.Id, c.Name))
            .ToListAsync();

        var showrooms = await _context
            .Showrooms.AsNoTracking()
            .Select(s => new ShowroomRow(s.Id, s.Name, s.Address, s.Latitude, s.Longitude))
            .ToListAsync();

        return StatsCalculator.Catalog(products, categories, showrooms);
    }

    // Статистика продажів — лише для адміністратора.
    // [Authorize] спрацьовує раніше за фільтр кешу, тому кеш не віддасть дані без прав.
    [Authorize(Roles = "Admin")]
    [HttpGet("sales")]
    [CacheResponse("stats", 60)]
    public async Task<ActionResult<SalesStatsDto>> GetSalesStats([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var orders = await _context
            .Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= start)
            .Select(o => new OrderRow(o.CreatedAt, o.TotalAmount, o.Status))
            .ToListAsync();

        var items = await _context
            .OrderItems.AsNoTracking()
            .Where(i => i.Order!.CreatedAt >= start && i.Order.Status != "Cancelled")
            .Select(i => new OrderItemRow(i.ProductId, i.Quantity, i.UnitPrice))
            .ToListAsync();

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var names = await _context
            .Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return StatsCalculator.Sales(orders, items, names, start, days);
    }
}
