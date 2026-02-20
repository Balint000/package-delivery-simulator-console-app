namespace package_delivery_simulator.Domain.ValueObjects;

public struct Point
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    // Távolság számítás két pont között (Euklideszi távolság)
    public double DistanceTo(Point other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ToString override a könnyebb debugging-hoz
    public override string ToString()
    {
        return $"({X:F1}, {Y:F1})";
    }
}
