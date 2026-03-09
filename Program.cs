// ============================================================
// Program.cs — Az alkalmazás belépési pontja
//
// MIÉRT OSZTÁLY?
//   Az OOP elvnek megfelelően az alkalmazás maga is egy objektum.
//   A static Main() a .NET konvencionális belépési pontja,
//   a tényleges logika a privát metódusokban él.
//
// FELELŐSSÉG: Setup + service-ek összerakása + szimuláció indítása.
// Üzleti logika NEM kerül ide — csak objektumok létrehozása és hívás.
//
// FÜGGŐSÉGI LÁNC:
//   Program
//    ├─ CityGraphLoader        → ICityGraph
//    ├─ WarehouseService       → IWarehouseService
//    ├─ CourierLoader
//    ├─ OrderLoader
//    ├─ ConsoleNotificationService → INotificationService
//    ├─ GreedyAssignmentService
//    ├─ DeliverySimulationService
//    └─ SimulationOrchestrator
// ============================================================

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Loaders;
using package_delivery_simulator_console_app.Infrastructure.Services;
using package_delivery_simulator_console_app.Presentation.Interfaces;
using package_delivery_simulator_console_app.Services.Assignment;
using package_delivery_simulator_console_app.Services.Notification;
using package_delivery_simulator_console_app.Services.Simulation;
using package_delivery_simulator_console_app.Services.Interfaces;

/// <summary>
/// Az alkalmazás fő osztálya.
/// A <see cref="Main"/> metódus a .NET belépési pontja.
/// </summary>
internal static class Program
{
    // ── Logger gyár — egy helyen van, minden service ebből kap loggert ──
    private static ILoggerFactory _loggerFactory = null!;

    // ────────────────────────────────────────────────────────────
    // BELÉPÉSI PONT
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// .NET belépési pont. Felépíti a service-eket és elindítja a szimulációt.
    /// </summary>
    private static async Task Main()
    {
        _loggerFactory = BuildLoggerFactory();

        PrintHeader();

        // ── 1. SETUP ─────────────────────────────────────────────
        Console.WriteLine("━━━ SETUP ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var cityGraph = LoadCityGraph();
        if (cityGraph == null) return;

        var warehouseService = BuildWarehouseService(cityGraph);
        var couriers = await LoadCouriersAsync();
        var orders = await LoadOrdersAsync();

        Console.WriteLine(
            $"\n✅ Setup kész: {cityGraph.Nodes.Count} csúcs | " +
            $"{couriers.Count} futár | {orders.Count} rendelés\n");

        // ── 2. SERVICE-EK ÖSSZERAKÁSA ────────────────────────────
        var orchestrator = BuildOrchestrator(cityGraph, warehouseService);

        // ── 3. SZIMULÁCIÓ INDÍTÁSA ───────────────────────────────
        Console.WriteLine("━━━ SZIMULÁCIÓ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("(Nyomj meg egy billentyűt az indításhoz...)");
        Console.ReadKey();
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n⚠️  Megszakítás kérve...");
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

        // ── 4. ÖSSZESÍTŐ ─────────────────────────────────────────
        PrintSummary(result, couriers);

        _loggerFactory.Dispose();
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Setup lépések
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Logger gyár létrehozása. Minden service ebből kap ILogger-t.
    /// </summary>
    private static ILoggerFactory BuildLoggerFactory() =>
        LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

    /// <summary>
    /// Városgráf betöltése JSON-ból. Hiba esetén null-t ad vissza.
    /// </summary>
    private static ICityGraph? LoadCityGraph()
    {
        try
        {
            return CityGraphLoader.LoadFromJson("Data/city-graph.json");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Hiba a városgráf betöltésekor: {ex.Message}");
            Console.ResetColor();
            return null;
        }
    }

    /// <summary>
    /// WarehouseService létrehozása és inicializálása.
    /// Az Initialize() megkeresi a gráfban az összes Warehouse node-ot.
    /// </summary>
    private static IWarehouseService BuildWarehouseService(ICityGraph cityGraph)
    {
        var service = new WarehouseService(
            cityGraph,
            _loggerFactory.CreateLogger<WarehouseService>());
        service.Initialize();
        return service;
    }

    /// <summary>
    /// Futárok betöltése JSON fájlból.
    /// </summary>
    private static async Task<List<package_delivery_simulator.Domain.Entities.Courier>>
        LoadCouriersAsync()
    {
        var loader = new CourierLoader(_loggerFactory.CreateLogger<CourierLoader>());
        return await loader.LoadAsync();
    }

    /// <summary>
    /// Rendelések betöltése JSON fájlból.
    /// </summary>
    private static async Task<List<package_delivery_simulator.Domain.Entities.DeliveryOrder>>
        LoadOrdersAsync()
    {
        var loader = new OrderLoader(_loggerFactory.CreateLogger<OrderLoader>());
        return await loader.LoadAsync();
    }

    /// <summary>
    /// Az összes service összerakása és az orchestrator visszaadása.
    /// </summary>
    private static SimulationOrchestrator BuildOrchestrator(
        ICityGraph cityGraph,
        IWarehouseService warehouseService)
    {
        // Értesítési service — késés esetén konzolra ír
        var notificationService = new NotificationService(
            _loggerFactory.CreateLogger<NotificationService>());

        // Greedy hozzárendelő — legközelebbi szabad futárt találja meg
        var assignmentService = new GreedyAssignmentService(
            cityGraph,
            _loggerFactory.CreateLogger<GreedyAssignmentService>());

        // Kézbesítési szimuláció — egy futár + egy rendelés teljes útja
        var simulationService = new DeliverySimulationService(
            cityGraph,
            warehouseService,
            notificationService,
            _loggerFactory.CreateLogger<DeliverySimulationService>());

        // Orchestrator — vezényli az egész szimulációt
        return new SimulationOrchestrator(
            assignmentService,
            simulationService,
            _loggerFactory.CreateLogger<SimulationOrchestrator>());
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Konzol kiírások
    // ────────────────────────────────────────────────────────────

    private static void PrintHeader()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║       🚚 CSOMAG KÉZBESÍTÉS SZIMULÁCIÓ               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static void PrintSummary(
        OrchestratorResult result,
        List<package_delivery_simulator.Domain.Entities.Courier> couriers)
    {
        Console.WriteLine();
        Console.WriteLine("━━━ ÖSSZESÍTŐ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"   📦 Összes rendelés:      {result.TotalOrders}");
        Console.WriteLine($"   ✅ Sikeresen kézbesítve: {result.Delivered}  ({result.SuccessRate:P0})");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"   ⚠️  Késve kézbesítve:    {result.Delayed}  ({result.DelayRate:P0})");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"   ❌ Sikertelen:           {result.Failed}");
        Console.WriteLine($"   📭 Sosem kiosztva:       {result.Unassigned}");
        Console.ResetColor();

        Console.WriteLine($"   ⏱️  Teljes futásidő:      {result.WallClockTime.TotalSeconds:F1}s");

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
    }
}
