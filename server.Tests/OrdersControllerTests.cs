using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
using Xunit;

namespace server.Tests;

public class OrdersControllerTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateOrder_AsGuest_SavesOrderSuccessfully()
    {
        var db = GetTestDb();
        var controller = new OrdersController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

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

        var orderDto = new OrdersController.CreateOrderDto
        {
            CustomerName = "Гість Покупець",
            CustomerPhone = "+380671112233",
            CustomerEmail = "guest@example.com",
            DeliveryCity = "Київ",
            DeliveryAddress = "Відділення №1",
            Items = new List<OrdersController.CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 2 },
            },
        };

        var response = await controller.CreateOrder(orderDto);
        Assert.NotNull(response);

        var savedOrder = await db
            .Orders.Include(o => o.Items)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(o => o.CustomerEmail == "guest@example.com");

        Assert.NotNull(savedOrder);
        Assert.Null(savedOrder.UserId);
        Assert.Equal(6400, savedOrder.TotalAmount);
        Assert.Single(savedOrder.Items);

        db.Orders.Remove(savedOrder);
        db.Products.Remove(product);
        db.Categories.Remove(category);
        db.Showrooms.Remove(showroom);
        await db.SaveChangesAsync();
    }
}
