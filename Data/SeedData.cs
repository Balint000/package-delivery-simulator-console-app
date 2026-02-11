using PackageDelivery.Models;

namespace PackageDelivery.Data;

/// <summary>
/// Tesztadatok generálása a szimuláció számára.
/// Zónákat, futárokat és rendeléseket hoz létre véletlenszerűen.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Adatbázis feltöltése kezdeti adatokkal.
    /// </summary>
    /// <param name="context">Az adatbázis kontextus</param>
    /// <param name="numberOfZones">Hány zónát hozzon létre</param>
    /// <param name="numberOfCouriers">Hány futárt hozzon létre</param>
    /// <param name="numberOfOrders">Hány rendelést hozzon létre</param>
    public static void Initialize(DeliveryDBContext context, int numberOfZones = 5, int numberOfCouriers = 10, int numberOfOrders = 50)
    {
        // Ha már van adat, ne töltse újra
        if (context.Zones.Any())
        {
            Console.WriteLine("⚠️  Az adatbázis már tartalmaz adatokat. Seed kihagyva.");
            return;
        }

        Console.WriteLine("🌱 Tesztadatok generálása...");

        var random = new Random();

        // --- ZÓNÁK LÉTREHOZÁSA ---
        var zones = new List<Zone>();
        string[] zoneNames = { "Észak", "Dél", "Kelet", "Nyugat", "Központ", "Külváros", "Iparnegyed", "Lakótelep" };

        for (int i = 0; i < numberOfZones; i++)
        {
            zones.Add(new Zone
            {
                Name = zoneNames[i % zoneNames.Length] + $" {i + 1}",
                CenterX = random.Next(0, 100), // 0-100 közötti koordináták
                CenterY = random.Next(0, 100),
                CurrentLoad = 0 // Kezdetben nincs terhelés
            });
        }
        context.Zones.AddRange(zones);
        context.SaveChanges(); // Mentés, hogy legyen ID-jük
        Console.WriteLine($"✅ {zones.Count} zóna létrehozva.");

        // --- FUTÁROK LÉTREHOZÁSA ---
        var couriers = new List<Courier>();
        string[] courierNames = { "János", "Péter", "Anna", "Kata", "Zoltán", "László", "Éva", "Gábor", "Réka", "Tamás" };

        for (int i = 0; i < numberOfCouriers; i++)
        {
            var startingZone = zones[random.Next(zones.Count)];
            couriers.Add(new Courier
            {
                Name = courierNames[i % courierNames.Length] + $" #{i + 1}",
                CurrentLocationX = startingZone.CenterX + random.Next(-10, 10), // Zóna közepétől kissé eltolva
                CurrentLocationY = startingZone.CenterY + random.Next(-10, 10),
                IsAvailable = true, // Kezdetben minden futár szabad
                CompletedDeliveries = 0,
                TotalDistanceTraveled = 0,
                TotalDelayMinutes = 0
            });
        }
        context.Couriers.AddRange(couriers);
        context.SaveChanges();
        Console.WriteLine($"✅ {couriers.Count} futár létrehozva.");

        // --- RENDELÉSEK LÉTREHOZÁSA ---
        var orders = new List<DeliveryOrder>();

        for (int i = 0; i < numberOfOrders; i++)
        {
            var targetZone = zones[random.Next(zones.Count)];

            // Célpont: zóna közelében (±15 egység a középponttól)
            double destX = targetZone.CenterX + random.Next(-15, 15);
            double destY = targetZone.CenterY + random.Next(-15, 15);

            // Deadline: 15-60 perc múlva
            var deadline = DateTime.Now.AddMinutes(random.Next(15, 60));

            orders.Add(new DeliveryOrder
            {
                DestinationAddress = $"{targetZone.Name}, {i + 1}. utca {random.Next(1, 100)}",
                DestX = destX,
                DestY = destY,
                CreatedAt = DateTime.Now,
                Deadline = deadline,
                ZoneId = targetZone.Id,
                Status = "Pending", // Kezdetben minden rendelés függőben
                WasDelayNotificationSent = false
            });

            // Zóna terhelésének növelése
            targetZone.CurrentLoad++;
        }
        context.DeliveryOrders.AddRange(orders);
        context.SaveChanges();
        Console.WriteLine($"✅ {orders.Count} rendelés létrehozva.");

        Console.WriteLine("🎉 Tesztadatok generálása kész!\n");
    }
}
