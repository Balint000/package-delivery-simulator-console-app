namespace package_delivery_simulator.Domain.Entities;

using package_delivery_simulator.Domain.ValueObjects;

public class Zone
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Point> Polygon { get; set; } // Zóna határvonalai

    public Zone(int id, string name, List<Point> polygon)
    {
        Id = id;
        Name = name;
        Polygon = polygon;
    }

    // Ellenőrzi, hogy egy pont benne van-e a zónában (ray-casting algoritmus)
    public bool ContainsPoint(Point p)
    {
        bool inside = false;
        int n = Polygon.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if ((Polygon[i].Y > p.Y) != (Polygon[j].Y > p.Y) &&
                (p.X < Polygon[i].X + (Polygon[j].X - Polygon[i].X) *
                (p.Y - Polygon[i].Y) / (Polygon[j].Y - Polygon[i].Y)))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    // Statikus metódus: előre definiált 4 zónát ad vissza (kitölti a teljes 500x500-as területet)
    public static List<Zone> GetPredefinedZones()
    {
        return new List<Zone>
        {
            // Zóna 1: bal felső negyed (kb.)
            new Zone(1, "Észak-Nyugat", new List<Point>
            {
                new(0, 0),
                new(250, 0),
                new(220, 250),
                new(0, 250)
            }),

            // Zóna 2: jobb felső negyed
            new Zone(2, "Észak-Kelet", new List<Point>
            {
                new(250, 0),
                new(500, 0),
                new(500, 220),
                new(280, 250),
                new(220, 250)
            }),

            // Zóna 3: bal alsó negyed
            new Zone(3, "Dél-Nyugat", new List<Point>
            {
                new(0, 250),
                new(220, 250),
                new(230, 500),
                new(0, 500)
            }),

            // Zóna 4: jobb alsó negyed
            new Zone(4, "Dél-Kelet", new List<Point>
            {
                new(220, 250),
                new(280, 250),
                new(500, 220),
                new(500, 500),
                new(230, 500)
            })
        };
    }
}
