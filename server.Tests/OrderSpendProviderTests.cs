using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using server.Data;
using server.Features;
using server.Models;
using Xunit;

namespace server.Tests;


public class OrderSpendProviderTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FixedPlanOptions : IOptionsMonitor<PlanOptions>
    {
        public PlanOptions CurrentValue { get; } = new() { PremiumSpendThreshold = 20000m };

        public PlanOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<PlanOptions, string?> listener) => null;
    }

    private static User NewUser(string prefix) =>
        new()
        {
            Email = $"{prefix}_{Guid.NewGuid():N}@example.com",
            FullName = prefix,
            PasswordHash = "x",
        };

    private static Order NewOrder(int userId, decimal total, string status) =>
        new()
        {
            UserId = userId,
            CustomerName = "Тест",
            CustomerPhone = "+380000000000",
            DeliveryCity = "Київ",
            DeliveryAddress = "Відділення №1",
            TotalAmount = total,
            Status = status,
            CreatedAt = DateTime.UtcNow,
        };

    private static async Task Cleanup(AppDbContext db, params User[] users)
    {
        var ids = users.Select(u => u.Id).ToList();
        db.Orders.RemoveRange(await db.Orders.Where(o => o.UserId != null && ids.Contains(o.UserId.Value)).ToListAsync());
        await db.SaveChangesAsync();
        db.Users.RemoveRange(await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync());
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Spend_SumsOnlyActiveOrdersOfThatUser()
    {
        var db = GetTestDb();
        var buyer = NewUser("buyer");
        var other = NewUser("other");
        db.Users.AddRange(buyer, other);
        await db.SaveChangesAsync();
        try
        {
            db.Orders.AddRange(
                NewOrder(buyer.Id, 12000m, "Pending"),
                NewOrder(buyer.Id, 9000m, "Pending"),
                NewOrder(buyer.Id, 5000m, "Cancelled"), 
                NewOrder(other.Id, 99999m, "Pending")
            );
            await db.SaveChangesAsync();

            var spent = await new OrderSpendProvider(db).GetLifetimeSpendAsync(buyer.Id);

            Assert.Equal(21000m, spent);
        }
        finally
        {
            await Cleanup(db, buyer, other);
        }
    }

    [Fact]
    public async Task ClientWithoutOrders_HasZeroSpend()
    {
        var db = GetTestDb();
        var user = NewUser("empty");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        try
        {
            Assert.Equal(0m, await new OrderSpendProvider(db).GetLifetimeSpendAsync(user.Id));
        }
        finally
        {
            await Cleanup(db, user);
        }
    }

    [Fact]
    public async Task Plan_BecomesPremiumAt20000_AndFallsBackWhenAnOrderIsCancelled()
    {
        var db = GetTestDb();
        var user = NewUser("plan");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        try
        {
            var plans = new PlanService(new FixedPlanOptions(), new OrderSpendProvider(db));
            var principal = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()) },
                    "Test"
                )
            );

            var small = NewOrder(user.Id, 15000m, "Pending");
            db.Orders.Add(small);
            await db.SaveChangesAsync();
            Assert.Equal("Free", (await plans.GetPlanAsync(principal)).Plan);

            var big = NewOrder(user.Id, 5000m, "Pending");
            db.Orders.Add(big);
            await db.SaveChangesAsync();
            Assert.Equal("Premium", (await plans.GetPlanAsync(principal)).Plan); // рівно 20 000

            big.Status = "Cancelled";
            await db.SaveChangesAsync();
            var info = await plans.GetPlanAsync(principal);
            Assert.Equal("Free", info.Plan);
            Assert.Equal(15000m, info.Spent);
        }
        finally
        {
            await Cleanup(db, user);
        }
    }
}
