namespace package_delivery_simulator_console_app.Presentation.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Élő konzol UI interfész - több futár párhuzamos szimulációjához.
///
/// KONCEPCIÓ:
/// A konzol 3 fő részre oszlik:
/// 1. HEADER - Általános info (város neve, idő)
/// 2. COURIER PANEL - Futárok aktuális státusza (fix pozíció, folyamatosan frissül)
/// 3. EVENT LOG - Események időrendben (alulról görgő)
///
/// THREAD-SAFETY:
/// Minden metódus thread-safe! Több futár párhuzamosan hívhatja őket.
/// </summary>
public interface ILiveConsoleRenderer
{
    // ====== INICIALIZÁLÁS ======

    /// <summary>
    /// Konzol inicializálása (képernyő törlés, kurzor elrejtés, ANSI engedélyezés).
    /// Csak egyszer hívjuk meg a szimuláció kezdetén!
    /// </summary>
    /// <param name="cityName">A város neve (megjelenik a header-ben)</param>
    /// <param name="totalCouriers">Futárok száma (méretezéshez)</param>
    void Initialize(string cityName, int totalCouriers);

    // ====== FUTÁR STÁTUSZ FRISSÍTÉS ======

    /// <summary>
    /// Futár státuszának frissítése a courier panel-ben.
    /// Ez a metódus thread-safe, bármikor hívható!
    /// </summary>
    /// <param name="courierId">Futár ID</param>
    /// <param name="courierName">Futár neve</param>
    /// <param name="status">Jelenlegi státusz (pl. "Moving", "Idle", "Loading")</param>
    /// <param name="currentLocation">Jelenlegi pozíció neve</param>
    /// <param name="targetLocation">Cél pozíció neve (ha van)</param>
    /// <param name="completedDeliveries">Eddig teljesített kézbesítések száma</param>
    /// <param name="totalAssignedDeliveries">Összes hozzárendelt kézbesítés</param>
    /// <param name="estimatedTimeMinutes">Becsült idő a jelenlegi feladathoz (ha van)</param>
    void UpdateCourierStatus(
        int courierId,
        string courierName,
        string status,
        string currentLocation,
        string? targetLocation = null,
        int completedDeliveries = 0,
        int totalAssignedDeliveries = 0,
        int? estimatedTimeMinutes = null);

    // ====== ESEMÉNY NAPLÓZÁS ======

    /// <summary>
    /// Esemény hozzáadása a log-hoz (alul görgő lista).
    /// Thread-safe, több futár párhuzamosan írhat.
    /// </summary>
    /// <param name="eventType">Esemény típusa (pl. "DELIVERY", "MOVE", "TRAFFIC")</param>
    /// <param name="message">Esemény üzenete</param>
    /// <param name="courierId">Melyik futárhoz tartozik (opcionális)</param>
    void LogEvent(string eventType, string message, int? courierId = null);

    // ====== FORGALOM FRISSÍTÉS ======

    /// <summary>
    /// Forgalmi heatmap frissítése (középső panel).
    /// Mutatja, hogy melyik él mennyire terhelt.
    /// </summary>
    /// <param name="edgeInfo">Él információk: (FromNode, ToNode, TrafficMultiplier, CourierCount)</param>
    void UpdateTrafficMap(List<(string FromNode, string ToNode, double TrafficMultiplier, int CourierCount)> edgeInfo);

    // ====== BEFEJEZÉS ======

    /// <summary>
    /// Szimuláció befejezése (kurzor visszaállítás, final summary).
    /// </summary>
    /// void Finalize();

    // ====== HELP METÓDUSOK ======

    /// <summary>
    /// Átmeneti üzenet megjelenítése (pl. "Simulation paused, press any key...").
    /// Ez felülírja a courier panel-t ideiglenesen.
    /// </summary>
    void ShowMessage(string message, string type = "INFO");

    /// <summary>
    /// Képernyő frissítés kényszerítése (ha nem automatikusan történik).
    /// Általában nem kell manuálisan hívni.
    /// </summary>
    void ForceRefresh();
}
