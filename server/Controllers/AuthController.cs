using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
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
            if (
                string.IsNullOrWhiteSpace(request.Email)
                || string.IsNullOrWhiteSpace(request.Password)
            )
            {
                return BadRequest(new { message = "Email та пароль є обов'язковими" });
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email == request.Email
            );
            if (existingUser != null)
            {
                return BadRequest(new { message = "Користувач із таким email уже зареєстрований" });
            }

            var role =
                request.Email == "admin@bagelle.ua" || request.Email == "bagelleadmin@gmail.com"
                    ? "Admin"
                    : "Customer";

            var user = new User
            {
                Email = request.Email,
                FullName = request.Email.Split('@')[0],
                Role = role,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return Ok(
                new
                {
                    token = token,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        name = user.FullName,
                        role = user.Role,
                    },
                }
            );
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return Unauthorized(new { message = "Невірний email або пароль" });
            }

            var token = GenerateJwtToken(user);

            return Ok(
                new
                {
                    token = token,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        name = user.FullName,
                        role = user.Role,
                    },
                }
            );
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
                    PasswordHash = "OAUTH_GOOGLE",
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
