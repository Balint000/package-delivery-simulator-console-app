/// <summary>
/// A "Greedy" és a "Nearest Neighbor" algoritmusokat különítsük el a szimuláció futtatásától.
/// Így később könnyen lecserélheted őket egy komolyabb (pl. Genetic vagy A*) algoritmusra anélkül, hogy a kód többi része törne.
/// </summary>

using PackageDelivery.Data;
using PackageDelivery.Models;
using Microsoft.EntityFrameworkCore;

namespace PackageDelivery.Services;

/// <summary>
/// Futárok és rendelések hozzárendelése Greedy algoritmussal.
/// Mindig a legközelebbi szabad futárt választja ki egy adott rendeléshez.
/// </summary>
public class AssignmentService
{
    private readonly DeliveryDBContext _context;

    public AssignmentService(DeliveryDBContext context)
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
    /// GREEDY ALGORITMUS: Legközelebbi szabad futár keresése egy rendeléshez.
    /// </summary>
    /// <param name="order">A rendelés, amelyhez futárt keresünk</param>
    /// <returns>A legközelebbi szabad futár, vagy null ha nincs elérhető</returns>
    public Courier? FindNearestAvailableCourier(DeliveryOrder order)
    {
        // Lekérdezzük az összes elérhető futárt
        var availableCouriers = _context.Couriers
            .Where(c => c.IsAvailable) // Csak a szabad futárok
            .ToList();

        if (!availableCouriers.Any())
        {
            return null; // Nincs elérhető futár
        }

        // Greedy: megkeressük a legközelebb lévőt
        Courier? nearestCourier = null;
        double minDistance = double.MaxValue;

        foreach (var courier in availableCouriers)
        {
            // Távolság számítása a futár jelenlegi pozíciója és a rendelés célpontja között
            double distance = CalculateDistance(
                courier.CurrentLocationX, courier.CurrentLocationY,
                order.DestX, order.DestY
            );

            // Ha ez a legközelebbi eddig, megjegyezzük
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestCourier = courier;
            }
        }

        return nearestCourier;
    }

    /// <summary>
    /// Rendelés hozzárendelése futárhoz és státusz frissítése.
    /// </summary>
    /// <param name="order">A rendelés</param>
    /// <param name="courier">A futár</param>
    public void AssignOrderToCourier(DeliveryOrder order, Courier courier)
    {
        // Rendelés hozzárendelése
        order.AssignedCourierId = courier.Id;
        order.Status = "Assigned";

        // Futár foglalttá tétele
        courier.IsAvailable = false;

        // Státusztörténet rögzítése
        var statusHistory = new StatusHistory
        {
            DeliveryOrderId = order.Id,
            NewStatus = "Assigned",
            Timestamp = DateTime.Now,
            Comment = $"Hozzárendelve: {courier.Name}"
        };
        _context.StatusHistories.Add(statusHistory);

        // Mentés
        _context.SaveChanges();

        Console.WriteLine($"📦 Rendelés #{order.Id} -> Futár: {courier.Name} (Távolság: {CalculateDistance(courier.CurrentLocationX, courier.CurrentLocationY, order.DestX, order.DestY):F2})");
    }

    /// <summary>
    /// Összes függőben lévő rendelés hozzárendelése (batch processing).
    /// </summary>
    public void AssignAllPendingOrders()
    {
        Console.WriteLine("\n🔄 Rendelések hozzárendelése...");

        // Lekérdezzük a függőben lévő rendeléseket
        var pendingOrders = _context.DeliveryOrders
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.Deadline) // Sürgősebbek előre
            .ToList();

        int assignedCount = 0;

        foreach (var order in pendingOrders)
        {
            var courier = FindNearestAvailableCourier(order);

            if (courier != null)
            {
                AssignOrderToCourier(order, courier);
                assignedCount++;
            }
            else
            {
                Console.WriteLine($"⚠️  Rendelés #{order.Id} - Nincs elérhető futár!");
            }
        }

        Console.WriteLine($"✅ {assignedCount}/{pendingOrders.Count} rendelés hozzárendelve.\n");
    }
}
