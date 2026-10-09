using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var claim =
                User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
            return int.TryParse(claim, out int id) ? id : null;
        }

        public class CreateOrderItemDto
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public class CreateOrderDto
        {
            public string CustomerName { get; set; } = string.Empty;
            public string CustomerPhone { get; set; } = string.Empty;
            public string CustomerEmail { get; set; } = string.Empty;
            public string DeliveryCity { get; set; } = string.Empty;
            public string DeliveryAddress { get; set; } = string.Empty;
            public List<CreateOrderItemDto> Items { get; set; } = new();
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto.Items == null || !dto.Items.Any())
            {
                return BadRequest(new { message = "Кошик не може бути порожнім" });
            }
            if (
                string.IsNullOrWhiteSpace(dto.CustomerName)
                || string.IsNullOrWhiteSpace(dto.CustomerPhone)
                || string.IsNullOrWhiteSpace(dto.DeliveryAddress)
            )
            {
                return BadRequest(new { message = "Вкажіть ім'я, телефон та адресу доставки" });
            }

            var userId = GetCurrentUserId();

            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context
                .Products.Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                return BadRequest(new { message = "Один або декілька товарів не знайдено" });
            }

            var order = new Order
            {
                UserId = userId,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                CustomerEmail = dto.CustomerEmail,
                DeliveryCity = dto.DeliveryCity,
                DeliveryAddress = dto.DeliveryAddress,
                CreatedAt = DateTime.UtcNow,
                Status = "Pending",
            };

            decimal total = 0;
            foreach (var item in dto.Items)
            {
                var prod = products.First(p => p.Id == item.ProductId);
                var qty = item.Quantity > 0 ? item.Quantity : 1;
                total += prod.Price * qty;

                order.Items.Add(
                    new OrderItem
                    {
                        ProductId = prod.Id,
                        Quantity = qty,
                        UnitPrice = prod.Price,
                    }
                );
            }

            order.TotalAmount = total;
            _context.Orders.Add(order);

            if (userId.HasValue)
            {
                var cart = await _context
                    .Carts.Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId.Value);

                if (cart != null && cart.Items.Any())
                {
                    _context.CartItems.RemoveRange(cart.Items);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    message = "Замовлення успішно створено",
                    orderId = order.Id,
                    totalAmount = order.TotalAmount,
                    createdAt = order.CreatedAt,
                }
            );
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var orders = await _context
                .Orders.Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (userId == null && !isAdmin)
            {
                return Unauthorized();
            }

            var order = await _context
                .Orders.Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && (isAdmin || o.UserId == userId));

            if (order == null)
            {
                return NotFound(new { message = "Замовлення не знайдено" });
            }

            return Ok(order);
        }

        [Authorize]
        [HttpGet]
        [Authorize]
        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (userId == null && !isAdmin)
            {
                return Unauthorized();
            }

            var order = await _context
                .Orders.Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id && (isAdmin || o.UserId == userId));

            if (order == null)
            {
                return NotFound(new { message = "Замовлення не знайдено" });
            }

            if (order.Status != "Pending")
            {
                return BadRequest(
                    new { message = "Скасувати можна лише замовлення, що очікує обробки" }
                );
            }

            order.Status = "Cancelled";
            await _context.SaveChangesAsync();

            return Ok(order);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (userId == null && !isAdmin)
            {
                return Unauthorized();
            }

            IQueryable<Order> query = _context
                .Orders.Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(o => o.CreatedAt);

            if (!isAdmin)
            {
                query = query.Where(o => o.UserId == userId);
            }

            var orders = await query.ToListAsync();
            return Ok(orders);
        }
    }
}
