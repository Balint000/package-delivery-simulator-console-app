namespace package_delivery_simulator.Domain.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Kézbesítési szolgáltatás interface.
/// A DeliveryService-t ezen az interface-en keresztül injektáljuk (DI).
/// </summary>
public interface IDeliveryService
{
    /// <summary>
    /// Futár hozzáadása a szimulációhoz.
    /// </summary>
    void AddCourier(Courier courier);

    /// <summary>
    /// Rendelés hozzáadása a szimulációhoz.
    /// </summary>
    void AddOrder(DeliveryOrder order);

    /// <summary>
    /// Összes futár lekérése (snapshot).
    /// </summary>
    IEnumerable<Courier> GetCouriers();

    /// <summary>
    /// Összes rendelés lekérése (snapshot).
    /// </summary>
    IEnumerable<DeliveryOrder> GetOrders();

    /// <summary>
    /// Statisztikák lekérése (thread-safe).
    /// </summary>
    (int TotalDeliveries, int TotalDelays) GetStatistics();

    /// <summary>
    /// Párhuzamos szimuláció futtatása (TPL).
    /// </summary>
    /// <param name="cancellationToken">Leállítási token (CTRL+C kezelés)</param>
    Task RunSimulationAsync(CancellationToken cancellationToken);
}
