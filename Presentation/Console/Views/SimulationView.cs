namespace package_delivery_simulator.Presentation.Console.Views;

using package_delivery_simulator.Presentation.Console.ViewsInterfaces;
using package_delivery_simulator.Domain.Entities;

public class SimulationView : ISimulationView
{
    public void UpdateDisplay(
        IEnumerable<Courier> couriers,
        IEnumerable<DeliveryOrder> orders,
        (int TotalDeliveries, int TotalDelays) stats)
    {
        System.Console.Clear();
        System.Console.WriteLine("═══════════════════════════════════════════");
        System.Console.WriteLine($"  Kézbesítések: {stats.TotalDeliveries} | Késések: {stats.TotalDelays}");
        System.Console.WriteLine("═══════════════════════════════════════════");
        System.Console.WriteLine();

        System.Console.WriteLine("FUTÁROK:");
        foreach (var courier in couriers)
        {
            var status = courier.Status switch
            {
                Domain.Enums.CourierStatus.Available => "🟢 Elérhető",
                Domain.Enums.CourierStatus.Delivering => "🚚 Szállít",
                _ => "⚪ Nem dolgozik"
            };
            System.Console.WriteLine($"  {courier.Name,-20} {status}  ({courier.TotalDeliveries} db)");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("AKTÍV RENDELÉSEK:");
        var activeOrders = orders.Where(o => o.Status != Domain.Enums.OrderStatus.Delivered).Take(5);
        foreach (var order in activeOrders)
        {
            System.Console.WriteLine($"  {order.OrderNumber} → {order.AddressText} [{order.Status}]");
        }
    }
}
