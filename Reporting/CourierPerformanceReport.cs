// ============================================================
// CourierPerformanceReport.cs  —  Futár teljesítmény riport
// ============================================================
//
// FELELŐSSÉG:
//   Rangsorolja a futárokat teljesítményük alapján,
//   és részletes statisztikákat mutat mindegyikükről.
//
// RENDEZÉSI SZEMPONT (feladatkiírás elvárása: "futárok teljesítménye"):
//   Elsődleges: kézbesített rendelések száma (csökkentő)
//   Másodlagos: késési ráta (növekvő — aki kevesebbet késett, előrébb van)
//   Harmadlagos: átlagos kézbesítési idő (növekvő)
//
// HOGYAN HASZNÁLJUK?
//   var report = new CourierPerformanceReport(couriers);
//   report.Print();
// ============================================================

namespace package_delivery_simulator_console_app.Reporting;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Futár teljesítmény riport — rangsorolja a futárokat és részletes statisztikákat mutat.
/// </summary>
public class CourierPerformanceReport
{
    // ── Adatok ───────────────────────────────────────────────────

    /// <summary>
    /// Az összes futár listája — ezekre számítjuk a statisztikákat.
    /// </summary>
    private readonly List<Courier> _couriers;

    // ── Konstruktor ──────────────────────────────────────────────

    /// <summary>
    /// CourierPerformanceReport létrehozása a szimuláció után.
    /// A futárok TotalDeliveriesCompleted, TotalDelayedDeliveries stb.
    /// mezői már fel vannak töltve a szimuláció által.
    /// </summary>
    /// <param name="couriers">Az összes futár listája</param>
    public CourierPerformanceReport(List<Courier> couriers)
    {
        _couriers = couriers;
    }

    // ── Fő metódus ───────────────────────────────────────────────

    /// <summary>
    /// Kiírja a teljes futárteljesítmény-riportot a konzolra.
    ///
    /// TARTALOM:
    ///   1. Fejléc
    ///   2. Rangsoroló táblázat (minden futárhoz egy sor)
    ///   3. Legjobb és legrosszabb futár kiemelése
    ///   4. Rendszerszintű összesítő
    /// </summary>
    public void Print()
    {
        // ── Fejléc ───────────────────────────────────────────────
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━ FUTÁR TELJESÍTMÉNY RANGSOR ━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();

        // ── Rendezés ─────────────────────────────────────────────
        // Elsődleges: Több kézbesítés = jobb (csökkentő)
        // Másodlagos: Kevesebb késési ráta = jobb (növekvő)
        // Harmadlagos: Kisebb átlagos idő = jobb (növekvő)
        var ranked = _couriers
            .OrderByDescending(c => c.TotalDeliveriesCompleted)  // legtöbb kézbesítés elöl
            .ThenBy(c => c.DelayRate)                            // kisebb késési ráta jobb
            .ThenBy(c => c.AverageDeliveryTime)                  // gyorsabb átlag jobb
            .ToList();

        // ── Táblázat fejléce ─────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(
            $"   {"Rang",4} │ {"Futár",-20} │ {"Zónák",-10} │ " +
            $"{"Kézb.",6} │ {"Késés",6} │ {"Késési %",9} │ {"Átlag idő",10}");
        Console.WriteLine(
            $"   {new string('─', 4)} │ {new string('─', 20)} │ {new string('─', 10)} │ " +
            $"{new string('─', 6)} │ {new string('─', 6)} │ {new string('─', 9)} │ {new string('─', 10)}");
        Console.ResetColor();

        // ── Futárok soronként ────────────────────────────────────
        for (int i = 0; i < ranked.Count; i++)
        {
            var courier = ranked[i];
            int rang = i + 1;

            // Zónák listájának szöveges megjelenítése pl. "1, 2, 3"
            string zones = string.Join(",", courier.AssignedZoneIds);

            // Késési % formázása
            double delayPercent = courier.DelayRate * 100.0;

            // Szín: az első helyezett arany, második ezüst, többi alapszín
            // Ha van késés → sárga figyelmeztetés
            if (rang == 1)
                Console.ForegroundColor = ConsoleColor.Yellow;       // 🥇 arany
            else if (rang == 2)
                Console.ForegroundColor = ConsoleColor.White;        // 🥈 ezüst
            else if (courier.TotalDelayedDeliveries > 0)
                Console.ForegroundColor = ConsoleColor.DarkYellow;   // van késés
            else
                Console.ForegroundColor = ConsoleColor.Gray;         // normál

            Console.Write($"   {rang,4} │ {courier.Name,-20} │ {zones,-10} │ ");

            // Kézbesítések száma
            Console.Write($"{courier.TotalDeliveriesCompleted,6} │ ");

            // Késések száma — piros ha van késés
            if (courier.TotalDelayedDeliveries > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{courier.TotalDelayedDeliveries,6}");
                Console.ForegroundColor = rang <= 2 ? ConsoleColor.Yellow : ConsoleColor.Gray;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{courier.TotalDelayedDeliveries,6}");
                Console.ForegroundColor = rang <= 2 ? ConsoleColor.Yellow : ConsoleColor.Gray;
            }

            Console.Write($" │ {delayPercent,8:F1}% │ ");
            Console.WriteLine($"{courier.AverageDeliveryTime,9:F1} p");
        }

        Console.ResetColor();

        // ── Legjobb és legrosszabb kiemelése ─────────────────────
        // Csak akkor van értelme, ha legalább 2 futár volt
        if (ranked.Count >= 2)
        {
            Console.WriteLine();

            // Legjobb: az első a rangsorban
            var best = ranked.First();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                $"   🏆 Legjobb futár:   {best.Name}" +
                $" ({best.TotalDeliveriesCompleted} kézb., {best.AverageDeliveryTime:F1} p átlag)");

            // Legrosszabb: az utolsó a rangsorban
            var worst = ranked.Last();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(
                $"   ⚠️  Fejlesztendő:   {worst.Name}" +
                $" ({worst.TotalDelayedDeliveries} késés, {worst.AverageDeliveryTime:F1} p átlag)");

            Console.ResetColor();
        }

        // ── Rendszerszintű összesítő ─────────────────────────────
        // Ezek az egész csapat teljesítményét mutatják
        int totalCompleted = _couriers.Sum(c => c.TotalDeliveriesCompleted);
        int totalDelayed = _couriers.Sum(c => c.TotalDelayedDeliveries);

        // Átlagos kézbesítési idő az összes futár átlagából (súlyozatlan)
        double systemAvgTime = _couriers
            .Where(c => c.TotalDeliveriesCompleted > 0)
            .Average(c => c.AverageDeliveryTime);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"   Rendszer összesen:  {totalCompleted} kézbesítés" +
                          $" │ {totalDelayed} késés" +
                          $" │ {systemAvgTime:F1} p átlag");
        Console.ResetColor();
    }
}
