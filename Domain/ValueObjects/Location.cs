namespace package_delivery_simulator.Domain.ValueObjects;

/// <summary>
/// Represents a 2D point on the city map.
/// This is a value object: it describes a position, not an entity.
/// </summary>
public record Location(double X, double Y)
{
    /// <summary>
    /// Calculates the straight-line (Euclidean) distance
    /// from this location to another location.
    /// Formula: √((x2 - x1)² + (y2 - y1)²)
    /// </summary>
    /// <param name="other">The target location</param>
    /// <returns>Distance as a double value</returns>
    public double DistanceTo(Location other)
    {
        // Difference along X axis
        double deltaX = other.X - X;

        // Difference along Y axis
        double deltaY = other.Y - Y;

        // Pythagorean theorem
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    /// <summary>
    /// Returns a readable string like "(12.34, 56.78)".
    /// </summary>
    public override string ToString() => $"({X:F2}, {Y:F2})";
}
