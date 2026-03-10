namespace package_delivery_simulator_console_app.Presentation;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator_console_app.Services.Interfaces;
using package_delivery_simulator_console_app.Services.Simulation;

/// <summary>
/// A szimuláció teljes megjelenítési rétege.
///
/// FELELŐSSÉG:
///   - LiveConsoleRenderer inicializálása
///   - Futárok kezdeti "waiting" státuszba állítása
///   - Szimuláció futtatása (orchestratoron keresztül)
///   - Ctrl+C / megszakítás kezelése
///   - Renderer lezárása (Complete)
///   - Összesítő és futár-teljesítmény kiírása
///
/// MIÉRT KÜLÖN OSZTÁLY?
///   A Program.cs-ben semmi megjelenítés nem történik.
///   Ez az osztály felelős MINDEN konzol kimenetért a szimuláció fázisban.
///
/// ÖSSZEFÜGGÉS A TÖBBI OSZTÁLLYAL:
///   SetupPresenter → SetupResult → SimulationPresenter
///   A SetupPresenter elkészíti az adatokat, ezt az osztályt
///   a Program.cs hívja meg a SetupResult-tal és az orchestratorral.
/// </summary>
public class SimulationPresenter
{
    // ── Függőségek ───────────────────────────────────────────────
    private readonly SimulationOrchestrator _orchestrator;
    private readonly LiveConsoleRenderer _renderer;

    // ── Konstruktor ──────────────────────────────────────────────
    public SimulationPresenter(
        SimulationOrchestrator orchestrator,
        LiveConsoleRenderer renderer)
    {
        _orchestrator = orchestrator;
        _renderer = renderer;
    }

    // ────────────────────────────────────────────────────────────
    // FŐ METÓDUS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// A teljes szimuláció fázis lefuttatása.
    ///
    /// LÉPÉSEK:
    ///   1. Renderer inicializálása (képernyő törlés, panelek)
    ///   2. Futárok "waiting" státuszba állítása
    ///   3. "Szimuláció elindult" esemény logolása
    ///   4. Orchestrator futtatása (a tényleges szimuláció)
    ///   5. Renderer lezárása
    ///   6. Összesítő kiírása
    ///
    /// Ha Ctrl+C → megszakítás kezelése, renderer lezárása, korai return.
    /// </summary>
    public async Task RunAsync(
        SetupResult setup,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Renderer inicializálása ───────────────────────────
        // Ettől a ponttól az élő UI veszi át az irányítást.
        // Console.Write()-ot közvetlenül NEM szabad hívni —
        // csak a renderer metódusain keresztül szabad megjeleníteni bármit.
        _renderer.Initialize("CSOMAG KÉZBESÍTÉS SZIMULÁCIÓ", setup.Couriers.Count);

        // ── 2. Futárok kezdeti státusza ──────────────────────────
        // Minden futár "várakozik" státuszban jelenik meg indulás előtt
        foreach (var courier in setup.Couriers)
        {
            var startNode = setup.CityGraph.GetNode(courier.CurrentNodeId);
            _renderer.UpdateCourierStatus(
                courierId: courier.Id,
                courierName: courier.Name,
                status: "waiting",
                currentLocation: startNode?.Name ?? "?",
                completedDeliveries: 0);
        }

        // ── 3. Indulás jelzése ───────────────────────────────────
        _renderer.LogEvent("start", "Szimuláció elindult");

        // ── 4. Szimuláció futtatása ──────────────────────────────
        OrchestratorResult result;
        try
        {
            result = await _orchestrator.RunAsync(
                setup.Couriers,
                setup.Orders,
                cancellationToken);

            _renderer.LogEvent(
                "done",
                $"Kész! {result.Delivered}/{result.TotalOrders} kézbesítve");
        }
        catch (OperationCanceledException)
        {
            // ── Megszakítás kezelése ─────────────────────────────
            // Renderer lezárása, hogy a kurzor a panel alá kerüljön,
            // és a megszakítás üzenete ne csússzon bele a panelbe.
            _renderer.Complete();
            PrintCancelled();
            return;
        }

        // ── 5. Renderer lezárása ─────────────────────────────────
        // Kurzor a panel alá kerül, hogy az összesítő ne csússzon
        // bele az élő UI paneljeibe.
        _renderer.Complete();

        // ── 6. Összesítő kiírása ─────────────────────────────────
        PrintSummary(result, setup.Couriers);
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Megjelenítés
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Megszakítás üzenete — a renderer Complete() után hívandó,
    /// hogy a kurzor már a panel alatt legyen.
    /// </summary>
    private static void PrintCancelled()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ⚠️  Szimuláció megszakítva.");
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>
    /// Szimuláció utáni összesítő — statisztikák és futár teljesítmény.
    ///
    /// A renderer Complete() után hívandó.
    /// </summary>
    private static void PrintSummary(
        OrchestratorResult result,
        List<Courier> couriers)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━ ÖSSZESÍTŐ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();

        // Általános statisztikák
        Console.WriteLine($"   📦 Összes rendelés:      {result.TotalOrders}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"   ✅ Sikeresen kézbesítve: {result.Delivered,3}  ({result.SuccessRate:P0})");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"   ⚠️  Késve kézbesítve:    {result.Delayed,3}  ({result.DelayRate:P0})");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"   ❌ Sikertelen:           {result.Failed,3}");
        Console.WriteLine($"   📭 Sosem kiosztva:       {result.Unassigned,3}");
        Console.ResetColor();

        Console.WriteLine($"   ⏱️  Teljes futásidő:      {result.WallClockTime.TotalSeconds:F1}s");

        // Futár teljesítmény táblázat
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━ FUTÁR TELJESÍTMÉNY ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();

        foreach (var courier in couriers.Where(c => c.TotalDeliveriesCompleted > 0))
        {
            Console.Write($"   👤 {courier.Name,-20} │ ");
            Console.Write($"Kézb.: {courier.TotalDeliveriesCompleted,2} │ ");

            // Késések piros színnel kiemelve, ha van
            if (courier.TotalDelayedDeliveries > 0)
                Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"Késés: {courier.TotalDelayedDeliveries,2}");
            Console.ResetColor();

            Console.WriteLine($" │ Átlag: {courier.AverageDeliveryTime:F1} perc");
        }

        // Kilépés prompt
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Nyomj meg egy billentyűt a kilépéshez...");
        Console.ResetColor();
        Console.ReadKey(intercept: true);
        Console.WriteLine();
    }
}
