using Microsoft.EntityFrameworkCore;
using server.Controllers;
using server.Data;
using server.Models;
using Xunit;

namespace server.Tests;

public class ShowroomsControllerTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetShowrooms_ReturnsList()
    {
        var db = GetTestDb();
        var controller = new ShowroomsController(db);

        var result = await controller.GetShowrooms();

        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Admin_CreateAndDeleteShowroom_WorksCorrectly()
    {
        var db = GetTestDb();
        var controller = new ShowroomsController(db);

        var newShowroom = new Showroom
        {
            Name = "Bagllet Львів",
            Address = "вул. Галицька, 12",
            WorkingHours = "10:00 - 20:00",
            Phone = "+380501234567",
            Latitude = 49.8419,
            Longitude = 24.0315,
        };

        await controller.CreateShowroom(newShowroom);
        Assert.True(newShowroom.Id > 0);

        var inDb = await db.Showrooms.FindAsync(newShowroom.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Bagllet Львів", inDb.Name);
        Assert.Equal("+380501234567", inDb.Phone);

        await controller.DeleteShowroom(newShowroom.Id);
        var deleted = await db.Showrooms.FindAsync(newShowroom.Id);
        Assert.Null(deleted);
    }
}
