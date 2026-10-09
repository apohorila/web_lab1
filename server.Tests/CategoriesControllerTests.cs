using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace server.Tests;

public class CategoriesControllerTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task ReturnExistingCategories()
    {
        var db = GetTestDb();
        var controller = new CategoriesController(db);

        var result = await controller.GetCategories();

        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Create_DeleteCategory()
    {
        var db = GetTestDb();
        var controller = new CategoriesController(db);

        var result = await controller.CreateCategory(
            new CategoryCreateDto { Name = "Рюкзаки", Description = "Тест" }
        );

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var category = Assert.IsType<Category>(created.Value);
        Assert.True(category.Id > 0);

        var categoryInDb = await db.Categories.FindAsync(category.Id);
        Assert.NotNull(categoryInDb);
        Assert.Equal("Рюкзаки", categoryInDb.Name);

        await controller.DeleteCategory(category.Id);
        var deleted = await db.Categories.FindAsync(category.Id);
        Assert.Null(deleted);
    }
}
