// ============================================================
// DelayReport.cs  —  Késési riport
// ============================================================
//
// FELELŐSSÉG:
//   Megmutatja, hogy MELY rendelések késtek, MENNYIT késtek,
//   és összesítő statisztikákat ad a késésekről.
//
// HOGYAN HASZNÁLJUK?
//   var report = new DelayReport(allOrders, allCouriers);
//   report.Print();
//
// RENDEZÉS (a feladatkiírás elvárása):
//   A késett rendelések késés szerint csökkentő sorrendbe vannak rendezve
//   → a legsúlyosabb késés jelenik meg legelől.
//
// MIÉRT KÜLÖN OSZTÁLY?
//   A SimulationPresenter a megjelenítésért felel, de nem érdemes
//   bele égetni a részletes riport logikát — az a Reporting réteg dolga.
//   Ha a riport formátuma változik, csak ezt a fájlt kell módosítani.
// ============================================================

namespace package_delivery_simulator_console_app.Reporting;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;

/// <summary>
/// Késési riport — megmutatja az összes késett rendelést és statisztikáikat.
/// </summary>
public class DelayReport
{
    // ── Adatok ───────────────────────────────────────────────────

    /// <summary>
    /// Az összes rendelés (ebből szűrjük ki a késetteket).
    /// </summary>
    private readonly List<DeliveryOrder> _orders;

    /// <summary>
    /// Az összes futár (a késett rendeléshez tartozó futár nevét innen kapjuk).
    /// </summary>
    private readonly List<Courier> _couriers;

    // ── Konstruktor ──────────────────────────────────────────────

    /// <summary>
    /// DelayReport létrehozása a szimuláció után.
    /// </summary>
    /// <param name="orders">Az összes rendelés listája</param>
    /// <param name="couriers">Az összes futár listája</param>
    public DelayReport(List<DeliveryOrder> orders, List<Courier> couriers)
    {
        _orders = orders;
        _couriers = couriers;
    }

    // ── Fő metódus ───────────────────────────────────────────────

    /// <summary>
    /// Kiírja a teljes késési riportot a konzolra.
    ///
    /// TARTALOM:
    ///   1. Fejléc
    ///   2. Összesítő számok (összes késés, átlag, maximum)
    ///   3. Részletes lista: késett rendelések csökkentő sorrendben
    ///   4. Ha nincs egyetlen késés sem: pozitív üzenet
    /// </summary>
    public void Print()
    {
        // ── Fejléc ───────────────────────────────────────────────
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━ KÉSÉSI RIPORT ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();

        // ── Késett rendelések összegyűjtése ──────────────────────
        // Csak azokat vesszük, amelyek ténylegesen késtek (WasDelayed == true)
        // ÉS sikeresen kézbesítésre kerültek — a sikertelenek külön kategória.
        var delayedOrders = _orders
            .Where(o => o.WasDelayed && o.Status == OrderStatus.Delivered)
            .ToList();

        // ── Összesítő sor ────────────────────────────────────────
        int totalDelivered = _orders.Count(o => o.Status == OrderStatus.Delivered);
        int totalDelayed = delayedOrders.Count;

        // Késési arány: hány % volt késve az összes kézbesítettből
        double delayRate = totalDelivered > 0
            ? (double)totalDelayed / totalDelivered * 100.0
            : 0.0;

        Console.Write("   Késett rendelések:  ");
        Console.ForegroundColor = totalDelayed > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.WriteLine($"{totalDelayed} / {totalDelivered} ({delayRate:F1}%)");
        Console.ResetColor();

        // Ha nincs egyetlen késés sem, nem kell tovább menni
        if (totalDelayed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("   ✅ Minden rendelés időben érkezett — nincs késés!");
            Console.ResetColor();
            return;
        }

        // ── Átlagos és maximális késés ───────────────────────────
        // DelayMinutes property: ActualDeliveryTimeMinutes - IdealDeliveryTimeMinutes
        // (csak akkor értelmes, ha WasDelayed == true)
        double avgDelay = delayedOrders.Average(o => o.DelayMinutes);
        int maxDelay = delayedOrders.Max(o => o.DelayMinutes);
        int minDelay = delayedOrders.Min(o => o.DelayMinutes);

        Console.WriteLine($"   Átlagos késés:       {avgDelay:F1} perc");
        Console.WriteLine($"   Legnagyobb késés:    {maxDelay} perc");
        Console.WriteLine($"   Legkisebb késés:     {minDelay} perc");

        // ── Részletes lista ──────────────────────────────────────
        // Rendezés: késés szerint csökkentő (a legsúlyosabb esetek elől)
        // Ez a feladatkiírás elvárása: "Rendezés: késések"
        var sorted = delayedOrders
            .OrderByDescending(o => o.DelayMinutes)
            .ToList();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("   Részletes lista (késés szerint csökkentve):");
        Console.WriteLine($"   {"Rendelés",-12} │ {"Ügyfél",-20} │ {"Zóna",4} │ {"Késés",8} │ {"Futár",-20}");
        Console.WriteLine($"   {new string('─', 12)} │ {new string('─', 20)} │ {new string('─', 4)} │ {new string('─', 8)} │ {new string('─', 20)}");
        Console.ResetColor();

        foreach (var order in sorted)
        {
            // Melyik futár szállította? (AssignedCourierId alapján keressük a nevet)
            string courierName = "—";
            if (order.AssignedCourierId.HasValue)
            {
                var courier = _couriers.FirstOrDefault(c => c.Id == order.AssignedCourierId.Value);
                if (courier != null)
                    courierName = courier.Name;
            }

            // Késés mértéke szerint színezzük a sort:
            // Kis késés (< 5 perc) → sárga, nagy késés (>= 5 perc) → piros
            Console.ForegroundColor = order.DelayMinutes >= 5
                ? ConsoleColor.Red
                : ConsoleColor.Yellow;

            Console.WriteLine(
                $"   {order.OrderNumber,-12} │ " +
                $"{order.CustomerName,-20} │ " +
                $"  {order.ZoneId,2} │ " +
                $"+{order.DelayMinutes,6} p │ " +
                $"{courierName,-20}");
        }

        Console.ResetColor();
    }
}
