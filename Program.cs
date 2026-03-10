// ============================================================
// Program.cs — Az alkalmazás belépési pontja
//
// FELELŐSSÉG: Service-ek összerakása (wiring) + presenterek hívása.
//
// AMI IDE NEM KERÜL:
//   - Console.Write / Console.WriteLine / Console.ReadKey → SetupPresenter / SimulationPresenter
//   - Üzleti logika                                       → Service-ek
//   - Adatbetöltés logikája                               → SetupPresenter
//   - Megjelenítési logika                                → Presentation layer
//
// FLOW:
//   SetupPresenter.RunAsync()
//     → betölti az adatokat, megjeleni a boot képernyőt, visszaadja a SetupResult-ot
//   BuildOrchestrator()
//     → összerakja a service-eket (pure wiring, semmi megjelenítés)
//   SimulationPresenter.RunAsync()
//     → futtatja a szimulációt, kezeli a megjelenítést, kiírja az összesítőt
// ============================================================

using Microsoft.Extensions.Logging;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Presentation;
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
    //
    // MEGJEGYZÉS: Az ILogger-ek átmenetileg megmaradnak a service-ekben,
    // de LogLevel.None-ra vannak állítva — a konzolra nem írnak semmit.
    // A megjelenítést kizárólag a Presentation layer végzi.
    // Később (külön refaktorálási lépésben) az ILogger hívások
    // el fognak kerülni a service-ekből.
    private static ILoggerFactory _loggerFactory = null!;

    // ────────────────────────────────────────────────────────────
    // BELÉPÉSI PONT
    // ────────────────────────────────────────────────────────────

    private static async Task Main()
    {
        // LogLevel.None: az ILogger-ek némák — semmi nem kerül a konzolra tőlük.
        // A Presentation layer (SetupPresenter, SimulationPresenter) végez
        // minden megjelenítést.
        _loggerFactory = BuildLoggerFactory(LogLevel.None);

        // ── 1. SETUP FÁZIS ───────────────────────────────────────
        // Boot képernyő + adatbetöltés a Presentation layerben.
        var setupPresenter = new SetupPresenter(_loggerFactory);
        var setup = await setupPresenter.RunAsync();

        // Ha a betöltés sikertelen volt (pl. hiányzó fájl),
        // a SetupPresenter már megjelentette a hibát — kilépünk.
        if (setup == null) return;

        // ── 2. SERVICE WIRING ────────────────────────────────────
        // Pure wiring: objektumok létrehozása és összekapcsolása.
        // Semmi megjelenítés nem történik itt.
        var renderer = new LiveConsoleRenderer();
        var orchestrator = BuildOrchestrator(setup.CityGraph, setup.WarehouseService, renderer);

        // ── 3. SZIMULÁCIÓ FÁZIS ──────────────────────────────────
        // Az élő UI megjelenítése, a szimuláció futtatása
        // és az összesítő kiírása a Presentation layerben.
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // Cancel jelzése, de a folyamat ne lépjen ki azonnal
            // — a SimulationPresenter kezeli a renderer lezárását
            e.Cancel = true;
            cts.Cancel();
        };

        var simulationPresenter = new SimulationPresenter(orchestrator, renderer);
        await simulationPresenter.RunAsync(setup, cts.Token);

        _loggerFactory.Dispose();
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Service wiring
    // ────────────────────────────────────────────────────────────

    private static ILoggerFactory BuildLoggerFactory(LogLevel level) =>
        LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            builder.SetMinimumLevel(level);
        });

    /// <summary>
    /// Az összes service összerakása és az orchestrator visszaadása.
    ///
    /// SORREND:
    ///   1. NotificationService  — független, semmit sem kap
    ///   2. AssignmentService    — csak cityGraph-ot kap
    ///   3. RouteService         — csak cityGraph-ot kap
    ///   4. SimulationService    — cityGraph + warehouseService + notificationService + renderer
    ///   5. Orchestrator         — assignmentService + simulationService + routeService
    /// </summary>
    private static SimulationOrchestrator BuildOrchestrator(
        ICityGraph cityGraph,
        IWarehouseService warehouseService,
        LiveConsoleRenderer renderer)
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
            _loggerFactory.CreateLogger<DeliverySimulationService>(),
            renderer);   // ← renderer átadása

        return new SimulationOrchestrator(
            assignmentService,
            simulationService,
            routeService,
            _loggerFactory.CreateLogger<SimulationOrchestrator>());
    }
}
