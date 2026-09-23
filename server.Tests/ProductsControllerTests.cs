using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
using Xunit;

namespace server.Tests;

public class ProductsControllerTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAllProducts_ReturnsList()
    {
        var db = GetTestDb();
        var controller = new ProductsController(db);

        var result = await controller.GetProducts();

        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task GetProductById_ExistingId_ReturnsProduct()
    {
        var db = GetTestDb();
        var controller = new ProductsController(db);

        // Перевіряємо отримання першого товару за ID 1
        var result = await controller.GetProduct(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var product = Assert.IsType<Product>(okResult.Value);
        Assert.Equal(1, product.Id);
    }

    [Fact]
    public async Task GetProductById_InvalidId_ReturnsNotFound()
    {
        var db = GetTestDb();
        var controller = new ProductsController(db);

        var result = await controller.GetProduct(99999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Admin_CreateUpdateAndDeleteProduct_WorksCorrectly()
    {
        var db = GetTestDb();
        var controller = new ProductsController(db);

        var newProduct = new Product
        {
            Name = "Bagllet Test Bag",
            Price = 3600,
            Description = "Тестова модель сумки для автотесту",
            ImageUrl = "https://example.com/test-bag.jpg",
            CategoryId = 1,
            ShowroomId = 1
        };

        await controller.CreateProduct(newProduct);
        Assert.True(newProduct.Id > 0);

        newProduct.Name = "Bagllet Test Bag UPDATED";
        newProduct.Price = 4100;
        await controller.UpdateProduct(newProduct.Id, newProduct);

        var updatedInDb = await db.Products.FindAsync(newProduct.Id);
        Assert.NotNull(updatedInDb);
        Assert.Equal("Bagllet Test Bag UPDATED", updatedInDb.Name);
        Assert.Equal(4100, updatedInDb.Price);

        await controller.DeleteProduct(newProduct.Id);
        var deleted = await db.Products.FindAsync(newProduct.Id);
        Assert.Null(deleted);
    }
}