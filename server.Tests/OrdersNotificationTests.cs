using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Integrations.Telegram;
using server.Models;
using Xunit;

namespace server.Tests;


public class OrdersNotificationTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class RecordingNotifier : IOrderNotifier
    {
        public List<OrderNotification> Created { get; } = new();

        public List<OrderNotification> Cancelled { get; } = new();

        public Task OrderCreatedAsync(OrderNotification order, CancellationToken cancellationToken = default)
        {
            Created.Add(order);
            return Task.CompletedTask;
        }

        public Task OrderCancelledAsync(OrderNotification order, CancellationToken cancellationToken = default)
        {
            Cancelled.Add(order);
            return Task.CompletedTask;
        }
    }

    private static OrdersController GuestController(AppDbContext db, IOrderNotifier notifier) =>
        new(db, notifier)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static OrdersController AdminController(AppDbContext db, IOrderNotifier notifier)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "Admin") },
            "Test"
        );
        return new OrdersController(db, notifier)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

    private static async Task<(Category category, Showroom showroom, Product product)> CreateProduct(AppDbContext db)
    {
        var category = new Category { Name = "Тестова Категорія" };
        var showroom = new Showroom { Name = "Київський Шоурум", Address = "вул. Хрещатик, 1" };
        db.Categories.Add(category);
        db.Showrooms.Add(showroom);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Name = "Сумка Bagelle Classic",
            Price = 3200,
            CategoryId = category.Id,
            ShowroomId = showroom.Id,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return (category, showroom, product);
    }

    private static OrdersController.CreateOrderDto Dto(Product product, int quantity) =>
        new()
        {
            CustomerName = "Гість Покупець",
            CustomerPhone = "+380671112233",
            CustomerEmail = "notify@example.com",
            DeliveryCity = "Київ",
            DeliveryAddress = "Відділення №1",
            Items = new List<OrdersController.CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = quantity },
            },
        };

    private static async Task Cleanup(AppDbContext db, int orderId, Category category, Showroom showroom, Product product)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order != null)
        {
            db.Orders.Remove(order);
        }
        db.Products.Remove(product);
        db.Categories.Remove(category);
        db.Showrooms.Remove(showroom);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateOrder_NotifiesWithTheServerCalculatedTotalAndLines()
    {
        var db = GetTestDb();
        var notifier = new RecordingNotifier();
        var (category, showroom, product) = await CreateProduct(db);
        var orderId = 0;
        try
        {
            var result = await GuestController(db, notifier).CreateOrder(Dto(product, 2));

            Assert.IsType<OkObjectResult>(result);
            var notification = Assert.Single(notifier.Created);
            orderId = notification.OrderId;
            Assert.True(notification.OrderId > 0);
            Assert.Equal("Гість Покупець", notification.CustomerName);
            Assert.Equal("+380671112233", notification.Phone);
            Assert.Equal("Київ", notification.City);
            Assert.Equal(6400m, notification.Total);
            Assert.True(notification.IsGuest);

            var line = Assert.Single(notification.Lines);
            Assert.Equal("Сумка Bagelle Classic", line.Name);
            Assert.Equal(2, line.Quantity);
            Assert.Equal(3200m, line.UnitPrice);
            Assert.Equal(0, notifier.Cancelled.Count);
        }
        finally
        {
            await Cleanup(db, orderId, category, showroom, product);
        }
    }

    [Fact]
    public async Task InvalidOrder_DoesNotNotify()
    {
        var db = GetTestDb();
        var notifier = new RecordingNotifier();
        var dto = new OrdersController.CreateOrderDto { CustomerName = "X", CustomerPhone = "1", DeliveryAddress = "a" };

        var result = await GuestController(db, notifier).CreateOrder(dto); // немає товарів

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, notifier.Created.Count);
    }

    [Fact]
    public async Task CancelOrder_NotifiesWithTheProductNames()
    {
        var db = GetTestDb();
        var notifier = new RecordingNotifier();
        var (category, showroom, product) = await CreateProduct(db);
        var orderId = 0;
        try
        {
            await GuestController(db, notifier).CreateOrder(Dto(product, 1));
            orderId = Assert.Single(notifier.Created).OrderId;

            var result = await AdminController(db, notifier).CancelOrder(orderId);

            Assert.IsType<OkObjectResult>(result);
            var cancelled = Assert.Single(notifier.Cancelled);
            Assert.Equal(orderId, cancelled.OrderId);
            Assert.Equal("Сумка Bagelle Classic", Assert.Single(cancelled.Lines).Name);
        }
        finally
        {
            await Cleanup(db, orderId, category, showroom, product);
        }
    }

    [Fact]
    public async Task CancellingAnAlreadyCancelledOrder_DoesNotNotifyAgain()
    {
        var db = GetTestDb();
        var notifier = new RecordingNotifier();
        var (category, showroom, product) = await CreateProduct(db);
        var orderId = 0;
        try
        {
            await GuestController(db, notifier).CreateOrder(Dto(product, 1));
            orderId = Assert.Single(notifier.Created).OrderId;
            var admin = AdminController(db, notifier);

            await admin.CancelOrder(orderId);
            var second = await admin.CancelOrder(orderId);

            Assert.IsType<BadRequestObjectResult>(second);
            Assert.Equal(1, notifier.Cancelled.Count);
        }
        finally
        {
            await Cleanup(db, orderId, category, showroom, product);
        }
    }
}
