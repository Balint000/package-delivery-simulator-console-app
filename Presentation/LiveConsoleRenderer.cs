namespace package_delivery_simulator_console_app.Presentation;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator_console_app.Presentation.Interfaces;

/// <summary>
/// Élő konzol megjelenítő — a futárok státusza és az eseménynapló
/// folyamatosan frissül a képernyőn, nem csak alulra görgeti a szöveget.
///
/// A TRÜKK — Console.SetCursorPosition():
///   A konzol minden karakterét egy (bal, sor) koordinátával lehet
///   megcímezni. Ha visszatesszük a kurzort egy korábbi sorra és
///   felülírjuk a tartalmat, az "frissülésnek" látszik a felhasználónak.
///
///   Példa:
///     Console.SetCursorPosition(0, 5);  // Ugrás az 5. sorba
///     Console.Write("új tartalom      "); // Felülírás (régi szöveg törlése)
///
/// THREAD-SAFETY:
///   A TPL-lel több futár párhuzamosan fut — bármelyik hívhatja
///   az UpdateCourierStatus() vagy LogEvent() metódust.
///   A lock(_lock) blokk garantálja, hogy egyszerre csak egy futár
///   rajzol a konzolra — nem "csúszik össze" a kimenet.
/// </summary>
public class LiveConsoleRenderer : ILiveConsoleRenderer
{
    // ── Belső állapot ────────────────────────────────────────────

    /// <summary>
    /// A konzolra írás szinkronizálásához — egyszerre csak egy szál rajzolhat.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Aktuálisan megjelenített sor futáronként (ID → megjelenített szöveg).
    /// UpdateCourierStatus() frissíti, a rajzoló ebből olvas.
    /// </summary>
    private readonly Dictionary<int, string> _courierLines = new();

    /// <summary>
    /// Az utolsó N esemény listája (görgős napló).
    /// Mindig a legújabb van alul — ha betelt, a legrégebbi kiesik.
    /// </summary>
    private readonly List<string> _events = new();

    /// <summary>
    /// Hány eseménysort mutatunk egyszerre (panel mérete).
    /// </summary>
    private const int MaxEventLines = 12;

    /// <summary>
    /// Melyik konzolsorban kezdődik a futárpanel tartalma (az első futár sora).
    /// Initialize() állítja be, aztán nem változik.
    /// </summary>
    private int _courierPanelContentRow;

    /// <summary>
    /// Melyik konzolsorban kezdődik az eseménynapló tartalma.
    /// Initialize() állítja be, futárok száma alapján.
    /// </summary>
    private int _eventPanelContentRow;

    /// <summary>
    /// Hány futár van — ennyi sort foglal le a futárpanel.
    /// </summary>
    private int _courierCount;

    /// <summary>
    /// Igaz, ha Initialize() már lefutott.
    /// </summary>
    private bool _initialized;

    // ────────────────────────────────────────────────────────────
    // INICIALIZÁLÁS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Konzol inicializálása: képernyő törlése, statikus keret kirajzolása,
    /// és a futárpanel/eseménynapló sorpozícióinak rögzítése.
    ///
    /// Csak egyszer hívandó, a szimuláció elején!
    /// </summary>
    public void Initialize(string cityName, int totalCouriers)
    {
        lock (_lock)
        {
            _courierCount = totalCouriers;

            // Kurzor elrejtése — villogás nélküli frissítéshez
            Console.CursorVisible = false;
            Console.Clear();

            // ── Fejléc ───────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  🚚 {cityName,-47}║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // ── Futárpanel fejléc ─────────────────────────────────
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("━━━ FUTÁROK ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.ResetColor();

            // Rögzítjük, hányadik sorban lesz az első futár adata
            _courierPanelContentRow = Console.CursorTop;

            // Üres sorok lefoglalása a futároknak
            // (ezeket fogják majd a futárok felülírni)
            for (int i = 0; i < totalCouriers; i++)
                Console.WriteLine(new string(' ', Console.WindowWidth - 1));

            Console.WriteLine();

            // ── Eseménynapló fejléce ──────────────────────────────
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("━━━ ESEMÉNYEK ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.ResetColor();

            // Rögzítjük az eseménynapló kezdősorát
            _eventPanelContentRow = Console.CursorTop;

            // Üres sorok lefoglalása az eseményeknek
            for (int i = 0; i < MaxEventLines; i++)
                Console.WriteLine(new string(' ', Console.WindowWidth - 1));

            // A kurzor a panel alá kerül — ide nem írunk többet
            Console.WriteLine();

            _initialized = true;
        }
    }

    // ────────────────────────────────────────────────────────────
    // FUTÁR STÁTUSZ FRISSÍTÉS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Egy futár sorának frissítése a futárpanelben.
    ///
    /// HOGYAN MŰKÖDIK?
    ///   1. Meghatározzuk, hányadik sorban van ez a futár
    ///      (_courierPanelContentRow + a futár sorszáma a listában)
    ///   2. Console.SetCursorPosition()-nel visszaugrunk arra a sorra
    ///   3. Felülírjuk az új adatokkal (kitöltjük szóközzel a régi törlésére)
    ///   4. A kurzort visszatesszük a panel aljára (hogy ne "ugorjon" a kép)
    /// </summary>
    public void UpdateCourierStatus(
        int courierId,
        string courierName,
        string status,
        string currentLocation,
        string? targetLocation = null,
        int completedDeliveries = 0,
        int totalAssignedDeliveries = 0,
        int? estimatedTimeMinutes = null)
    {
        if (!_initialized) return;

        lock (_lock)
        {
            // ── Sor tartalmának összeállítása ─────────────────────

            // Státusz ikon
            string icon = status switch
            {
                "moving" => "🚗",
                "loading" => "📦",
                "done" => "✅",
                "idle" => "⏸ ",
                "waiting" => "⏳",
                _ => "   "
            };

            // Helyadat: "Belváros → Északi py." vagy csak "Belváros"
            string location = targetLocation != null
                ? $"{Truncate(currentLocation, 16)} → {Truncate(targetLocation, 16)}"
                : Truncate(currentLocation, 35);

            // Becsült idő
            string eta = estimatedTimeMinutes.HasValue
                ? $"~{estimatedTimeMinutes} perc"
                : "—      ";

            // A teljes sor — fix szélességgel, hogy pontosan törölje a régit
            string line =
                $"  {icon} {courierName,-20} │ {location,-35} │ {eta,-9} │ {completedDeliveries} kézb.";

            // Sorpozíció: a futár ID-ját indexként használjuk (0-tól indul)
            // A courierId 1-től indul a JSON-ban → -1 az eltolás
            int courierIndex = courierId - 1;
            int targetRow = _courierPanelContentRow + courierIndex;

            // ── Visszaugrás és felülírás ──────────────────────────
            int originalRow = Console.CursorTop;
            int originalCol = Console.CursorLeft;

            Console.SetCursorPosition(0, targetRow);

            // Konzolszélesség-biztos kiírás
            // (ha a sor rövidebb a konzolnál, szóközzel töltjük ki — így töröljük a régit)
            Console.Write(PadOrTruncate(line, Console.WindowWidth - 1));

            // Kurzor visszahelyezése az eredeti pozícióba
            Console.SetCursorPosition(originalCol, originalRow);

            // Eltároljuk a sort (debug célra)
            _courierLines[courierId] = line;
        }
    }

    // ────────────────────────────────────────────────────────────
    // ESEMÉNYNAPLÓ
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Új esemény hozzáadása a görgős naplóhoz.
    ///
    /// HOGYAN GÖRGŐS?
    ///   Az _events lista max MaxEventLines elemet tartalmaz.
    ///   Ha betelt, a legrégebbi (első) elemet eltávolítjuk,
    ///   majd az újat a végére fűzzük — így mindig a legfrissebb látszik.
    ///   Ezután az egész eseménypanelt felülírjuk a friss listával.
    /// </summary>
    public void LogEvent(string eventType, string message, int? courierId = null)
    {
        if (!_initialized) return;

        lock (_lock)
        {
            // Időbélyeg + ikon + üzenet összeállítása
            string icon = eventType switch
            {
                "delivery" => "✅",
                "delay" => "⚠️ ",
                "moving" => "🚗",
                "pickup" => "📦",
                "start" => "🚀",
                "done" => "🏁",
                "refill" => "📥",
                _ => "ℹ️ "
            };

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = $"  [{timestamp}] {icon} {message}";

            // Ha betelt, a legrégebbi kiesik
            if (_events.Count >= MaxEventLines)
                _events.RemoveAt(0);

            _events.Add(line);

            // ── Teljes eseménynapló újrarajzolása ─────────────────
            int originalRow = Console.CursorTop;
            int originalCol = Console.CursorLeft;

            for (int i = 0; i < MaxEventLines; i++)
            {
                Console.SetCursorPosition(0, _eventPanelContentRow + i);

                if (i < _events.Count)
                {
                    // Van esemény ezen a soron — kiírjuk
                    string eventLine = _events[i];

                    // Szín a típus alapján
                    if (eventLine.Contains("✅"))
                        Console.ForegroundColor = ConsoleColor.Green;
                    else if (eventLine.Contains("⚠️"))
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    else if (eventLine.Contains("🏁"))
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    else
                        Console.ResetColor();

                    Console.Write(PadOrTruncate(eventLine, Console.WindowWidth - 1));
                    Console.ResetColor();
                }
                else
                {
                    // Üres sor — töröljük az esetleges régi tartalmat
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                }
            }

            Console.SetCursorPosition(originalCol, originalRow);
        }
    }

    // ────────────────────────────────────────────────────────────
    // BEFEJEZÉS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Szimuláció vége — kurzor visszaállítása a panel alá,
    /// hogy az összesítő kiírás ne csússzon bele a panelbe.
    /// </summary>
    public void Complete()
    {
        lock (_lock)
        {
            // Kurzor a legalsó panel alá mozgatása
            int finalRow = _eventPanelContentRow + MaxEventLines + 2;
            Console.SetCursorPosition(0, finalRow);
            Console.CursorVisible = true;
            Console.ResetColor();
        }
    }

    // ────────────────────────────────────────────────────────────
    // EGYÉB INTERFÉSZ METÓDUSOK (egyszerű implementációk)
    // ────────────────────────────────────────────────────────────

    public void UpdateTrafficMap(
        List<(string FromNode, string ToNode, double TrafficMultiplier, int CourierCount)> edgeInfo)
    {
        // Traffic map nem kért funkció — szándékosan üres
    }

    public void ShowMessage(string message, string type = "INFO")
    {
        LogEvent(type.ToLower(), message);
    }

    public void ForceRefresh()
    {
        // Manuális frissítés nem szükséges — minden update azonnal rajzol
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT SEGÉDMETÓDUSOK
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Szöveget max. adott hosszra vág le, "..." jelzéssel ha kellett.
    /// </summary>
    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Szöveget pontosan adott hosszra hozza:
    /// - Ha rövidebb → szóközökkel kiegészíti (így törli a régi tartalmat)
    /// - Ha hosszabb → levágja
    /// </summary>
    private static string PadOrTruncate(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        return text.PadRight(width);
    }
}
