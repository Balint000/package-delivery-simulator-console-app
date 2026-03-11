namespace package_delivery_simulator_console_app.Presentation;

using Microsoft.Extensions.Logging;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Loaders;
using package_delivery_simulator_console_app.Infrastructure.Services;
using package_delivery_simulator_console_app.Services.Interfaces;

/// <summary>
/// A szimuláció indítása előtti setup fázis megjelenítője.
///
/// FELELŐSSÉG:
///   - Boot képernyő kirajzolása
///   - Adatok betöltése (városgráf, raktárak, futárok, rendelések)
///   - Minden betöltési lépés visszajelzése a felhasználónak
///   - "Nyomj billentyűt az indításhoz" prompt kezelése
///   - SetupResult visszaadása a hívónak
///
/// MIÉRT KÜLÖN OSZTÁLY?
///   A Program.cs-ben semmi megjelenítés nem történik.
///   Ez az osztály felelős MINDEN konzol kimenetért a setup fázisban.
///
/// MEGJEGYZÉS AZ ILOGGER-EKRŐL:
///   Az ILogger példányok átmenetileg megmaradnak a service-ekben,
///   de LogLevel.None-ra vannak állítva — a konzolra nem írnak semmit.
///   Később (külön refaktorálási lépésben) el fognak kerülni.
/// </summary>
public class SetupPresenter
{
    // ── Függőségek ───────────────────────────────────────────────
    private readonly ILoggerFactory _loggerFactory;

    // ── Konstansok ───────────────────────────────────────────────
    private const int ConsoleWidth = 56;

    // ── Konstruktor ──────────────────────────────────────────────
    public SetupPresenter(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    // ────────────────────────────────────────────────────────────
    // FŐ METÓDUS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// A teljes setup fázis lefuttatása.
    ///
    /// LÉPÉSEK:
    ///   1. Boot képernyő kirajzolása
    ///   2. Városgráf betöltése
    ///   3. Raktárszolgáltatás inicializálása
    ///   4. Futárok betöltése
    ///   5. Rendelések betöltése
    ///   6. Összesítő sor kiírása
    ///   7. "Press key" prompt + várakozás
    ///
    /// Visszatérési érték: SetupResult ha sikeres, null ha hiba volt.
    /// </summary>
    public async Task<SetupResult?> RunAsync()
    {
        DrawBootScreen();

        // ── 1. Városgráf ─────────────────────────────────────────
        var cityGraph = LoadCityGraph();
        if (cityGraph == null) return null;

        // ── 2. Raktárszolgáltatás ────────────────────────────────
        var warehouseService = BuildWarehouseService(cityGraph);

        // ── 3. Futárok ───────────────────────────────────────────
        var couriers = await LoadCouriersAsync();

        // ── 4. Rendelések ────────────────────────────────────────
        var orders = await LoadOrdersAsync();

        // ── 5. Összesítő ─────────────────────────────────────────
        PrintSetupSummary(cityGraph, couriers.Count, orders.Count);

        // ── 6. Indítás prompt ────────────────────────────────────
        WaitForKeyPress();

        return new SetupResult(cityGraph, warehouseService, couriers, orders);
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Megjelenítés
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Statikus boot képernyő — csak egyszer rajzolódik ki,
    /// a betöltési lépések alatta jelennek meg.
    /// </summary>
    private static void DrawBootScreen()
    {
        Console.Clear();
        Console.CursorVisible = true;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║          CSOMAG KÉZBESÍTÉS SZIMULÁTORA               ║");
        Console.WriteLine("║                  — Setup fázis —                     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━ ADATOK BETÖLTÉSE ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();
    }

    /// <summary>
    /// Egy betöltési lépés eredményének kiírása.
    /// Sikeres → zöld pipa, hiba → piros X.
    /// </summary>
    private static void PrintLoadStep(string label, string? detail = null, bool success = true)
    {
        if (success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  ✔ ");
            Console.ResetColor();
            Console.Write($"{label,-30}");
            if (detail != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  {detail}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  ✘ ");
            Console.ResetColor();
            Console.Write($"{label,-30}");
            if (detail != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"  {detail}");
                Console.ResetColor();
            }
        }
        Console.WriteLine();
    }

    /// <summary>
    /// A setup végén: összefoglaló sor + elválasztó.
    /// </summary>
    private static void PrintSetupSummary(
        ICityGraph cityGraph,
        int courierCount,
        int orderCount)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();

        Console.Write("  ✅ Kész: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{cityGraph.Nodes.Count} csúcs");
        Console.ResetColor();
        Console.Write(" │ ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{courierCount} futár");
        Console.ResetColor();
        Console.Write(" │ ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{orderCount} rendelés");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
    }

    /// <summary>
    /// Billentyűnyomásra várakozás.
    /// intercept: true = a leütött billentyű nem jelenik meg a konzolon.
    /// </summary>
    private static void WaitForKeyPress()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Nyomj meg egy billentyűt a szimuláció indításához...");
        Console.ResetColor();

        Console.ReadKey(intercept: true);
        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Betöltés
    // ────────────────────────────────────────────────────────────

    private ICityGraph? LoadCityGraph()
    {
        try
        {
            var graph = CityGraphLoader.LoadFromJson("Data/city-graph.json");
            PrintLoadStep(
                "Városgráf",
                $"{graph.Nodes.Count} csúcs betöltve");
            return graph;
        }
        catch (Exception ex)
        {
            PrintLoadStep("Városgráf", ex.Message, success: false);
            return null;
        }
    }

    private IWarehouseService BuildWarehouseService(ICityGraph cityGraph)
    {
        var service = new WarehouseService(
            cityGraph,
            _loggerFactory.CreateLogger<WarehouseService>());
        service.Initialize();

        PrintLoadStep(
            "Raktárszolgáltatás",
            "inicializálva");

        return service;
    }

    private async Task<List<package_delivery_simulator.Domain.Entities.Courier>>
        LoadCouriersAsync()
    {
        var loader = new CourierLoader(_loggerFactory.CreateLogger<CourierLoader>());
        var couriers = await loader.LoadAsync();
        PrintLoadStep("Futárok", $"{couriers.Count} futár betöltve");
        return couriers;
    }

    private async Task<List<package_delivery_simulator.Domain.Entities.DeliveryOrder>>
        LoadOrdersAsync()
    {
        var loader = new OrderLoader(_loggerFactory.CreateLogger<OrderLoader>());
        var orders = await loader.LoadAsync();
        PrintLoadStep("Rendelések", $"{orders.Count} rendelés betöltve");
        return orders;
    }
}
