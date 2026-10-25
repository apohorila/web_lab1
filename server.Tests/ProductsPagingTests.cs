using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
using server.Paging;
using Xunit;

namespace server.Tests;


public class ProductsPagingTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    private static ProductsController ControllerFor(AppDbContext db, string query = "")
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("localhost:5038");
        http.Request.Path = "/api/products";
        http.Request.QueryString = new QueryString(query);

        return new ProductsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private sealed record Seed(Category Category, Showroom Showroom, List<Product> Products);

    
    private static async Task<Seed> SeedProducts(AppDbContext db, int count)
    {
        var category = new Category { Name = "Пагінація Тест" };
        var showroom = new Showroom { Name = "Пагінація Шоурум", Address = "вул. Тестова, 1" };
        db.Categories.Add(category);
        db.Showrooms.Add(showroom);
        await db.SaveChangesAsync();

        var products = Enumerable
            .Range(1, count)
            .Select(i => new Product
            {
                Name = $"Pg {i:00}",
                Price = 1000 + i,
                CategoryId = category.Id,
                ShowroomId = showroom.Id,
            })
            .ToList();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        return new Seed(category, showroom, products);
    }

    private static async Task Cleanup(AppDbContext db, Seed seed)
    {
        db.Products.RemoveRange(seed.Products);
        await db.SaveChangesAsync();
        db.Categories.Remove(seed.Category);
        db.Showrooms.Remove(seed.Showroom);
        await db.SaveChangesAsync();
    }

    private static PagedResult<Product> PageOf(ActionResult<object> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<PagedResult<Product>>(ok.Value);
    }

    [Fact]
    public async Task Pages_DoNotOverlap_AndCoverEveryProduct_InStableOrder()
    {
        var db = GetTestDb();
        var seed = await SeedProducts(db, 7);
        try
        {
            var first = PageOf(await ControllerFor(db, $"?categoryId={seed.Category.Id}&skip=0&limit=3").GetProducts(seed.Category.Id, 0, 3));
            var second = PageOf(await ControllerFor(db, $"?categoryId={seed.Category.Id}&skip=3&limit=3").GetProducts(seed.Category.Id, 3, 3));
            var third = PageOf(await ControllerFor(db, $"?categoryId={seed.Category.Id}&skip=6&limit=3").GetProducts(seed.Category.Id, 6, 3));

            Assert.Equal(3, first.Items.Count);
            Assert.Equal(3, second.Items.Count);
            Assert.Equal(1, third.Items.Count);
            Assert.Equal(7, first.Total);
            Assert.Equal(7, third.Total);

            var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(p => p.Id).ToArray();
            Assert.Equal(seed.Products.OrderBy(p => p.Id).Select(p => p.Id).ToArray(), ids);
        }
        finally
        {
            await Cleanup(db, seed);
        }
    }

    [Fact]
    public async Task NextLink_ChainsThePagesAndEndsWithNull()
    {
        var db = GetTestDb();
        var seed = await SeedProducts(db, 7);
        try
        {
            var first = PageOf(await ControllerFor(db, $"?categoryId={seed.Category.Id}&skip=0&limit=3").GetProducts(seed.Category.Id, 0, 3));
            var third = PageOf(await ControllerFor(db, $"?categoryId={seed.Category.Id}&skip=6&limit=3").GetProducts(seed.Category.Id, 6, 3));

            Assert.NotNull(first.NextLink);
            Assert.Contains("skip=3", first.NextLink);
            Assert.Contains("limit=3", first.NextLink);
            Assert.Contains($"categoryId={seed.Category.Id}", first.NextLink); // фільтр зберігається
            Assert.Null(third.NextLink);
        }
        finally
        {
            await Cleanup(db, seed);
        }
    }

    [Fact]
    public async Task Items_IncludeCategoryAndShowroom()
    {
        var db = GetTestDb();
        var seed = await SeedProducts(db, 2);
        try
        {
            var page = PageOf(await ControllerFor(db, "?limit=5").GetProducts(seed.Category.Id, null, 5));

            Assert.Equal("Пагінація Тест", page.Items[0].Category?.Name);
            Assert.Equal("Пагінація Шоурум", page.Items[0].Showroom?.Name);
            Assert.Equal(0, page.Skip);
        }
        finally
        {
            await Cleanup(db, seed);
        }
    }

    [Fact]
    public async Task SkipBeyondTheEnd_ReturnsAnEmptyPageWithoutNextLink()
    {
        var db = GetTestDb();
        var seed = await SeedProducts(db, 3);
        try
        {
            var page = PageOf(await ControllerFor(db, "?skip=100&limit=5").GetProducts(seed.Category.Id, 100, 5));

            Assert.Equal(0, page.Items.Count);
            Assert.Equal(3, page.Total);
            Assert.Null(page.NextLink);
        }
        finally
        {
            await Cleanup(db, seed);
        }
    }

    [Fact]
    public async Task InvalidParameters_ReturnBadRequest()
    {
        var db = GetTestDb();
        var controller = ControllerFor(db);

        Assert.IsType<BadRequestObjectResult>((await controller.GetProducts(null, -1, 10)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetProducts(null, 0, 0)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetProducts(null, 0, -3)).Result);
    }

    [Fact]
    public async Task TooLargeLimit_IsClampedToTheMaximum()
    {
        var db = GetTestDb();
        var seed = await SeedProducts(db, 2);
        try
        {
            var page = PageOf(await ControllerFor(db, "?limit=100000").GetProducts(seed.Category.Id, null, 100000));

            Assert.Equal(100, page.Limit);
            Assert.Equal(2, page.Items.Count);
        }
        finally
        {
            await Cleanup(db, seed);
        }
    }

    
    [Fact]
    public async Task WithoutPagingParameters_ReturnsThePlainList()
    {
        var db = GetTestDb();
        var seed = await SeedProducts(db, 4);
        try
        {
            var result = await ControllerFor(db).GetProducts(seed.Category.Id);

            Assert.Null(result.Result); // не обгортка, а саме список
            var list = Assert.IsAssignableFrom<IEnumerable<Product>>(result.Value);
            Assert.Equal(4, list.Count());
        }
        finally
        {
            await Cleanup(db, seed);
        }
    }

    [Fact]
    public async Task CategoryFilter_ReturnsOnlyProductsOfThatCategory()
    {
        var db = GetTestDb();
        var seed = await SeedProducts(db, 3);
        try
        {
            var filtered = Assert.IsAssignableFrom<IEnumerable<Product>>((await ControllerFor(db).GetProducts(seed.Category.Id)).Value);
            var unknown = Assert.IsAssignableFrom<IEnumerable<Product>>((await ControllerFor(db).GetProducts(int.MaxValue)).Value);

            Assert.Equal(3, filtered.Count());
            Assert.True(filtered.All(p => p.CategoryId == seed.Category.Id));
            Assert.Equal(0, unknown.Count());
        }
        finally
        {
            await Cleanup(db, seed);
        }
    }

    [Fact]
    public async Task WithoutAnyParameters_StillReturnsEveryProduct()
    {
        var db = GetTestDb();

        var result = await ControllerFor(db).GetProducts();

        Assert.NotNull(result.Value);
        Assert.Equal(await db.Products.CountAsync(), Assert.IsAssignableFrom<IEnumerable<Product>>(result.Value).Count());
    }
}
