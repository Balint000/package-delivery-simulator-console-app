namespace package_delivery_simulator.Services.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Útvonal-optimalizálási szolgáltatás interface.
/// Felelős: legközelebbi rendelés kiválasztása futár számára (greedy).
/// </summary>
public interface IRouteOptimizationService
{
    /// <summary>
    /// Legközelebbi rendelés keresése a futár aktuális pozíciójához képest.
    /// GREEDY algoritmus: legközelebbi távolság alapján (nearest neighbor).
    /// </summary>
    /// <param name="courier">A futár, akinek rendelést keresünk</param>
    /// <param name="availableOrders">Szabad (Pending) rendelések listája</param>
    /// <returns>A legközelebbi rendelés, vagy null ha nincs elérhető</returns>
    DeliveryOrder? FindNearestOrder(Courier courier, IEnumerable<DeliveryOrder> availableOrders);
}
