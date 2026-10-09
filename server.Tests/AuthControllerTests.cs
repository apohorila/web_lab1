using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using server.Controllers;
using server.Data;
using server.Models;
using Xunit;

namespace server.Tests;

public class AuthControllerTests
{
    private const string ValidPassword = "Passw0rd!";
    private const string JwtKey = "SuperSecretKeyForBagelleJwtAuthenticationTesting2026!";
    private const string JwtIssuer = "BagelleApi";
    private const string JwtAudience = "BagelleClient";

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
            { "Jwt:Key", JwtKey },
            { "Jwt:Issuer", JwtIssuer },
            { "Jwt:Audience", JwtAudience },
        };

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static string UniqueEmail() => $"test_{Guid.NewGuid():N}@example.com";

    private static async Task DeleteUser(AppDbContext db, string email)
    {
        var lower = email.ToLower();
        var users = await db.Users.Where(u => u.Email.ToLower() == lower).ToListAsync();
        db.Users.RemoveRange(users);
        await db.SaveChangesAsync();
    }

    private static JsonElement ToJson(object? value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement;

    private static async Task<string> RegisterAndGetToken(AuthController controller, string email)
    {
        var result = await controller.Register(
            new RegisterRequest { Email = email, Password = ValidPassword }
        );
        var ok = Assert.IsType<OkObjectResult>(result);
        return ToJson(ok.Value).GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Register_ValidData_ReturnsOkWithTokenAndCustomerRole()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var result = await controller.Register(
                new RegisterRequest { Email = email, Password = ValidPassword }
            );

            var ok = Assert.IsType<OkObjectResult>(result);
            var json = ToJson(ok.Value);
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
            Assert.Equal(email, json.GetProperty("user").GetProperty("email").GetString());
            Assert.Equal("Customer", json.GetProperty("user").GetProperty("role").GetString());
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            await controller.Register(
                new RegisterRequest { Email = email, Password = ValidPassword }
            );

            var user = await db.Users.SingleAsync(u => u.Email == email);

            Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
            Assert.NotEqual(ValidPassword, user.PasswordHash);
            Assert.DoesNotContain(ValidPassword, user.PasswordHash);

            var verify = new PasswordHasher<User>().VerifyHashedPassword(
                user,
                user.PasswordHash,
                ValidPassword
            );
            Assert.Equal(PasswordVerificationResult.Success, verify);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Register_SamePasswordTwice_ProducesDifferentHashes()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email1 = UniqueEmail();
        var email2 = UniqueEmail();
        try
        {
            await controller.Register(
                new RegisterRequest { Email = email1, Password = ValidPassword }
            );
            await controller.Register(
                new RegisterRequest { Email = email2, Password = ValidPassword }
            );

            var hash1 = (await db.Users.SingleAsync(u => u.Email == email1)).PasswordHash;
            var hash2 = (await db.Users.SingleAsync(u => u.Email == email2)).PasswordHash;

            Assert.NotEqual(hash1, hash2); // унікальна сіль для кожного користувача
        }
        finally
        {
            await DeleteUser(db, email1);
            await DeleteUser(db, email2);
        }
    }

    [Fact]
    public async Task Register_EmailMatchingConfiguredAdmin_StillGetsCustomerRole()
    {
        var db = GetTestDb();
        var email = UniqueEmail();
        var controller = new AuthController(db, GetTestConfiguration(adminEmail: email));
        try
        {
            var result = await controller.Register(
                new RegisterRequest { Email = email, Password = ValidPassword }
            );

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(
                "Customer",
                ToJson(ok.Value).GetProperty("user").GetProperty("role").GetString()
            );
            Assert.Equal("Customer", (await db.Users.SingleAsync(u => u.Email == email)).Role);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Theory]
    [InlineData("", "Passw0rd!")]
    [InlineData("   ", "Passw0rd!")]
    [InlineData("user@example.com", "")]
    [InlineData("user@example.com", "   ")]
    public async Task Register_MissingEmailOrPassword_ReturnsBadRequest(
        string email,
        string password
    )
    {
        var controller = new AuthController(GetTestDb(), GetTestConfiguration());

        var result = await controller.Register(
            new RegisterRequest { Email = email, Password = password }
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("plainaddress.com")]
    [InlineData("@no-local-part.com")]
    [InlineData("no-domain@")]
    public async Task Register_InvalidEmailFormat_ReturnsBadRequest(string email)
    {
        var controller = new AuthController(GetTestDb(), GetTestConfiguration());

        var result = await controller.Register(
            new RegisterRequest { Email = email, Password = ValidPassword }
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("short1")] 
    [InlineData("1234567")] 
    [InlineData("longpasswordwithoutdigit")] 
    public async Task Register_WeakPassword_ReturnsBadRequest(string password)
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var result = await controller.Register(
                new RegisterRequest { Email = email, Password = password }
            );

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.False(await db.Users.AnyAsync(u => u.Email == email));
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var request = new RegisterRequest { Email = email, Password = ValidPassword };

            Assert.IsType<OkObjectResult>(await controller.Register(request));
            Assert.IsType<BadRequestObjectResult>(await controller.Register(request));
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Register_DuplicateEmailInDifferentCase_ReturnsBadRequest()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            Assert.IsType<OkObjectResult>(
                await controller.Register(
                    new RegisterRequest { Email = email, Password = ValidPassword }
                )
            );

            var result = await controller.Register(
                new RegisterRequest { Email = email.ToUpper(), Password = ValidPassword }
            );

            Assert.IsType<BadRequestObjectResult>(result);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            await RegisterAndGetToken(controller, email);

            var result = await controller.Login(
                new LoginRequest { Email = email, Password = ValidPassword }
            );

            var ok = Assert.IsType<OkObjectResult>(result);
            var json = ToJson(ok.Value);
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
            Assert.Equal(email, json.GetProperty("user").GetProperty("email").GetString());
            Assert.Equal("Customer", json.GetProperty("user").GetProperty("role").GetString());
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            await RegisterAndGetToken(controller, email);

            var result = await controller.Login(
                new LoginRequest { Email = email, Password = "WrongPassw0rd!" }
            );

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var controller = new AuthController(GetTestDb(), GetTestConfiguration());

        var result = await controller.Login(
            new LoginRequest { Email = UniqueEmail(), Password = ValidPassword }
        );

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnIdenticalMessage()
    {
        
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            await RegisterAndGetToken(controller, email);

            var wrongPassword = Assert.IsType<UnauthorizedObjectResult>(
                await controller.Login(
                    new LoginRequest { Email = email, Password = "WrongPassw0rd!" }
                )
            );
            var unknownEmail = Assert.IsType<UnauthorizedObjectResult>(
                await controller.Login(
                    new LoginRequest { Email = UniqueEmail(), Password = ValidPassword }
                )
            );

            Assert.Equal(
                ToJson(wrongPassword.Value).GetProperty("message").GetString(),
                ToJson(unknownEmail.Value).GetProperty("message").GetString()
            );
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Login_EmailInDifferentCase_ReturnsOk()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            await RegisterAndGetToken(controller, email);

            var result = await controller.Login(
                new LoginRequest { Email = email.ToUpper(), Password = ValidPassword }
            );

            Assert.IsType<OkObjectResult>(result);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Theory]
    [InlineData("", "Passw0rd!")]
    [InlineData("user@example.com", "")]
    public async Task Login_MissingEmailOrPassword_ReturnsBadRequest(string email, string password)
    {
        var controller = new AuthController(GetTestDb(), GetTestConfiguration());

        var result = await controller.Login(
            new LoginRequest { Email = email, Password = password }
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_GoogleOnlyAccount_CannotLoginWithPassword()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        db.Users.Add(
            new User
            {
                Email = email,
                FullName = "g",
                PasswordHash = "OAUTH_GOOGLE",
            }
        );
        await db.SaveChangesAsync();
        try
        {
            
            var result = await controller.Login(
                new LoginRequest { Email = email, Password = "OAUTH_GOOGLE" }
            );

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Login_LegacyAccountWithoutPassword_ReturnsUnauthorized()
    {
       
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        db.Users.Add(
            new User
            {
                Email = email,
                FullName = "legacy",
                PasswordHash = "",
            }
        );
        await db.SaveChangesAsync();
        try
        {
            var result = await controller.Login(
                new LoginRequest { Email = email, Password = ValidPassword }
            );

            Assert.IsType<UnauthorizedObjectResult>(result);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

   

    [Fact]
    public async Task Token_ContainsUserIdEmailRoleAndNameClaims()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var token = await RegisterAndGetToken(controller, email);
            var user = await db.Users.SingleAsync(u => u.Email == email);

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal(user.Id.ToString(), jwt.Subject);
            Assert.Contains(jwt.Claims, c => c.Value == email);
            Assert.Contains(jwt.Claims, c => c.Value == "Customer");
            Assert.Contains(jwt.Claims, c => c.Type == "fullName" && c.Value == user.FullName);
            Assert.Equal(JwtIssuer, jwt.Issuer);
            Assert.Contains(JwtAudience, jwt.Audiences);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Token_ExpiresInAboutSevenDays()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var token = await RegisterAndGetToken(controller, email);

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var lifetime = jwt.ValidTo - DateTime.UtcNow;

            Assert.InRange(lifetime.TotalDays, 6.9, 7.1);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    private static TokenValidationParameters ValidationParameters(string key) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = JwtIssuer,
            ValidateAudience = true,
            ValidAudience = JwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        };

    [Fact]
    public async Task Token_IsAcceptedWithConfiguredKey()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var token = await RegisterAndGetToken(controller, email);

            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                ValidationParameters(JwtKey),
                out var validated
            );

            Assert.NotNull(principal);
            Assert.NotNull(validated);
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Token_IsRejectedWithDifferentKey()
    {
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var token = await RegisterAndGetToken(controller, email);

            Assert.ThrowsAny<SecurityTokenException>(() =>
                new JwtSecurityTokenHandler().ValidateToken(
                    token,
                    ValidationParameters("AnotherCompletelyDifferentSecretKey-0123456789!!"),
                    out _
                )
            );
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    [Fact]
    public async Task Token_WithTamperedPayload_IsRejected()
    {
       
        var db = GetTestDb();
        var controller = new AuthController(db, GetTestConfiguration());
        var email = UniqueEmail();
        try
        {
            var token = await RegisterAndGetToken(controller, email);
            var parts = token.Split('.');

            var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
            var forgedJson = payloadJson.Replace("Customer", "Admin");
            Assert.NotEqual(payloadJson, forgedJson);

            var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(forgedJson)}.{parts[2]}";

            Assert.ThrowsAny<SecurityTokenException>(() =>
                new JwtSecurityTokenHandler().ValidateToken(
                    forged,
                    ValidationParameters(JwtKey),
                    out _
                )
            );
        }
        finally
        {
            await DeleteUser(db, email);
        }
    }

    

    [Fact]
    public async Task GoogleLogin_EmptyToken_ReturnsBadRequest()
    {
        var controller = new AuthController(GetTestDb(), GetTestConfiguration());

        var result = await controller.GoogleLogin(
            new AuthController.GoogleAuthRequest { IdToken = "" }
        );

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_MalformedToken_ReturnsBadRequest()
    {
        var controller = new AuthController(GetTestDb(), GetTestConfiguration());

        var result = await controller.GoogleLogin(
            new AuthController.GoogleAuthRequest { IdToken = "this.is.not-a-google-token" }
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
