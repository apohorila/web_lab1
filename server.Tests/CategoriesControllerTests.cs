using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
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

        var newCategory = new Category { Name = "Рюкзаки" };

        await controller.CreateCategory(newCategory);
        Assert.True(newCategory.Id > 0);

        var categoryInDb = await db.Categories.FindAsync(newCategory.Id);
        Assert.NotNull(categoryInDb);
        Assert.Equal("Рюкзаки", categoryInDb.Name);

        await controller.DeleteCategory(newCategory.Id);
        var deleted = await db.Categories.FindAsync(newCategory.Id);
        Assert.Null(deleted);
    }
}
