namespace package_delivery_simulator.Presentation.Console.Views;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Presentation.Console.ViewsInterfaces;

public class ReportView : IReportView
{
    public void ShowFinalReport(
        IEnumerable<Courier> couriers,
        IEnumerable<DeliveryOrder> orders,
        (int TotalDeliveries, int TotalDelays) stats)
    {
        System.Console.Clear();
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine("╔═══════════════════════════════════════════╗");
        System.Console.WriteLine("║           SZIMULÁCIÓ BEFEJEZVE            ║");
        System.Console.WriteLine("╚═══════════════════════════════════════════╝");
        System.Console.ResetColor();
        System.Console.WriteLine();

        System.Console.WriteLine("VÉGSŐ STATISZTIKÁK:");
        System.Console.WriteLine($"  Összes kézbesítés: {stats.TotalDeliveries}");
        System.Console.WriteLine($"  Késések száma: {stats.TotalDelays}");

        if (stats.TotalDeliveries > 0)
        {
            var rate = (double)stats.TotalDelays / stats.TotalDeliveries * 100;
            System.Console.WriteLine($"  Késési arány: {rate:F1}%");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("FUTÁR TELJESÍTMÉNYEK:");
        foreach (var courier in couriers.OrderByDescending(c => c.TotalDeliveries))
        {
            System.Console.WriteLine($"  {courier.Name,-20}: {courier.TotalDeliveries,3} kézbesítés");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Nyomj ENTER-t a kilépéshez...");
        System.Console.ReadLine();
    }
}
