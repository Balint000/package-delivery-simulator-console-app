using PackageDelivery.Data;
using PackageDelivery.Services;

Console.WriteLine("🚚 === CSOMAGKÉZBESÍTÉS SZIMULÁCIÓ ===\n");

// Adatbázis kontextus létrehozása
using var context = new DeliveryDBContext();

// Adatbázis biztosítása
context.Database.EnsureCreated();

// Ellenőrzés: van-e már adat?
if (context.Zones.Any())
{
    Console.Write("⚠️  Az adatbázis már tartalmaz adatokat. Töröljem és újra generáljam? (i/n): ");
    var response = Console.ReadLine()?.ToLower();

    if (response == "i")
    {
        Console.WriteLine("🗑️  Adatbázis törlése...");
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        Console.WriteLine("✅ Adatbázis törölve és újraépítve.\n");
    }
    else
    {
        Console.WriteLine("ℹ️  Meglévő adatok használata.\n");
    }
}

// 1. Tesztadatok generálása
SeedData.Initialize(context, numberOfZones: 5, numberOfCouriers: 8, numberOfOrders: 20);

// 2. Assignment: Greedy algoritmussal futárok hozzárendelése
var assignmentService = new AssignmentService(context);
assignmentService.AssignAllPendingOrders();

// 3. Routing: Nearest Neighbor útvonal-optimalizálás
var routingService = new RoutingService(context);
routingService.OptimizeAllRoutes();

// 4. Szimuláció: TPL párhuzamos futtatás
Console.WriteLine("Nyomj ENTER-t a szimuláció indításához...");
Console.ReadLine();

var simulationEngine = new SimulationEngine(context);
await simulationEngine.RunSimulationAsync();

Console.WriteLine("\n✅ Program vége! Nyomj ENTER-t a kilépéshez...");
Console.ReadLine();
