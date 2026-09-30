using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
using Xunit;

namespace server.Tests;

public class CartControllerTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    private void SetUserContext(CartController controller, int userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    [Fact]
    public async Task AddItemToCart_And_RemoveItem()
    {
        var db = GetTestDb();
        var controller = new CartController(db);

        var category = new Category { Name = "Шопери" };
        var showroom = new Showroom { Name = "Центральний Шоурум", Address = "вул. Франка, 10" };
        db.Categories.Add(category);
        db.Showrooms.Add(showroom);
        await db.SaveChangesAsync();

        var user = new User { Email = "cart_tester@example.com", PasswordHash = "hash" };
        var product = new Product
        {
            Name = "Шопер Bagelle",
            Price = 2100,
            CategoryId = category.Id,
            ShowroomId = showroom.Id,
        };

        db.Users.Add(user);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        SetUserContext(controller, user.Id);

        await controller.AddItemToCart(
            new CartController.AddToCartDto { ProductId = product.Id, Quantity = 1 }
        );

        var cart = await db
            .Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == user.Id);

        Assert.NotNull(cart);
        Assert.Single(cart.Items);
        Assert.Equal(product.Id, cart.Items.First().ProductId);

        var cartItemId = cart.Items.First().Id;

        await controller.RemoveItem(cartItemId);

        var deletedItem = await db.CartItems.FindAsync(cartItemId);
        Assert.Null(deletedItem);

        db.Carts.Remove(cart);
        db.Users.Remove(user);
        db.Products.Remove(product);
        db.Categories.Remove(category);
        db.Showrooms.Remove(showroom);
        await db.SaveChangesAsync();
    }
}
