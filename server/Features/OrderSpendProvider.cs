using Microsoft.EntityFrameworkCore;
using server.Data;

namespace server.Features;


public class OrderSpendProvider : ISpendProvider
{
    private readonly AppDbContext _db;

    public OrderSpendProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetLifetimeSpendAsync(int userId) =>
        await _db
            .Orders.AsNoTracking()
            .Where(o => o.UserId == userId && o.Status != "Cancelled")
            .SumAsync(o => o.TotalAmount);
}
