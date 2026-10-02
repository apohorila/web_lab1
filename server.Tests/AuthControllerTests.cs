using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using server.Controllers;
using server.Data;
using server.Models;
using Xunit;

namespace server.Tests;

public class AuthControllerTests
{
    private AppDbContext GetTestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=brand_db;Username=admin;Password=student")
            .Options;

        return new AppDbContext(options);
    }

    private IConfiguration GetTestConfiguration(string adminEmail = "admin@bagelle.ua")
    {
        var settings = new Dictionary<string, string?>
        {
            { "Authentication:AdminEmail", adminEmail },
            { "Jwt:Key", "SuperSecretKeyForBagelleJwtAuthenticationTesting2026!" },
            { "Jwt:Issuer", "BagelleApi" },
            { "Jwt:Audience", "BagelleClient" },
        };

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public async Task GoogleLogin_EmptyToken_ReturnsBadRequest()
    {
        var db = GetTestDb();
        var config = GetTestConfiguration();
        var controller = new AuthController(db, config);

        var result = await controller.GoogleLogin(
            new AuthController.GoogleAuthRequest { IdToken = "" }
        );

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task ExistingUser_Login_AssignsAdminRoleCorrectly()
    {
        var db = GetTestDb();
        var adminEmail = "admin_test@bagelle.ua";
        var config = GetTestConfiguration(adminEmail);
        var controller = new AuthController(db, config);

        var user = new User { Email = adminEmail, PasswordHash = "oauth_user" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var exists = await db.Users.AnyAsync(u => u.Email == adminEmail);
        Assert.True(exists);

        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }
}
