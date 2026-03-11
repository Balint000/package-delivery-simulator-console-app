// ============================================================
// ZoneLoadReport.cs  —  Zónánkénti terhelés riport
// ============================================================
//
// FELELŐSSÉG:
//   Megmutatja, hogy az egyes zónák mennyire voltak "terhelve":
//   hány rendelés érkezett, hány lett kézbesítve, késett-e valami,
//   és melyik futárok dolgoztak az adott zónában.
//
// RENDEZÉSI SZEMPONT (feladatkiírás elvárása: "zónánkénti terhelés"):
//   Zóna ID szerint növekvő sorrend (Zóna 1, 2, 3, 4).
//   Ezen belül látható az összes fontos mutató egy sorban.
//
// FOGALMAK:
//   "Terhelés"  = hány rendelés érkezett a zónába
//   "Teljesített" = ebből hányat sikerült kézbesíteni
//   "Hatékonyság" = kézbesítve / összes (%)
//
// HOGYAN HASZNÁLJUK?
//   var report = new ZoneLoadReport(allOrders, allCouriers);
//   report.Print();
// ============================================================

namespace package_delivery_simulator_console_app.Reporting;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;

/// <summary>
/// Zónánkénti terhelés riport — megmutatja, hogyan oszlott meg a munka a zónák között.
/// </summary>
public class ZoneLoadReport
{
    // ── Adatok ───────────────────────────────────────────────────

    /// <summary>
    /// Az összes rendelés — ezekből csoportosítjuk zónánként.
    /// </summary>
    private readonly List<DeliveryOrder> _orders;

    /// <summary>
    /// Az összes futár — ezekből tudjuk, melyik zónában melyik futár dolgozik.
    /// </summary>
    private readonly List<Courier> _couriers;

    // ── Konstruktor ──────────────────────────────────────────────

    /// <summary>
    /// ZoneLoadReport létrehozása a szimuláció után.
    /// </summary>
    /// <param name="orders">Az összes rendelés listája</param>
    /// <param name="couriers">Az összes futár listája</param>
    public ZoneLoadReport(List<DeliveryOrder> orders, List<Courier> couriers)
    {
        _orders = orders;
        _couriers = couriers;
    }

    // ── Fő metódus ───────────────────────────────────────────────

    /// <summary>
    /// Kiírja a zónánkénti terhelési riportot a konzolra.
    ///
    /// TARTALOM:
    ///   1. Fejléc
    ///   2. Táblázat: zónánként egy sor (összes rendelés, kézbesített, késett, futárok)
    ///   3. Legterheltebb és legkevésbé terhelt zóna kiemelése
    ///   4. Egyenetlen elosztásra figyelmeztetés ha szükséges
    /// </summary>
    public void Print()
    {
        // ── Fejléc ───────────────────────────────────────────────
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("━━━ ZÓNÁNKÉNTI TERHELÉS ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.ResetColor();

        // ── Zónák azonosítása ─────────────────────────────────────
        // Az összes egyedi zóna ID-t kiszűrjük a rendelésekből.
        // Így nem kell hardkódolni a zónák számát — ha új zóna kerül,
        // automatikusan megjelenik a riportban.
        var zoneIds = _orders
            .Select(o => o.ZoneId)
            .Distinct()
            .OrderBy(z => z)   // Zóna 1, 2, 3, 4 sorrendben
            .ToList();

        if (zoneIds.Count == 0)
        {
            Console.WriteLine("   Nincs adat a zónákról.");
            return;
        }

        // ── Táblázat fejléce ─────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(
            $"   {"Zóna",5} │ {"Rendelés",9} │ {"Kézbesítve",11} │ " +
            $"{"Késett",7} │ {"Hatékonys.",11} │ Futárok");
        Console.WriteLine(
            $"   {new string('─', 5)} │ {new string('─', 9)} │ {new string('─', 11)} │ " +
            $"{new string('─', 7)} │ {new string('─', 11)} │ {new string('─', 20)}");
        Console.ResetColor();

        // ── Adatok gyűjtése minden zónáról ──────────────────────
        // ZoneStats: egy belső segédosztály, hogy ne kelljen sok változót kezelni
        var zoneStatsList = new List<ZoneStats>();

        foreach (int zoneId in zoneIds)
        {
            // Rendelések ebben a zónában
            var zoneOrders = _orders.Where(o => o.ZoneId == zoneId).ToList();
            int total = zoneOrders.Count;

            // Kézbesítve (Delivered státuszú rendelések)
            int delivered = zoneOrders.Count(o => o.Status == OrderStatus.Delivered);

            // Késett kézbesítések
            int delayed = zoneOrders.Count(o => o.WasDelayed);

            // Hatékonyság: kézbesítve / összes (%)
            double efficiency = total > 0 ? (double)delivered / total * 100.0 : 0.0;

            // Futárok, akik dolgoztak ebben a zónában (AssignedZoneIds alapján)
            // Egy futár akkor "dolgozik" egy zónában, ha az ő zóna listájában
            // szerepel az adott zóna ID.
            var workingCouriers = _couriers
                .Where(c => c.AssignedZoneIds.Contains(zoneId))
                .Select(c => c.Name.Split(' ')[0])   // Csak a keresztnév (rövidebb)
                .ToList();

            string couriersStr = workingCouriers.Count > 0
                ? string.Join(", ", workingCouriers)
                : "—";

            zoneStatsList.Add(new ZoneStats(zoneId, total, delivered, delayed, efficiency, couriersStr));
        }

        // ── Táblázat sorok kiírása ────────────────────────────────
        foreach (var stats in zoneStatsList)
        {
            // Szín a hatékonyság alapján:
            // 100% → zöld, 80-99% → sárga, <80% → piros
            if (stats.Efficiency >= 100.0)
                Console.ForegroundColor = ConsoleColor.Green;
            else if (stats.Efficiency >= 80.0)
                Console.ForegroundColor = ConsoleColor.Yellow;
            else
                Console.ForegroundColor = ConsoleColor.Red;

            Console.Write($"   {stats.ZoneId,5} │ ");
            Console.Write($"{stats.Total,9} │ ");
            Console.Write($"{stats.Delivered,11} │ ");

            // Késett szám: ha 0 → zöld, ha > 0 → piros
            if (stats.Delayed > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{stats.Delayed,7}");
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{stats.Delayed,7}");
            }

            // Hatékonyság színezése
            if (stats.Efficiency >= 100.0)
                Console.ForegroundColor = ConsoleColor.Green;
            else if (stats.Efficiency >= 80.0)
                Console.ForegroundColor = ConsoleColor.Yellow;
            else
                Console.ForegroundColor = ConsoleColor.Red;

            Console.Write($" │ {stats.Efficiency,10:F1}% │ ");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(stats.CourierNames);
        }

        Console.ResetColor();

        // ── Legterheltebb zóna kiemelése ─────────────────────────
        // A feladatkiírásban szerepel a "zónánkénti terhelés" — ez mutatja,
        // melyik zóna kapta a legtöbb rendelést.
        if (zoneStatsList.Count >= 2)
        {
            var mostLoaded = zoneStatsList.OrderByDescending(z => z.Total).First();
            var leastLoaded = zoneStatsList.OrderBy(z => z.Total).First();

            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(
                $"   🔴 Legterheltebb zóna:  Zóna {mostLoaded.ZoneId}" +
                $" ({mostLoaded.Total} rendelés)");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(
                $"   🟢 Legkevésbé terhelt:  Zóna {leastLoaded.ZoneId}" +
                $" ({leastLoaded.Total} rendelés)");

            Console.ResetColor();

            // ── Egyenetlen elosztás figyelmeztetése ───────────────
            // Ha a legterheltebb zóna 2x annyi rendelést kapott mint a legkevésbé
            // terhelt, érdemes figyelmeztetni az egyenetlen elosztásra.
            if (mostLoaded.Total > leastLoaded.Total * 2 && leastLoaded.Total > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"Egyenetlen terhelés: Zóna {mostLoaded.ZoneId} " +
                    $"{(double)mostLoaded.Total / leastLoaded.Total:F1}x annyit kapott " +
                    $"mint Zóna {leastLoaded.ZoneId}");
                Console.ResetColor();
            }
        }

        // ── Rendszerszintű összesítő ─────────────────────────────
        int grandTotal = zoneStatsList.Sum(z => z.Total);
        int grandDelivered = zoneStatsList.Sum(z => z.Delivered);
        double grandEfficiency = grandTotal > 0
            ? (double)grandDelivered / grandTotal * 100.0
            : 0.0;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(
            $"   Összes zóna:  {grandTotal} rendelés" +
            $" │ {grandDelivered} kézbesítve" +
            $" │ {grandEfficiency:F1}% hatékonyság");
        Console.ResetColor();
    }

    // ── Belső segédosztály ───────────────────────────────────────

    /// <summary>
    /// Egy zóna összesített statisztikáit tárolja.
    /// MIÉRT RECORD?
    ///   Csak adatokat tárol, immutable, tömör szintaxis.
    ///   Nem kell Equals/GetHashCode kézzel — a record csinálja.
    /// </summary>
    private record ZoneStats(
        int ZoneId,           // Zóna azonosítója
        int Total,            // Összes rendelés ebben a zónában
        int Delivered,        // Sikeresen kézbesített rendelések
        int Delayed,          // Késett kézbesítések száma
        double Efficiency,    // Kézbesítési hatékonyság (%)
        string CourierNames   // Ebben a zónában dolgozó futárok neve
    );
}
