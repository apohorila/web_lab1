namespace server.Models;

public class Showroom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}