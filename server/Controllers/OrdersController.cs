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
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
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

            var userId = GetCurrentUserId();

            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

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
                Status = "Pending"
            };

            decimal total = 0;
            foreach (var item in dto.Items)
            {
                var prod = products.First(p => p.Id == item.ProductId);
                var qty = item.Quantity > 0 ? item.Quantity : 1;
                total += prod.Price * qty;

                order.Items.Add(new OrderItem
                {
                    ProductId = prod.Id,
                    Quantity = qty,
                    UnitPrice = prod.Price
                });
            }

            order.TotalAmount = total;
            _context.Orders.Add(order);

            if (userId.HasValue)
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId.Value);

                if (cart != null && cart.Items.Any())
                {
                    _context.CartItems.RemoveRange(cart.Items);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Замовлення успішно створено",
                orderId = order.Id,
                totalAmount = order.TotalAmount,
                createdAt = order.CreatedAt
            });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            IQueryable<Order> query = _context.Orders
                .Include(o => o.Items)
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