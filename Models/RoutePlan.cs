namespace PackageDelivery.Models;

/// <summary>
/// A futárok útvonaltervét tároló osztály.
/// </summary>
public class RoutePlan
{
    public int Id { get; set; }
    public int CourierId { get; set; }

    // Az optimalizált sorrend (pl. JSON-ben vagy vesszővel elválasztva tárolt Order ID-k)
    public string OptimizedOrderSequence { get; set; } = string.Empty;

    // Becsült menetidő percekben (a Nearest Neighbor alapján)
    public int EstimatedTotalMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
