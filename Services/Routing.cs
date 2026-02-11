using PackageDelivery.Data;
using PackageDelivery.Models;
using Microsoft.EntityFrameworkCore;

namespace PackageDelivery.Services;

/// <summary>
/// Útvonal-optimalizálás Nearest Neighbor algoritmussal.
/// Egy futár több rendelését sorrendbe rakja úgy, hogy a lehető legrövidebb útvonalat járja be.
/// </summary>
public class RoutingService
{
    private readonly DeliveryDBContext _context;

    public RoutingService(DeliveryDBContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Euklideszi távolság számítása két pont között.
    /// Képlet: √((x2-x1)² + (y2-y1)²)
    /// </summary>
    private double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    /// <summary>
    /// NEAREST NEIGHBOR ALGORITMUS: Legközelebbi szomszéd módszer az útvonal-optimalizáláshoz.
    /// TSP (Traveling Salesman Problem) közelítő megoldása.
    /// </summary>
    /// <param name="courierId">A futár ID-ja, akinek az útvonalát optimalizáljuk</param>
    /// <returns>Optimalizált útvonalterv</returns>
    public RoutePlan OptimizeRoute(int courierId)
    {
        // Futár adatainak lekérdezése
        var courier = _context.Couriers.Find(courierId);
        if (courier == null)
        {
            throw new ArgumentException($"Nem található futár ID-val: {courierId}");
        }

        // Futárhoz rendelt, még ki nem szállított rendelések
        var assignedOrders = _context.DeliveryOrders
            .Where(o => o.AssignedCourierId == courierId && o.Status != "Delivered")
            .ToList();

        if (!assignedOrders.Any())
        {
            Console.WriteLine($"ℹ️  {courier.Name} - Nincs kiszállítandó rendelés.");
            return new RoutePlan
            {
                CourierId = courierId,
                OptimizedOrderSequence = "", // ← JAVÍTVA
                EstimatedTotalMinutes = 0,   // ← JAVÍTVA
                CreatedAt = DateTime.Now
            };
        }

        // Nearest Neighbor: mindig a legközelebbi következő pontot választjuk
        var orderedRoute = new List<DeliveryOrder>();
        var remainingOrders = new List<DeliveryOrder>(assignedOrders);

        // Kezdőpozíció: futár jelenlegi helye
        double currentX = courier.CurrentLocationX;
        double currentY = courier.CurrentLocationY;
        double totalDistance = 0;

        // Addig megyünk, amíg van kiszállítatlan rendelés
        while (remainingOrders.Any())
        {
            // Legközelebbi rendelés keresése
            DeliveryOrder? nearestOrder = null;
            double minDistance = double.MaxValue;

            foreach (var order in remainingOrders)
            {
                double distance = CalculateDistance(currentX, currentY, order.DestX, order.DestY);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestOrder = order;
                }
            }

            if (nearestOrder != null)
            {
                // Hozzáadjuk az útvonalhoz
                orderedRoute.Add(nearestOrder);
                totalDistance += minDistance;

                // Frissítjük a jelenlegi pozíciót
                currentX = nearestOrder.DestX;
                currentY = nearestOrder.DestY;

                // Eltávolítjuk a listából
                remainingOrders.Remove(nearestOrder);
            }
        }

        // Becsült idő: 1 egység távolság = 1 perc (egyszerűsítés)
        int estimatedMinutes = (int)Math.Ceiling(totalDistance);

        // Útvonalterv létrehozása
        var routePlan = new RoutePlan
        {
            CourierId = courierId,
            OptimizedOrderSequence = string.Join(",", orderedRoute.Select(o => o.Id)), // ← JAVÍTVA
            EstimatedTotalMinutes = estimatedMinutes, // ← JAVÍTVA
            CreatedAt = DateTime.Now
        };

        // Mentés adatbázisba
        _context.RoutePlans.Add(routePlan);
        _context.SaveChanges();

        Console.WriteLine($"🗺️  {courier.Name} - Optimalizált útvonal: {orderedRoute.Count} rendelés, becsült idő: {estimatedMinutes} perc");

        return routePlan;
    }

    /// <summary>
    /// Összes futár útvonalának optimalizálása.
    /// </summary>
    public void OptimizeAllRoutes()
    {
        Console.WriteLine("\n🗺️  Útvonalak optimalizálása...");

        // Minden futár, akinek van hozzárendelt rendelése
        var couriersWithOrders = _context.DeliveryOrders
            .Where(o => o.AssignedCourierId != null && o.Status != "Delivered")
            .Select(o => o.AssignedCourierId!.Value)
            .Distinct()
            .ToList();

        foreach (var courierId in couriersWithOrders)
        {
            OptimizeRoute(courierId);
        }

        Console.WriteLine("✅ Útvonal-optimalizálás kész!\n");
    }
}
