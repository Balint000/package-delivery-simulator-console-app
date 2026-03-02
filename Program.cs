using System;
using System.IO;
using System.Linq;
using System.Threading;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Infrastructure.Graph;
using package_delivery_simulator.Infrastructure.Loaders;

class Program
{
    static void Main()
    {
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║   🚚 PACKAGE DELIVERY SIMULATION - JSON DEMO  ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝\n");

        // ===== 1. VÁROS BETÖLTÉSE JSON-BŐL =====
        string jsonPath = Path.Combine("Data", "city-graph.json");
        CityGraph cityGraph;

        try
        {
            cityGraph = CityGraphLoader.LoadFromJson(jsonPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading city graph: {ex.Message}");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        // Gráf kiírása
        cityGraph.PrintGraph();

        // ===== 2. FUTÁR ÉS RENDELÉS =====
        Console.WriteLine("STEP 2: Creating delivery scenario...\n");

        var warehouseNode = cityGraph.GetNode(0);  // Central Warehouse
        var targetNode = cityGraph.GetNode(1);     // North District

        Courier courier = new Courier
        {
            Id = 1,
            Name = "Kovács Péter",
            CurrentLocation = warehouseNode.Location,
            Status = CourierStatus.Available
        };

        DeliveryOrder order = new DeliveryOrder
        {
            Id = 101,
            OrderNumber = "ORD-00101",
            CustomerName = "Nagy István",
            AddressText = "North District, Main Street 42",
            AddressLocation = targetNode.Location,
            ZoneId = 1,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.Now
        };

        Console.WriteLine($"👤 Courier: {courier.Name}");
        Console.WriteLine($"   Start: {warehouseNode.Name}");
        Console.WriteLine($"\n📦 Order: {order.OrderNumber}");
        Console.WriteLine($"   Destination: {targetNode.Name}");
        Console.WriteLine($"   Customer: {order.CustomerName}");

        // ===== 3. IDEÁLIS VS AKTUÁLIS IDŐ =====
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("STEP 3: Calculating delivery times...\n");

        int idealTime = cityGraph.CalculateIdealTime(0, 1);
        var (path, actualTime) = cityGraph.FindShortestPath(0, 1);

        Console.WriteLine($"⏱️  Ideal time (no traffic): {idealTime} minutes");
        Console.WriteLine($"🚦 Current time (with traffic): {actualTime} minutes");

        if (actualTime > idealTime)
        {
            int delay = actualTime - idealTime;
            double delayPercent = (double)delay / idealTime * 100;

            Console.ForegroundColor = actualTime > idealTime * 1.2
                ? ConsoleColor.Red
                : ConsoleColor.Yellow;

            Console.WriteLine($"⚠️  Delay: +{delay} min ({delayPercent:F1}%)");

            if (actualTime > idealTime * 1.2)
            {
                Console.WriteLine("📧 Customer notification sent!");
            }

            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ On schedule!");
            Console.ResetColor();
        }

        // Útvonal kiírása
        cityGraph.PrintPath(path, actualTime);

        // ===== 4. INTERAKTÍV SZIMULÁCIÓ =====
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("STEP 4: Simulating delivery route...");
        Console.WriteLine("(Press any key to start simulation)\n");
        Console.ReadKey();

        for (int i = 0; i < path.Count - 1; i++)
        {
            int fromId = path[i];
            int toId = path[i + 1];

            var fromNode = cityGraph.GetNode(fromId);
            var toNode = cityGraph.GetNode(toId);
            var edge = cityGraph.GetEdge(fromId, toId);

            Console.WriteLine($"\n🚗 Segment {i + 1}/{path.Count - 1}:");
            Console.WriteLine($"   {fromNode.Name} → {toNode.Name}");
            Console.WriteLine($"   Distance: {edge.CurrentTimeMinutes} min");
            Console.WriteLine($"   Traffic: {edge.TrafficMultiplier:F2}x");

            // Animált progressbar
            Console.Write("   [");
            for (int j = 0; j < 30; j++)
            {
                Thread.Sleep(edge.CurrentTimeMinutes * 5);
                Console.Write("█");
            }
            Console.WriteLine("]");

            // Forgalom frissítés
            cityGraph.RegisterCourierMovement(fromId, toId);
            cityGraph.UpdateTrafficConditions();
        }

        // ===== 5. BEFEJEZÉS =====
        Console.WriteLine("\n" + new string('=', 60));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ DELIVERY COMPLETED!");
        Console.ResetColor();

        order.Status = OrderStatus.Delivered;
        courier.CurrentLocation = targetNode.Location;

        Console.WriteLine($"\n📍 Package delivered to: {targetNode.Name}");
        Console.WriteLine($"📦 Order status: {order.Status}");

        // Forgalom hatás ellenőrzése
        var (_, newTime) = cityGraph.FindShortestPath(0, 1);
        Console.WriteLine($"\n📊 Traffic impact:");
        Console.WriteLine($"   Before: {actualTime} min");
        Console.WriteLine($"   After:  {newTime} min");
        Console.WriteLine($"   Change: {(newTime > actualTime ? "+" : "")}{newTime - actualTime} min");

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
