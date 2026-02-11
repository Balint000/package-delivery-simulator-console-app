/// <summary>
/// A TPL alapú párhuzamos futtatás
/// </summary>
using PackageDelivery.Data;
using PackageDelivery.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace PackageDelivery.Services;

/// <summary>
/// A szimuláció fő motorja - Task Parallel Library (TPL) használatával párhuzamosan futtatja a futárokat.
/// Valós idejű státusz kiírással a konzolra.
/// </summary>
public class SimulationEngine
{
    private readonly DeliveryDBContext _context;
    private readonly ConcurrentDictionary<int, string> _courierStatuses; // Thread-safe futár státuszok

    public SimulationEngine(DeliveryDBContext context)
    {
        _context = context;
        _courierStatuses = new ConcurrentDictionary<int, string>();
    }

    /// <summary>
    /// Euklideszi távolság számítása.
    /// </summary>
    private double CalculateDistance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
    }

    /// <summary>
    /// Egy futár szimulációja - ez fog párhuzamosan futni minden futárra.
    /// </summary>
    /// <param name="courierId">A futár ID-ja</param>
    /// <param name="cancellationToken">Leállítási token</param>
    private async Task SimulateCourierAsync(int courierId, CancellationToken cancellationToken)
    {
        // Minden futárnak saját DbContext példánya kell (thread-safety miatt)
        using var courierContext = new DeliveryDBContext();

        var courier = await courierContext.Couriers.FindAsync(courierId);
        if (courier == null) return;

        _courierStatuses[courierId] = $"{courier.Name}: Indulás...";

        // Futár útvonaltervének lekérdezése
        var routePlan = await courierContext.RoutePlans
                    .Where(rp => rp.CourierId == courierId)
                    .OrderByDescending(rp => rp.CreatedAt)
                    .FirstOrDefaultAsync();

        if (routePlan == null || string.IsNullOrEmpty(routePlan.OptimizedOrderSequence)) // ← JAVÍTVA
        {
            _courierStatuses[courierId] = $"{courier.Name}: Nincs útvonalterv";
            return;
        }

        // Útvonal rendelések ID-inak parsálása
        var orderIds = routePlan.OptimizedOrderSequence.Split(',').Select(int.Parse).ToList();

        // Rendelések kiszállítása egyesével
        foreach (var orderId in orderIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var order = await courierContext.DeliveryOrders.FindAsync(orderId);
            if (order == null) continue;

            // Távolság számítása
            double distance = CalculateDistance(
                courier.CurrentLocationX, courier.CurrentLocationY,
                order.DestX, order.DestY
            );

            // Státusz frissítés: útban
            order.Status = "InProgress";
            _courierStatuses[courierId] = $"{courier.Name}: Úton rendelés #{orderId} felé ({distance:F1} egység)";

            // StatusHistory rögzítés
            courierContext.StatusHistories.Add(new StatusHistory
            {
                DeliveryOrderId = orderId,
                NewStatus = "InProgress",
                Timestamp = DateTime.Now,
                Comment = $"{courier.Name} úton van"
            });

            await courierContext.SaveChangesAsync();

            // Utazás szimulálása (1 egység = 100ms)
            int travelTimeMs = (int)(distance * 100);
            await Task.Delay(travelTimeMs, cancellationToken);

            // Kiszállítás
            order.DeliveredAt = DateTime.Now;
            order.Status = "Delivered";

            // Késés ellenőrzése
            bool isDelayed = order.DeliveredAt > order.Deadline;
            int delayMinutes = isDelayed ? (int)(order.DeliveredAt.Value - order.Deadline).TotalMinutes : 0;

            if (isDelayed && !order.WasDelayNotificationSent)
            {
                // EXTRA: Késés esetén értesítés
                _courierStatuses[courierId] = $"{courier.Name}: ⚠️ KÉSÉS! Rendelés #{orderId} ({delayMinutes} perc)";
                order.WasDelayNotificationSent = true;

                courierContext.StatusHistories.Add(new StatusHistory
                {
                    DeliveryOrderId = orderId,
                    NewStatus = "Delayed",
                    Timestamp = DateTime.Now,
                    Comment = $"Késés: {delayMinutes} perc"
                });

                await Task.Delay(500); // Értesítés megjelenítése
            }
            else
            {
                _courierStatuses[courierId] = $"{courier.Name}: ✅ Kiszállítva rendelés #{orderId}";
            }

            // Futár pozíciójának frissítése
            courier.CurrentLocationX = order.DestX;
            courier.CurrentLocationY = order.DestY;
            courier.CompletedDeliveries++;
            courier.TotalDistanceTraveled += distance;

            if (isDelayed)
            {
                courier.TotalDelayMinutes += delayMinutes;
            }

            // StatusHistory: Delivered
            courierContext.StatusHistories.Add(new StatusHistory
            {
                DeliveryOrderId = orderId,
                NewStatus = "Delivered",
                Timestamp = DateTime.Now,
                Comment = isDelayed ? $"Kiszállítva {delayMinutes} perc késéssel" : "Időben kiszállítva"
            });

            await courierContext.SaveChangesAsync();
            await Task.Delay(300); // Kis szünet a következő rendelés előtt
        }

        // Futár szabaddá válik
        courier.IsAvailable = true;
        _courierStatuses[courierId] = $"{courier.Name}: 🏁 Kész! ({courier.CompletedDeliveries} rendelés)";
        await courierContext.SaveChangesAsync();
    }

    /// <summary>
    /// Konzolos státusz kijelző - valós időben frissül.
    /// </summary>
    private async Task DisplayStatusAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Clear();
            Console.WriteLine("🚚 === CSOMAGKÉZBESÍTÉS SZIMULÁCIÓ - ÉLŐ STÁTUSZ ===\n");

            foreach (var status in _courierStatuses.OrderBy(s => s.Key))
            {
                Console.WriteLine($"  {status.Value}");
            }

            Console.WriteLine("\n[Nyomj CTRL+C a leállításhoz]");

            await Task.Delay(200, cancellationToken); // Frissítés 5x/másodperc
        }
    }

    /// <summary>
    /// A szimuláció indítása - TPL párhuzamos futtatással.
    /// </summary>
    public async Task RunSimulationAsync()
    {
        Console.WriteLine("\n🚀 Szimuláció indítása...\n");

        // Összes futár lekérdezése, akiknek van útvonaltervük
        var courierIds = await _context.RoutePlans
            .Select(rp => rp.CourierId)
            .Distinct()
            .ToListAsync();

        if (!courierIds.Any())
        {
            Console.WriteLine("⚠️ Nincs útvonalterv, nem lehet szimulálni!");
            return;
        }

        // CancellationToken a leállításhoz
        using var cts = new CancellationTokenSource();

        // CTRL+C kezelése
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Státusz megjelenítő Task indítása
        var displayTask = DisplayStatusAsync(cts.Token);

        // TPL: Párhuzamos futár szimulációk
        var courierTasks = courierIds.Select(id => SimulateCourierAsync(id, cts.Token)).ToList();

        try
        {
            // Várunk, amíg minden futár végez
            await Task.WhenAll(courierTasks);

            // Kis várakozás, hogy lássa a végeredményt
            await Task.Delay(2000);

            // Leállítjuk a státusz kijelzőt
            cts.Cancel();
            await displayTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n\n⚠️ Szimuláció megszakítva!\n");
        }

        Console.Clear();
        Console.WriteLine("✅ Szimuláció befejeződött!\n");
    }
}
