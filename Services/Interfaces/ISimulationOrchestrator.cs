// ============================================================
// ISimulationOrchestrator.cs
// ============================================================
// A szimuláció orchestrálásának interfésze.
//
// FELELŐSSÉG:
//   - Fogadja a futárokat és rendeléseket
//   - Elvégzi a teljes futtatást (initial assignment + queue loop)
//   - Visszaad egy összesítő eredményt
//
// MIÉRT KÜLÖN INTERFÉSZ?
//   - TPL átállásnál csak az implementációt cseréljük (RunAsync belseje)
//   - Az interfész nem változik: Program.cs nem fogja érezni a változást
//   - Mock-olható teszteléshez
// ============================================================

namespace package_delivery_simulator_console_app.Services.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// A teljes szimulációt vezérlő orchestrator interfésze.
/// </summary>
public interface ISimulationOrchestrator
{
    /// <summary>
    /// Teljes szimuláció futtatása:
    ///   1. Initial batch assignment (minden futár MaxCapacity-ig feltöltve)
    ///   2. Maradék rendelések ConcurrentQueue-ba kerülnek
    ///   3. Minden futár a saját loop-jában dolgozik (most: szekvenciális)
    ///   4. Futár visszaér → RefillCourier → folytatja, amíg van rendelés
    ///
    /// TPL-RE KÉSZ:
    ///   A szekvenciális foreach-et Task.WhenAll-ra cserélve
    ///   azonnal párhuzamos lesz — az architektúra ezt már támogatja.
    /// </summary>
    /// <param name="couriers">Az összes futár (JSON-ból betöltve)</param>
    /// <param name="allOrders">Az összes rendelés (JSON-ból betöltve)</param>
    /// <param name="cancellationToken">Ctrl+C kezeléséhez</param>
    /// <returns>Összesítő eredmény (sikerek, késések, idő)</returns>
    Task<OrchestratorResult> RunAsync(
        List<Courier> couriers,
        List<DeliveryOrder> allOrders,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A teljes szimuláció összesítő eredménye.
///
/// MIÉRT record?
/// Csak adatokat tartalmaz, immutable, automatikus ToString/Equals.
/// Tökéletes visszatérési értéknek.
/// </summary>
public record OrchestratorResult(
    int TotalOrders,        // Összes rendelés száma
    int Delivered,          // Sikeresen kézbesített
    int Delayed,            // Ebből késve volt
    int Failed,             // Sikertelen (nem lett kézbesítve)
    int Unassigned,         // Sosem kapott futárt (queue-ból maradt)
    TimeSpan WallClockTime  // Teljes eltelt valósidő
)
{
    /// <summary>
    /// Kézbesítési sikerráta (0.0 – 1.0).
    /// </summary>
    public double SuccessRate =>
        TotalOrders > 0 ? (double)Delivered / TotalOrders : 0;

    /// <summary>
    /// Késési ráta a kézbesítetteken belül (0.0 – 1.0).
    /// </summary>
    public double DelayRate =>
        Delivered > 0 ? (double)Delayed / Delivered : 0;
}
