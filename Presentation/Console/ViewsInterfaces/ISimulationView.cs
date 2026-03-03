namespace package_delivery_simulator.Presentation.Console.ViewsInterfaces;

using package_delivery_simulator.Domain.Entities;

public interface ISimulationView
{
    void UpdateDisplay(
        IEnumerable<Courier> couriers,
        IEnumerable<DeliveryOrder> orders,
        (int TotalDeliveries, int TotalDelays) stats
    );
}
