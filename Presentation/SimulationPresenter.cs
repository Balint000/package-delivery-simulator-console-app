namespace package_delivery_simulator_console_app.Presentation;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator_console_app.Reporting;
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
///   - Összesítő, Reporting riportok kiírása
///
/// RIPORTOK (Reporting réteg):
///   A szimuláció végén három külön riport készül:
///   1. DelayReport          — késett rendelések részletesen
///   2. CourierPerformanceReport — futárok teljesítmény rangsor
///   3. ZoneLoadReport       — zónánkénti terhelés elemzés
///
///   Minden riport a saját osztályában él (Reporting/ mappa),
///   ez az osztály csak meghívja őket — nem tudja a részleteket.
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
    ///   7. Riportok kiírása (Reporting réteg)
    ///
    /// Ha Ctrl+C → megszakítás kezelése, renderer lezárása, korai return.
    /// </summary>
    public async Task RunAsync(
        SetupResult setup,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Renderer inicializálása ───────────────────────────
        _renderer.Initialize("CSOMAG KÉZBESÍTÉS SZIMULÁCIÓ", setup.Couriers.Count);

        // ── 2. Futárok kezdeti státusza ──────────────────────────
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
            _renderer.Complete();
            PrintCancelled();
            return;
        }

        // ── 5. Renderer lezárása ─────────────────────────────────
        // A kurzor a panel alá kerül — ide fognak kerülni a riportok.
        _renderer.Complete();

        // ── 6. Összesítő + Riportok kiírása ─────────────────────
        // SORREND:
        //   a) Rövid összesítő (néhány sor)
        //   b) DelayReport          — ki, mennyit késett
        //   c) CourierPerformanceReport — futár rangsor
        //   d) ZoneLoadReport       — zónánkénti terhelés
        //   e) Kilépési prompt
        PrintSummary(result);
        PrintReports(setup.Couriers, setup.Orders);
        PrintExitPrompt();
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Megjelenítés
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Megszakítás üzenete.
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
    /// Rövid összesítő a szimuláció fő számairól.
    /// Ez csak a "top-line" számokat mutatja — a részletek a riportokban vannak.
    /// </summary>
    private static void PrintSummary(OrchestratorResult result)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━ ÖSSZESÍTŐ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();

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
    }

    /// <summary>
    /// A három Reporting riport meghívása sorban.
    ///
    /// MIÉRT EBBEN A SORRENDBEN?
    ///   1. Késések → az ember először azt akarja tudni, mi ment rosszul
    ///   2. Futár teljesítmény → ki a felelős, ki teljesített jól
    ///   3. Zóna terhelés → strukturális elemzés a jövőre
    ///
    /// MIÉRT NEM ITT VAN A RIPORT LOGIKA?
    ///   Ez az osztály csak összefog — nem tudja a részleteket.
    ///   Ha a DelayReport formátuma változik, csak a DelayReport.cs-t módosítjuk.
    ///   Ez a Single Responsibility Principle (SRP) alkalmazása.
    /// </summary>
    private static void PrintReports(List<Courier> couriers, List<DeliveryOrder> orders)
    {
        // ── 1. Késési riport ─────────────────────────────────────
        // Megmutatja, melyek késtek, mennyit, és ki szállította.
        var delayReport = new DelayReport(orders, couriers);
        delayReport.Print();

        // ── 2. Futár teljesítmény rangsor ────────────────────────
        // Rangsorolja a futárokat: legtöbb kézbesítés, legkevesebb késés.
        var performanceReport = new CourierPerformanceReport(couriers);
        performanceReport.Print();

        // ── 3. Zónánkénti terhelés ────────────────────────────────
        // Megmutatja, melyik zóna volt legjobban terhelve.
        var zoneReport = new ZoneLoadReport(orders, couriers);
        zoneReport.Print();
    }

    /// <summary>
    /// Kilépési prompt — a riportok után jelenik meg.
    /// </summary>
    private static void PrintExitPrompt()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Nyomj meg egy billentyűt a kilépéshez...");
        Console.ResetColor();
        Console.ReadKey(intercept: true);
        Console.WriteLine();
    }
}
