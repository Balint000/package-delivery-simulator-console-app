// ============================================================
// Program.cs
// ============================================================
// A program belépési pontja — setup + orchestrator hívás.
//
// ARCHITEKTÚRA (manuális bekötés, DI nélkül):
//
//   Program.cs
//     │
//     ├── CityGraphLoader      → városgráf betöltése (JSON)
//     ├── WarehouseService     → raktárak azonosítása a gráfban
//     ├── CourierLoader        → futárok betöltése (JSON)
//     ├── OrderLoader          → rendelések betöltése (JSON)
//     └── SimulationOrchestrator.RunAsync()
//               ├─ GreedyAssignmentService.AssignAll()  [initial batch]
//               ├─ ConcurrentQueue<DeliveryOrder>        [maradék rendelések]
//               └─ RunCourierLoopAsync() × futárok
//                     └─ DeliverySimulationService.SimulateDeliveryAsync()
//
// VÁLTOZÁS A KORÁBBI VERZIÓHOZ KÉPEST:
//   RÉGEN: Program.cs vezérelte a foreach-et, szimuláció és hozzárendelés
//          keveredett itt.
//   MOST:  Program.cs csak setup. A teljes logika a SimulationOrchestrator-ban van.
//
// KÖVETKEZŐ LÉPÉS (TPL):
//   SimulationOrchestrator.RunAsync()-ban a szekvenciális foreach
//   Task.WhenAll-ra cserélhető — Program.cs nem változik.
// ============================================================

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Loaders;
using package_delivery_simulator_console_app.Infrastructure.Services;
using package_delivery_simulator_console_app.Services.Assignment;
using package_delivery_simulator_console_app.Services.Simulation;
using package_delivery_simulator_console_app.Presentation.Interfaces;

// ============================================================
// LOGGER GYÁR
// ============================================================

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "[HH:mm:ss] ";
    });
    builder.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// FEJLÉC
// ============================================================

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║       🚚 CSOMAG KÉZBESÍTÉS SZIMULÁCIÓ               ║");
Console.WriteLine("║          Demo City — Queue-alapú szimuláció          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

// ============================================================
// SETUP — adatok betöltése és service-ek inicializálása
// ============================================================

Console.WriteLine("━━━ SETUP ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// 1. Városgráf
ICityGraph cityGraph;
try
{
    cityGraph = CityGraphLoader.LoadFromJson("Data/city-graph.json");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ Hiba a városgráf betöltésekor: {ex.Message}");
    Console.ResetColor();
    return;
}

// 2. Warehouse service
IWarehouseService warehouseService = new WarehouseService(
    cityGraph,
    loggerFactory.CreateLogger<WarehouseService>());
warehouseService.Initialize();

// 3. Futárok betöltése
var courierLoader = new CourierLoader(loggerFactory.CreateLogger<CourierLoader>());
var couriers = await courierLoader.LoadAsync();

// 4. Rendelések betöltése
var orderLoader = new OrderLoader(loggerFactory.CreateLogger<OrderLoader>());
var orders = await orderLoader.LoadAsync();

Console.WriteLine($"\n✅ Setup kész: {cityGraph.Nodes.Count} csúcs | " +
                  $"{couriers.Count} futár | {orders.Count} rendelés");
Console.WriteLine();

// ============================================================
// SERVICE-EK ÖSSZERAKÁSA (manuális DI)
// ============================================================

var assignmentService = new GreedyAssignmentService(
    cityGraph,
    loggerFactory.CreateLogger<GreedyAssignmentService>());

var simulationService = new DeliverySimulationService(
    cityGraph,
    warehouseService,
    loggerFactory.CreateLogger<DeliverySimulationService>());

var orchestrator = new SimulationOrchestrator(
    assignmentService,
    simulationService,
    loggerFactory.CreateLogger<SimulationOrchestrator>());

// ============================================================
// SZIMULÁCIÓ INDÍTÁSA
// ============================================================

Console.WriteLine("━━━ SZIMULÁCIÓ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("(Nyomj meg egy billentyűt az indításhoz...)");
Console.ReadKey();
Console.WriteLine();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n⚠️  Megszakítás kérve... leállítás folyamatban.");
};

OrchestratorResult result;
try
{
    result = await orchestrator.RunAsync(couriers, orders, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n⚠️  Szimuláció megszakítva.");
    return;
}

// ============================================================
// ÖSSZESÍTŐ
// ============================================================

Console.WriteLine();
Console.WriteLine("━━━ ÖSSZESÍTŐ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"   📦 Összes rendelés:      {result.TotalOrders}");
Console.WriteLine($"   ✅ Sikeresen kézbesítve: {result.Delivered}  " +
                  $"({result.SuccessRate:P0})");

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"   ⚠️  Késve kézbesítve:    {result.Delayed}  " +
                  $"({result.DelayRate:P0} a kézbesítettekből)");
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"   ❌ Sikertelen:           {result.Failed}");
Console.WriteLine($"   📭 Sosem kiosztva:       {result.Unassigned}");
Console.ResetColor();

Console.WriteLine($"   ⏱️  Teljes futásidő:      {result.WallClockTime.TotalSeconds:F1}s");

// Futár teljesítmény
Console.WriteLine();
Console.WriteLine("━━━ FUTÁR TELJESÍTMÉNY ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
foreach (var courier in couriers.Where(c => c.TotalDeliveriesCompleted > 0))
{
    Console.WriteLine(
        $"   👤 {courier.Name,-20} | " +
        $"Kézb.: {courier.TotalDeliveriesCompleted,2} | " +
        $"Késés: {courier.TotalDelayedDeliveries,2} | " +
        $"Átlag: {courier.AverageDeliveryTime:F1} perc");
}

Console.WriteLine();
Console.WriteLine("Nyomj meg egy billentyűt a kilépéshez...");
Console.ReadKey();
