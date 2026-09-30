using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;

namespace server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Користувача не ідентифіковано");
            }

            return userId;
        }

        public class AddToCartDto
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; } = 1;
        }

        public class UpdateQuantityDto
        {
            public int Quantity { get; set; }
        }

        public class GuestCartItemDto
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public class MergeCartDto
        {
            public List<GuestCartItemDto> Items { get; set; } = new();
        }

        [HttpGet]
        public async Task<ActionResult<Cart>> GetCart()
        {
            var userId = GetCurrentUserId();

            var cart = await _context
                .Carts.Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return Ok(cart);
        }

        [HttpPost("items")]
        public async Task<ActionResult> AddItemToCart([FromBody] AddToCartDto dto)
        {
            var userId = GetCurrentUserId();

            var cart = await _context
                .Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var productExists = await _context.Products.AnyAsync(p => p.Id == dto.ProductId);
            if (!productExists)
            {
                return NotFound(new { message = "Товар не знайдено" });
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == dto.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity > 0 ? dto.Quantity : 1;
            }
            else
            {
                cart.Items.Add(
                    new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = dto.ProductId,
                        Quantity = dto.Quantity > 0 ? dto.Quantity : 1,
                    }
                );
            }

            await _context.SaveChangesAsync();

            var updatedCart = await _context
                .Carts.Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            return Ok(updatedCart);
        }

        [HttpPut("items/{itemId}")]
        public async Task<ActionResult> UpdateQuantity(int itemId, [FromBody] UpdateQuantityDto dto)
        {
            var userId = GetCurrentUserId();

            var cartItem = await _context
                .CartItems.Include(i => i.Cart)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Cart!.UserId == userId);

            if (cartItem == null)
            {
                return NotFound(new { message = "Позицію в кошику не знайдено" });
            }

            if (dto.Quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
            }
            else
            {
                cartItem.Quantity = dto.Quantity;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("items/{itemId}")]
        public async Task<ActionResult> RemoveItem(int itemId)
        {
            var userId = GetCurrentUserId();

            var cartItem = await _context
                .CartItems.Include(i => i.Cart)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Cart!.UserId == userId);

            if (cartItem == null)
            {
                return NotFound(new { message = "Позицію в кошику не знайдено" });
            }

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("merge")]
        public async Task<ActionResult<Cart>> MergeCart([FromBody] MergeCartDto dto)
        {
            var userId = GetCurrentUserId();

            var cart = await _context
                .Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            if (dto.Items != null && dto.Items.Any())
            {
                var incomingIds = dto.Items.Select(x => x.ProductId).ToList();
                var validProductIds = await _context
                    .Products.Where(p => incomingIds.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                foreach (var guestItem in dto.Items)
                {
                    if (!validProductIds.Contains(guestItem.ProductId))
                        continue;

                    var existing = cart.Items.FirstOrDefault(i =>
                        i.ProductId == guestItem.ProductId
                    );
                    if (existing != null)
                    {
                        existing.Quantity += guestItem.Quantity > 0 ? guestItem.Quantity : 1;
                    }
                    else
                    {
                        cart.Items.Add(
                            new CartItem
                            {
                                CartId = cart.Id,
                                ProductId = guestItem.ProductId,
                                Quantity = guestItem.Quantity > 0 ? guestItem.Quantity : 1,
                            }
                        );
                    }
                }

                await _context.SaveChangesAsync();
            }

            var mergedCart = await _context
                .Carts.Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            return Ok(mergedCart);
        }
    }
}
