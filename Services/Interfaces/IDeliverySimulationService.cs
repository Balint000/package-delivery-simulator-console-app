// ============================================================
// IDeliverySimulationService.cs
// ============================================================
// A kézbesítési szimuláció service interfésze.
//
// FRISSÍTÉS (2026. március):
// A LoadCouriersAsync() és LoadOrdersAsync() metódusok
// kikerültek ebből az interfészből, mert kiszerveztük
// a saját dedikált osztályaikba:
//   - CourierLoader  (Infrastructure/Loaders/)
//   - OrderLoader    (Infrastructure/Loaders/)
//
// Ez az interfész most csak két dologért felel:
//   1. Rendelés hozzárendelése futárhoz (greedy)
//   2. Szimuláció futtatása (egy futár, egy rendelés)
// ============================================================

namespace package_delivery_simulator_console_app.Services.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// A kézbesítési szimuláció service interfésze.
/// </summary>
public interface IDeliverySimulationService
{
    /// <summary>
    /// Egy rendelés hozzárendelése a legközelebbi elérhető futárhoz.
    ///
    /// GREEDY ALGORITMUS:
    /// - Megkeresi az összes Available státuszú futárt
    /// - Dijkstrával kiszámolja mindegyik távolságát a rendeléshez
    /// - A legközelebbit választja és hozzárendeli
    ///
    /// MEGJEGYZÉS:
    /// A tényleges algoritmus a GreedyAssignmentService-ben van,
    /// ez az interfész metódus csak delegálja oda a hívást.
    /// </summary>
    /// <param name="order">A hozzárendelendő rendelés</param>
    /// <param name="availableCouriers">Az összes futár listája</param>
    /// <returns>A kiválasztott futár, vagy null ha nincs szabad</returns>
    Courier? AssignOrderToNearestCourier(
        DeliveryOrder order,
        List<Courier> availableCouriers);

    /// <summary>
    /// Egy futár teljes kézbesítési útjának szimulálása.
    ///
    /// FOLYAMAT:
    ///   futár jelenlegi pozíció
    ///     → raktár (csomag felvétel)
    ///     → kézbesítési cím (csomag átadása)
    ///     → futár visszaáll Available státuszra
    ///
    /// VISSZATÉRÉSI ÉRTÉK:
    /// SimulationResult rekord, ami tartalmazza:
    ///   - Success:            sikerült-e a kézbesítés
    ///   - ActualTimeMinutes:  tényleges kézbesítési idő
    ///   - IdealTimeMinutes:   ideális idő (forgalom nélkül)
    ///   - WasDelayed:         volt-e késés (>20% az ideálisnál)
    /// </summary>
    /// <param name="courier">A szimulált futár</param>
    /// <param name="order">A kézbesítendő rendelés</param>
    /// <param name="cancellationToken">
    ///     Megszakítási jel — ha Ctrl+C-t nyom a felhasználó,
    ///     a szimuláció szépen leáll
    /// </param>
    Task<SimulationResult> SimulateDeliveryAsync(
        Courier courier,
        DeliveryOrder order,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Egy szimuláció eredményét leíró adatosztály.
///
/// MIÉRT "record" és nem "class"?
/// A record egy speciális C# osztály, ami:
///   - Csak adatokat tárol (nincs üzleti logika)
///   - Immutable: létrehozás után nem változtatható
///   - Automatikusan generál ToString(), Equals() metódusokat
///
/// Tökéletes olyan adatcsomagokhoz, amiket csak létrehozunk
/// és átadunk — nem módosítjuk őket utólag.
/// </summary>
public record SimulationResult(
    bool Success,             // Sikerült-e a kézbesítés?
    int ActualTimeMinutes,    // Tényleges kézbesítési idő percben
    int IdealTimeMinutes,     // Ideális idő forgalom nélkül
    bool WasDelayed)          // Volt-e késés?
{
    /// <summary>
    /// A késés mértéke percben.
    /// Ha nem volt késés, 0-t ad vissza.
    ///
    /// Ez egy "computed property" — nem tárolt érték,
    /// mindig újraszámolja magát a többi mezőből.
    /// </summary>
    public int DelayMinutes =>
        WasDelayed
            ? ActualTimeMinutes - IdealTimeMinutes
            : 0;
}
