using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator.Services.Delivery;
using package_delivery_simulator.Presentation.Console;

// UTF-8 encoding a magyar karakterekhez (ékezetek)
System.Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("🚀 Csomagkézbesítés Szimuláció - TPL Edition");
Console.WriteLine("============================================\n");

// ===== 1. VÁROS/GRÁF BETÖLTÉSE =====
// TODO: A te gráf betöltő kódod ide!
// Pl: var cityGraph = CityGraphLoader.LoadFromJson("Data/city-graph.json");
// ÁTMENETI: null objektum (később cseréld ki!)
object cityGraph = new object();

Console.WriteLine("✅ Város gráf betöltve!");

// ===== 2. DELIVERY SERVICE LÉTREHOZÁSA =====
var deliveryService = new DeliveryService(cityGraph);

// ===== 3. FUTÁROK HOZZÁADÁSA =====
deliveryService.AddCourier(new Courier
{
    Id = 1,
    Name = "Kovács Péter",
    CurrentNodeId = 0 // Raktár
});

deliveryService.AddCourier(new Courier
{
    Id = 2,
    Name = "Nagy Anna",
    CurrentNodeId = 0
});

deliveryService.AddCourier(new Courier
{
    Id = 3,
    Name = "Szabó Gábor",
    CurrentNodeId = 0
});

Console.WriteLine("✅ 3 futár hozzáadva!");

// ===== 4. RENDELÉSEK GENERÁLÁSA =====
var random = new Random();
int orderCount = 10;

for (int i = 1; i <= orderCount; i++)
{
    deliveryService.AddOrder(new DeliveryOrder
    {
        Id = i,
        OrderNumber = $"ORD-{i:D5}",
        CustomerName = $"Ügyfél #{i}",
        AddressText = $"Cím {i}",
        AddressLocation = new Location(random.Next(1, 100), random.Next(1, 100)),
        ZoneId = random.Next(1, 4),
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.Now,
        ExpectedDeliveryTime = DateTime.Now.AddMinutes(15) // 15 perc a cél
    });
}

Console.WriteLine($"✅ {orderCount} rendelés generálva!\n");

// ===== 5. ÉLŐJE UI LÉTREHOZÁSA =====
var ui = new LiveConsoleUI();

// ===== 6. CANCELLATION TOKEN (CTRL+C kezelés) =====
var cancellationTokenSource = new CancellationTokenSource();

// CTRL+C esemény figyelése
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true; // Nem lép ki azonnal
    Console.WriteLine("\n\n⏹️  Leállítás folyamatban...");
    cancellationTokenSource.Cancel();
};

// ===== 7. INDÍTÁS =====
Console.WriteLine("🚀 Nyomj ENTER-t a szimuláció indításához...");
Console.ReadLine();

// ===== 8. UI INICIALIZÁLÁS =====
ui.Initialize();

// ===== 9. SZIMULÁCIÓ ÉS UI FRISSÍTÉS PÁRHUZAMOSAN =====

// Szimuláció Task (párhuzamos futárok)
var simulationTask = Task.Run(async () =>
{
    await deliveryService.RunSimulationAsync(cancellationTokenSource.Token);
}, cancellationTokenSource.Token);

// UI frissítő Task (500ms-enként frissít)
var uiTask = Task.Run(async () =>
{
    while (!cancellationTokenSource.Token.IsCancellationRequested)
    {
        // Adatok lekérése a service-ből
        var couriers = deliveryService.GetCouriers();
        var orders = deliveryService.GetOrders();
        var (totalDeliveries, totalDelays) = deliveryService.GetStatistics();

        // Statisztikák objektum
        var stats = new SimulationStats
        {
            TotalDeliveries = totalDeliveries,
            TotalDelays = totalDelays
        };

        // UI frissítés (500ms-enként)
        ui.Update(couriers, orders, stats);

        // Várakozás
        await Task.Delay(500, cancellationTokenSource.Token);
    }
}, cancellationTokenSource.Token);

// ===== 10. VÁRUNK A BEFEJEZÉSRE =====
try
{
    // Mindkét Task-ra várunk (szimuláció ÉS UI)
    await Task.WhenAll(simulationTask, uiTask);
}
catch (OperationCanceledException)
{
    // Normális leállítás (CTRL+C)
}

// ===== 11. CLEANUP =====
ui.Cleanup();

// ===== 12. VÉGSŐ STATISZTIKA =====
Console.Clear();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    ✅ SZIMULÁCIÓ BEFEJEZVE                            ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝\n");
Console.ResetColor();

var (finalDeliveries, finalDelays) = deliveryService.GetStatistics();

Console.WriteLine("📊 VÉGSŐ STATISZTIKÁK:");
Console.WriteLine("───────────────────────────────────────────────────────────────────────");
Console.WriteLine($"Összes kézbesítés:  {finalDeliveries}");
Console.WriteLine($"Késések száma:      {finalDelays}");

if (finalDeliveries > 0)
{
    double delayRate = (double)finalDelays / finalDeliveries * 100;
    Console.WriteLine($"Késési arány:       {delayRate:F1}%");
}

Console.WriteLine("\n👥 FUTÁR TELJESÍTMÉNYEK:");
Console.WriteLine("───────────────────────────────────────────────────────────────────────");

foreach (var courier in deliveryService.GetCouriers().OrderByDescending(c => c.TotalDeliveries))
{
    Console.WriteLine($"{courier.Name,-20}: {courier.TotalDeliveries,3} kézbesítés");
}

Console.WriteLine("\n🎉 Nyomj meg egy billentyűt a kilépéshez...");
Console.ReadKey();
