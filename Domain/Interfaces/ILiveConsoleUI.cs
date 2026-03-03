namespace package_delivery_simulator.Domain.Interfaces;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Presentation.Console;

/// <summary>
/// Élő konzol UI interface.
/// Lehetővé teszi, hogy a LiveConsoleUI könnyen lecserélhető vagy mockolható legyen.
/// </summary>
public interface ILiveConsoleUI
{
    /// <summary>
    /// UI inicializálás - fix header kirajzolása.
    /// </summary>
    void Initialize();

    /// <summary>
    /// UI frissítése (élő státusz).
    /// </summary>
    void Update(IEnumerable<Courier> couriers, IEnumerable<DeliveryOrder> orders, SimulationStats stats);

    /// <summary>
    /// Cleanup - kurzor és színek visszaállítása.
    /// </summary>
    void Cleanup();
}
