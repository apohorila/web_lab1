using System.ComponentModel.DataAnnotations;

namespace server.Models
{
    public class Order
    {
        public int Id { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending"; 

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CustomerEmail { get; set; } = string.Empty;

        [MaxLength(100)]
        public string DeliveryCity { get; set; } = string.Empty;

        [MaxLength(200)]
        public string DeliveryAddress { get; set; } = string.Empty;

        public List<OrderItem> Items { get; set; } = new();
    }
}
