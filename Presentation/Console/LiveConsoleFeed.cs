using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.Interfaces;
using System.Text;

namespace package_delivery_simulator.Presentation.Console;

/// <summary>
/// ÉLŐJE KONZOL MEGJELENÍTŐ.
///
/// Működési elv:
/// 1. Console.Clear() egyszer az elején
/// 2. Console.SetCursorPosition() visszaállítja a kurzort
/// 3. Felülírjuk a régi sorokat -> NEM VILLOG!
///
/// Thread-safe: lock objektummal védjük a Console írást,
/// hogy több Task ne írjon egyszerre.
/// </summary>
public class LiveConsoleUI : ILiveConsoleUI
{
    // Lock object a thread-safe kiíráshoz
    private readonly object _consoleLock = new object();

    // UI layout méretek
    private int _dynamicAreaStartY = 0;
    private bool _isInitialized = false;

    /// <summary>
    /// UI INICIALIZÁLÁS - fix header kirajzolása.
    /// Ezt csak egyszer kell meghívni induláskor!
    /// </summary>
    public void Initialize()
    {
        lock (_consoleLock)
        {
            // Teljes képernyő törlése
            System.Console.Clear();

            // Villogó kurzor elrejtése (szebb UI)
            System.Console.CursorVisible = false;

            // Fix header rajzolása
            DrawHeader();

            // Mentjük, hol kezdődik a dinamikus terület
            _dynamicAreaStartY = System.Console.CursorTop;
            _isInitialized = true;
        }
    }

    /// <summary>
    /// TELJES UI FRISSÍTÉSE - futárok, rendelések, statisztikák.
    /// Ezt folyamatosan hívjuk (pl. 500ms-enként).
    ///
    /// KULCS TRÜKK: Console.SetCursorPosition() visszaállítja a kurzort
    /// a dinamikus terület elejére, így felülírjuk a régi sorokat!
    /// </summary>
    public void Update(
        IEnumerable<Courier> couriers,
        IEnumerable<DeliveryOrder> orders,
        SimulationStats stats)
    {
        if (!_isInitialized)
            return;

        lock (_consoleLock)
        {
            // Kurzor visszaállítása a dinamikus terület elejére
            // Ez a TRÜKK! Nem töröljük a képernyőt, csak felülírjuk!
            System.Console.SetCursorPosition(0, _dynamicAreaStartY);

            // Futárok kirajzolása
            DrawCouriers(couriers);

            // Rendelések összegzés
            DrawOrdersSummary(orders);

            // Statisztikák
            DrawStatistics(stats);

            // Footer
            DrawFooter();
        }
    }

    /// <summary>
    /// CLEANUP - kurzor visszaállítás, színek reset.
    /// Ezt hívjuk meg a program végén.
    /// </summary>
    public void Cleanup()
    {
        lock (_consoleLock)
        {
            System.Console.CursorVisible = true;
            System.Console.ResetColor();
            System.Console.WriteLine();
        }
    }

    // ==================== PRIVÁT RAJZOLÓ METÓDUSOK ====================

    /// <summary>
    /// Fix header rajzolása (egyszer, induláskor).
    /// </summary>
    private void DrawHeader()
    {
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
        System.Console.WriteLine("║         🚚 ÉLŐJE CSOMAGKÉZBESÍTÉS SZIMULÁCIÓ - TPL Edition 🚚         ║");
        System.Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
        System.Console.ResetColor();
        System.Console.WriteLine();
    }

    /// <summary>
    /// Futárok megjelenítése táblázat formában.
    /// </summary>
    private void DrawCouriers(IEnumerable<Courier> couriers)
    {
        WriteSectionHeader("👥 FUTÁROK");

        // Fejléc
        System.Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Console.WriteLine("  ID │ Név              │ Státusz    │ Kézbesítve │ Pozíció       ");
        System.Console.WriteLine("─────┼──────────────────┼────────────┼────────────┼────────────────");
        System.Console.ResetColor();

        // Futárok listája
        foreach (var courier in couriers.OrderBy(c => c.Id))
        {
            DrawCourierRow(courier);
        }

        System.Console.WriteLine(); // Üres sor
    }

    /// <summary>
    /// Egy futár sora (színes státusz).
    /// </summary>
    private void DrawCourierRow(Courier courier)
    {
        // Sor építése StringBuilder-rel (hatékonyság)
        var row = new StringBuilder();
        row.Append($"  {courier.Id,2} │ ");
        row.Append($"{courier.Name,-16} │ ");

        // Státusz színezés
        System.Console.Write(row.ToString());
        DrawCourierStatus(courier.Status);

        // Kézbesített csomagok
        System.Console.Write($" │ {courier.TotalDeliveries,10} │ ");

        // Pozíció
        System.Console.Write($"Node #{courier.CurrentNodeId}");

        // Sor vége - kitöltjük space-ekkel (felülírjuk a régi tartalmat)
        System.Console.Write(new string(' ', 10));
        System.Console.WriteLine();
    }

    /// <summary>
    /// Futár státusz színes megjelenítése.
    /// </summary>
    private void DrawCourierStatus(CourierStatus status)
    {
        var (text, color) = status switch
        {
            CourierStatus.Available => ("🟢 Elérhető", ConsoleColor.Green),
            CourierStatus.Delivering => ("🟡 Szállít  ", ConsoleColor.Yellow),
            CourierStatus.Offline => ("⚫ Offline  ", ConsoleColor.Gray),
            _ => ("⚪ Ismeretlen", ConsoleColor.White)
        };

        System.Console.ForegroundColor = color;
        System.Console.Write(text);
        System.Console.ResetColor();
    }

    /// <summary>
    /// Rendelések összegző statisztika.
    /// </summary>
    private void DrawOrdersSummary(IEnumerable<DeliveryOrder> orders)
    {
        WriteSectionHeader("📦 RENDELÉSEK");

        var ordersList = orders.ToList();
        var pending = ordersList.Count(o => o.Status == OrderStatus.Pending);
        var inTransit = ordersList.Count(o => o.Status == OrderStatus.InTransit);
        var delivered = ordersList.Count(o => o.Status == OrderStatus.Delivered);

        // Pending
        System.Console.Write("  ⏳ Függőben:       ");
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine($"{pending,3}");
        System.Console.ResetColor();

        // In Transit
        System.Console.Write("  🚗 Szállítás alatt: ");
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine($"{inTransit,3}");
        System.Console.ResetColor();

        // Delivered
        System.Console.Write("  ✅ Kézbesítve:      ");
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine($"{delivered,3}");
        System.Console.ResetColor();

        System.Console.WriteLine();
    }

    /// <summary>
    /// Statisztikák megjelenítése.
    /// </summary>
    private void DrawStatistics(SimulationStats stats)
    {
        WriteSectionHeader("📊 STATISZTIKÁK");

        System.Console.WriteLine($"  Összes kézbesítés:  {stats.TotalDeliveries,4}");
        System.Console.WriteLine($"  Késések száma:      {stats.TotalDelays,4}");

        // Késési arány (színes)
        System.Console.Write($"  Késési arány:       ");

        if (stats.TotalDeliveries > 0)
        {
            var color = stats.DelayPercentage > 20
                ? ConsoleColor.Red
                : ConsoleColor.Green;

            System.Console.ForegroundColor = color;
            System.Console.WriteLine($"{stats.DelayPercentage,5:F1}%");
            System.Console.ResetColor();
        }
        else
        {
            System.Console.WriteLine("  N/A");
        }

        System.Console.WriteLine();
    }

    /// <summary>
    /// Footer - leállítási útmutatás.
    /// </summary>
    private void DrawFooter()
    {
        System.Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Console.WriteLine("───────────────────────────────────────────────────────────────────────");
        System.Console.WriteLine("  Leállítás: CTRL+C");
        System.Console.ResetColor();
    }

    /// <summary>
    /// Szekció fejléc rajzolása (pl. "FUTÁROK").
    /// </summary>
    private void WriteSectionHeader(string title)
    {
        System.Console.ForegroundColor = ConsoleColor.White;
        System.Console.WriteLine("───────────────────────────────────────────────────────────────────────");
        System.Console.WriteLine($"  {title}");
        System.Console.WriteLine("───────────────────────────────────────────────────────────────────────");
        System.Console.ResetColor();
    }
}
