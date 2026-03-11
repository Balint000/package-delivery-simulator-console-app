namespace package_delivery_simulator.Domain.ValueObjects;

/// <summary>
/// Represents a 2D point on the city map.
/// This is a value object: it describes a position, not an entity.
/// </summary>
public record Location(double X, double Y)
{
    /// <summary>
    /// Returns a readable string like "(12.34, 56.78)".
    /// </summary>
    public override string ToString() => $"({X:F2}, {Y:F2})";
}
