namespace package_delivery_simulator.Services.Routing;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Interfaces;

/// <summary>
/// GREEDY (nearest neighbor) útvonal-optimalizáló szolgáltatás.
///
/// Működés:
/// - Megkeresi a futár aktuális pozíciójához legközelebbi szabad rendelést.
/// - Használja a Location.DistanceTo metódust a távolság számításhoz.
/// - Ez egy egyszerű, gyors, de nem optimális algoritmus (elfogadható a feladathoz).
/// </summary>
public class GreedyRouteOptimizationService : IRouteOptimizationService
{
    /// <summary>
    /// Legközelebbi rendelés keresése a futár pozíciójához.
    /// </summary>
    public DeliveryOrder? FindNearestOrder(Courier courier, IEnumerable<DeliveryOrder> availableOrders)
    {
        // Ha nincs elérhető rendelés, nincs mit választani
        var ordersList = availableOrders.ToList();
        if (!ordersList.Any())
            return null;

        // Futár aktuális pozíciója
        var courierLocation = courier.CurrentLocation;

        // GREEDY algoritmus: legközelebbi rendelés távolság alapján
        // LINQ: OrderBy távolság szerint, majd First (legkisebb távolság)
        var nearestOrder = ordersList
            .OrderBy(order => courierLocation.DistanceTo(order.AddressLocation))
            .First();

        return nearestOrder;
    }
}
