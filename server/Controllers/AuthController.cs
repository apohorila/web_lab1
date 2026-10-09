using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using server.Data;
using server.Models;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        private const string OAuthPasswordMarker = "OAUTH_GOOGLE";
        private const int MinPasswordLength = 8;

        private static readonly PasswordHasher<User> PasswordHasher = new();

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public class GoogleAuthRequest
        {
            public string IdToken { get; set; } = string.Empty;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var email = (request.Email ?? string.Empty).Trim();
            var password = request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new { message = "Email та пароль є обов'язковими" });
            }

            if (!IsValidEmail(email))
            {
                return BadRequest(new { message = "Невірний формат email" });
            }

            if (password.Length < MinPasswordLength || !password.Any(char.IsDigit))
            {
                return BadRequest(
                    new { message = "Пароль має містити щонайменше 8 символів та цифру" }
                );
            }

            var emailLower = email.ToLower();
            var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
            if (exists)
            {
                return BadRequest(new { message = "Користувач із таким email уже зареєстрований" });
            }

            var user = new User
            {
                Email = email,
                FullName = email.Split('@')[0],
                Role = "Customer",
            };


            user.PasswordHash = PasswordHasher.HashPassword(user, password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var email = (request.Email ?? string.Empty).Trim();
            var password = request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return BadRequest(new { message = "Email та пароль є обов'язковими" });
            }

            var emailLower = email.ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email.ToLower() == emailLower
            );

            
            if (user == null || !VerifyPassword(user, password))
            {
                return Unauthorized(new { message = "Невірний email або пароль" });
            }

            return Ok(BuildAuthResponse(user));
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthRequest request)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new[] { _config["Authentication:GoogleClientId"] },
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch (Exception)
            {
                return BadRequest(new { message = "Недійсний токен Google" });
            }

            var email = payload.Email;
            var adminEmail = _config["Authentication:AdminEmail"];

            var user = await _context
                .Users.Include(u => u.Cart)
                .FirstOrDefaultAsync(u => u.Email == email);

            string assignedRole = string.Equals(
                email,
                adminEmail,
                StringComparison.OrdinalIgnoreCase
            )
                ? "Admin"
                : "Customer";

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    FullName = payload.Name ?? "Google User",
                    PasswordHash =  OAuthPasswordMarker,
                    Role = assignedRole,
                    Cart = new Cart(),
                };
                _context.Users.Add(user);
            }
            else
            {
                user.Role = assignedRole;
            }

            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return Ok(
                new
                {
                    token,
                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.FullName,
                        user.Role,
                    },
                }
            );
        }

        private static bool IsValidEmail(string email) =>
            email.Length <= 100
            && MailAddress.TryCreate(email, out var address)
            && address.Address == email;

        private static bool VerifyPassword(User user, string password)
        {
            // Google-акаунти та старі акаунти без пароля не можуть входити за паролем
            if (string.IsNullOrEmpty(user.PasswordHash) || user.PasswordHash == OAuthPasswordMarker)
            {
                return false;
            }

            try
            {
                return PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
                    != PasswordVerificationResult.Failed;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private object BuildAuthResponse(User user) =>
            new
            {
                token = GenerateJwtToken(user),
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    name = user.FullName,
                    role = user.Role,
                },
            };

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("fullName", user.FullName),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}