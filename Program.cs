// ============================================================
// Program.cs — Az alkalmazás belépési pontja
//
// FELELŐSSÉG: Setup + service-ek összerakása + szimuláció indítása.
// Üzleti logika NEM kerül ide — csak objektumok létrehozása és hívás.
// ============================================================

using Microsoft.Extensions.Logging;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Loaders;
using package_delivery_simulator_console_app.Infrastructure.Services;
using package_delivery_simulator_console_app.Services.Interfaces;
using package_delivery_simulator_console_app.Services.Assignment;
using package_delivery_simulator_console_app.Services.Notification;
using package_delivery_simulator_console_app.Services.Routing;
using package_delivery_simulator_console_app.Services.Simulation;

/// <summary>
/// Az alkalmazás fő osztálya.
/// A <see cref="Main"/> metódus a .NET belépési pontja.
/// </summary>
internal static class Program
{
    // ── Logger gyár — minden service ebből kap ILogger-t ────────
    private static ILoggerFactory _loggerFactory = null!;

    // ────────────────────────────────────────────────────────────
    // BELÉPÉSI PONT
    // ────────────────────────────────────────────────────────────

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

        // ── 2. SERVICE-EK + SZIMULÁCIÓ ───────────────────────────
        var orchestrator = BuildOrchestrator(cityGraph, warehouseService);

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

        // ── 3. ÖSSZESÍTŐ ─────────────────────────────────────────
        PrintSummary(result, couriers);

        _loggerFactory.Dispose();
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Setup
    // ────────────────────────────────────────────────────────────

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

    private static IWarehouseService BuildWarehouseService(ICityGraph cityGraph)
    {
        var service = new WarehouseService(
            cityGraph,
            _loggerFactory.CreateLogger<WarehouseService>());
        service.Initialize();
        return service;
    }

    private static async Task<List<package_delivery_simulator.Domain.Entities.Courier>>
        LoadCouriersAsync()
    {
        var loader = new CourierLoader(_loggerFactory.CreateLogger<CourierLoader>());
        return await loader.LoadAsync();
    }

    private static async Task<List<package_delivery_simulator.Domain.Entities.DeliveryOrder>>
        LoadOrdersAsync()
    {
        var loader = new OrderLoader(_loggerFactory.CreateLogger<OrderLoader>());
        return await loader.LoadAsync();
    }

    /// <summary>
    /// Az összes service összerakása és az orchestrator visszaadása.
    ///
    /// SORREND:
    ///   1. NotificationService  — független, semmit sem kap
    ///   2. AssignmentService    — csak cityGraph-ot kap
    ///   3. RouteService         — csak cityGraph-ot kap
    ///   4. SimulationService    — cityGraph + warehouseService + notificationService
    ///   5. Orchestrator         — assignmentService + simulationService + routeService
    /// </summary>
    private static SimulationOrchestrator BuildOrchestrator(
        ICityGraph cityGraph,
        IWarehouseService warehouseService)
    {
        var notificationService = new NotificationService(
            _loggerFactory.CreateLogger<NotificationService>());

        var assignmentService = new GreedyAssignmentService(
            cityGraph,
            _loggerFactory.CreateLogger<GreedyAssignmentService>());

        // Nearest Neighbor útvonal-optimalizáló
        var routeService = new NearestNeighborRouteService(
            cityGraph,
            _loggerFactory.CreateLogger<NearestNeighborRouteService>());

        var simulationService = new DeliverySimulationService(
            cityGraph,
            warehouseService,
            notificationService,
            _loggerFactory.CreateLogger<DeliverySimulationService>());

        return new SimulationOrchestrator(
            assignmentService,
            simulationService,
            routeService,
            _loggerFactory.CreateLogger<SimulationOrchestrator>());
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Kiírások
    // ────────────────────────────────────────────────────────────

    private static void PrintHeader()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║          CSOMAG KÉZBESÍTÉS SZIMULÁCIÓ                ║");
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
