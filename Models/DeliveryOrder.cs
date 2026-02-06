namespace PackageDelivery.Models;

public class DeliveryOrder
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int ZoneId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Pending"; // Pending, Assigned, Delivered, Delayed
}
