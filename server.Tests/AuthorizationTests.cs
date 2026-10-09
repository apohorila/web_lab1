using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
using Xunit;

namespace server.Tests;

public class AuthorizationTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }



    [Theory]
    [InlineData(typeof(ProductsController), "CreateProduct")]
    [InlineData(typeof(ProductsController), "UpdateProduct")]
    [InlineData(typeof(ProductsController), "DeleteProduct")]
    [InlineData(typeof(CategoriesController), "CreateCategory")]
    [InlineData(typeof(CategoriesController), "UpdateCategory")]
    [InlineData(typeof(CategoriesController), "DeleteCategory")]
    [InlineData(typeof(ShowroomsController), "CreateShowroom")]
    [InlineData(typeof(ShowroomsController), "UpdateShowroom")]
    [InlineData(typeof(ShowroomsController), "DeleteShowroom")]
    public void ModifyingActions_RequireAdminRole(Type controller, string action)
    {
        var method = controller.GetMethod(action);
        Assert.NotNull(method);

        var attribute = method!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("Admin", attribute!.Roles);
    }

    [Theory]
    [InlineData(typeof(ProductsController), "GetProducts")]
    [InlineData(typeof(ProductsController), "GetProduct")]
    [InlineData(typeof(CategoriesController), "GetCategories")]
    [InlineData(typeof(CategoriesController), "GetCategory")]
    [InlineData(typeof(ShowroomsController), "GetShowrooms")]
    [InlineData(typeof(ShowroomsController), "GetShowroom")]
    [InlineData(typeof(OrdersController), "CreateOrder")] // гостьове оформлення замовлення
    [InlineData(typeof(AuthController), "Login")]
    [InlineData(typeof(AuthController), "Register")]
    public void PublicActions_AreAnonymous(Type controller, string action)
    {
        var method = controller.GetMethod(action);
        Assert.NotNull(method);

        Assert.Empty(method!.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Empty(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    [Fact]
    public void CartController_RequiresAuthentication()
    {
        Assert.NotEmpty(typeof(CartController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    [Theory]
    [InlineData("GetOrders")]
    [InlineData("GetOrder")]
    [InlineData("GetMyOrders")]
    [InlineData("CancelOrder")]
    public void OrderReadAndCancelActions_RequireAuthentication(string action)
    {
        var method = typeof(OrdersController).GetMethod(action);
        Assert.NotNull(method);

        Assert.NotEmpty(method!.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    

    private static OrdersController ControllerFor(AppDbContext db, int? userId, string role = "Customer")
    {
        var claims = new List<Claim>();
        if (userId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = userId == null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(claims, "TestAuth");

        return new OrdersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

    
    private async Task<(User owner, User stranger, Order order)> Arrange(AppDbContext db, string status = "Pending")
    {
        var owner = new User { Email = $"owner_{Guid.NewGuid():N}@example.com", FullName = "owner", PasswordHash = "x" };
        var stranger = new User { Email = $"stranger_{Guid.NewGuid():N}@example.com", FullName = "stranger", PasswordHash = "x" };
        db.Users.AddRange(owner, stranger);
        await db.SaveChangesAsync();

        var order = new Order
        {
            UserId = owner.Id,
            CustomerName = "Owner",
            CustomerPhone = "+380000000000",
            DeliveryCity = "Київ",
            DeliveryAddress = "Відділення №1",
            TotalAmount = 100,
            Status = status,
            CreatedAt = DateTime.UtcNow,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (owner, stranger, order);
    }

    private static async Task Cleanup(AppDbContext db, params User[] users)
    {
        var ids = users.Select(u => u.Id).ToList();
        db.Orders.RemoveRange(await db.Orders.Where(o => o.UserId != null && ids.Contains(o.UserId.Value)).ToListAsync());
        await db.SaveChangesAsync();
        db.Users.RemoveRange(await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync());
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetOrder_Owner_ReturnsOk()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, owner.Id).GetOrder(order.Id);

            Assert.IsType<OkObjectResult>(result);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task GetOrder_AnotherUser_ReturnsNotFound()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, stranger.Id).GetOrder(order.Id);

            Assert.IsType<NotFoundObjectResult>(result);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task GetOrder_Admin_CanViewAnyOrder()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, stranger.Id, role: "Admin").GetOrder(order.Id);

            Assert.IsType<OkObjectResult>(result);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task GetOrder_WithoutIdentity_ReturnsUnauthorized()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, userId: null).GetOrder(order.Id);

            Assert.IsType<UnauthorizedResult>(result);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task GetMyOrders_ReturnsOnlyOrdersOfCurrentUser()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var strangerResult = await ControllerFor(db, stranger.Id).GetMyOrders();
            var ownerResult = await ControllerFor(db, owner.Id).GetMyOrders();

            var strangerOrders = Assert.IsAssignableFrom<IEnumerable<Order>>(
                Assert.IsType<OkObjectResult>(strangerResult).Value
            );
            var ownerOrders = Assert.IsAssignableFrom<IEnumerable<Order>>(
                Assert.IsType<OkObjectResult>(ownerResult).Value
            );

            Assert.DoesNotContain(strangerOrders, o => o.Id == order.Id);
            Assert.Contains(ownerOrders, o => o.Id == order.Id);
            Assert.All(ownerOrders, o => Assert.Equal(owner.Id, o.UserId));
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task GetOrders_ForCustomer_DoesNotIncludeOtherUsersOrders()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, stranger.Id).GetOrders();

            var orders = Assert.IsAssignableFrom<IEnumerable<Order>>(
                Assert.IsType<OkObjectResult>(result).Value
            );
            Assert.DoesNotContain(orders, o => o.Id == order.Id);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task CancelOrder_Owner_SetsStatusCancelled()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, owner.Id).CancelOrder(order.Id);

            Assert.IsType<OkObjectResult>(result);
            using var fresh = GetTestDb();
            Assert.Equal("Cancelled", (await fresh.Orders.SingleAsync(o => o.Id == order.Id)).Status);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task CancelOrder_AnotherUser_ReturnsNotFoundAndKeepsStatus()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, stranger.Id).CancelOrder(order.Id);

            Assert.IsType<NotFoundObjectResult>(result);
            using var fresh = GetTestDb();
            Assert.Equal("Pending", (await fresh.Orders.SingleAsync(o => o.Id == order.Id)).Status);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task CancelOrder_AlreadyCancelled_ReturnsBadRequest()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db, status: "Cancelled");
        try
        {
            var result = await ControllerFor(db, owner.Id).CancelOrder(order.Id);

            Assert.IsType<BadRequestObjectResult>(result);
        }
        finally { await Cleanup(db, owner, stranger); }
    }

    [Fact]
    public async Task CancelOrder_WithoutIdentity_ReturnsUnauthorized()
    {
        var db = GetTestDb();
        var (owner, stranger, order) = await Arrange(db);
        try
        {
            var result = await ControllerFor(db, userId: null).CancelOrder(order.Id);

            Assert.IsType<UnauthorizedResult>(result);
        }
        finally { await Cleanup(db, owner, stranger); }
    }
}
