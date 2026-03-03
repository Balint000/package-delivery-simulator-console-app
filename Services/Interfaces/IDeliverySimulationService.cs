namespace package_delivery_simulator_console_app.Services.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Kézbesítési szimuláció service - központi vezérlő.
///
/// FELELŐSSÉGEK:
/// - Futárok és rendelések inicializálása
/// - Rendelések hozzárendelése futárokhoz (greedy algoritmus)
/// - Szimuláció futtatása (1 futár útja)
/// - Státusz követés és frissítés
///
/// NEM FELELŐS:
/// - UI kiírás (azt a Presentation layer csinálja)
/// - Gráf betöltés (azt az Infrastructure csinálja)
/// </summary>
public interface IDeliverySimulationService
{
    /// <summary>
    /// Futárok betöltése (JSON-ból vagy memóriából).
    /// </summary>
    Task<List<Courier>> LoadCouriersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rendelések betöltése (JSON-ból vagy memóriából).
    /// </summary>
    Task<List<DeliveryOrder>> LoadOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Egy rendelés hozzárendelése a legközelebbi elérhető futárhoz (greedy).
    ///
    /// GREEDY ALGORITMUS:
    /// - Keresi az összes Available státuszú futárt
    /// - Kiválasztja a legközelebb állót (legrövidebb út a rendeléshez)
    /// - Hozzárendeli a rendelést
    /// </summary>
    /// <param name="order">A hozzárendelendő rendelés</param>
    /// <param name="availableCouriers">Elérhető futárok listája</param>
    /// <returns>A kiválasztott futár (vagy null, ha nincs elérhető)</returns>
    Courier? AssignOrderToNearestCourier(DeliveryOrder order, List<Courier> availableCouriers);

    /// <summary>
    /// Egy futár útjának szimulálása (1 rendelés kézbesítése).
    ///
    /// LÉPÉSEK:
    /// 1. Útvonal számítás (jelenlegi pozíció → rendelés címe)
    /// 2. Útvonal bejárása (node-ról node-ra)
    /// 3. Forgalom frissítés minden lépésnél
    /// 4. Késés detektálás és értesítés
    /// 5. Státusz frissítések (Busy → Delivering → Available)
    /// </summary>
    /// <param name="courier">A futár</param>
    /// <param name="order">A rendelés</param>
    /// <param name="cancellationToken">Megszakítás token (graceful shutdown-hoz)</param>
    /// <returns>Szimuláció eredménye: (Success, ActualTime, IdealTime)</returns>
    Task<SimulationResult> SimulateDeliveryAsync(
        Courier courier,
        DeliveryOrder order,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Szimuláció eredmény model.
/// </summary>
public record SimulationResult(
    bool Success,
    int ActualTimeMinutes,
    int IdealTimeMinutes,
    bool WasDelayed)
{
    /// <summary>
    /// Késés ideje (ha volt).
    /// </summary>
    public int DelayMinutes => WasDelayed ? ActualTimeMinutes - IdealTimeMinutes : 0;
}
