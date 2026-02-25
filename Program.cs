using System;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator.Services.Interfaces;
using package_delivery_simulator.Services.StatusTracking;

class Program
{
    static void Main()
    {
        // Create a courier starting at location (0, 0)
        Courier courier = new Courier
        {
            Id = 1,
            Name = "Test Courier",
            CurrentLocation = new Location(0, 0),
            Status = CourierStatus.Available
        };

        // Create a delivery order at location (5, 5)
        DeliveryOrder order = new DeliveryOrder
        {
            Id = 10,
            OrderNumber = "ORD-0010",
            CustomerName = "Test Customer",
            AddressText = "Test Street 1",
            AddressLocation = new Location(5, 5),
            ZoneId = 1,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.Now,
            ExpectedDeliveryTime = DateTime.Now.AddMinutes(30)
        };

        // Calculate distance between courier and order address
        double distance = courier.CurrentLocation.DistanceTo(order.AddressLocation);

        // Print basic information to the console
        Console.WriteLine("Courier:");
        Console.WriteLine($"  Id: {courier.Id}");
        Console.WriteLine($"  Name: {courier.Name}");
        Console.WriteLine($"  Location: {courier.CurrentLocation}");
        Console.WriteLine($"  Status: {courier.Status}");
        Console.WriteLine();

        Console.WriteLine("Order:");
        Console.WriteLine($"  Id: {order.Id}");
        Console.WriteLine($"  Number: {order.OrderNumber}");
        Console.WriteLine($"  Customer: {order.CustomerName}");
        Console.WriteLine($"  Address: {order.AddressText}");
        Console.WriteLine($"  Location: {order.AddressLocation}");
        Console.WriteLine($"  Status: {order.Status}");
        Console.WriteLine();

        Console.WriteLine($"Distance between courier and order: {distance:F2}");

        // ===============================
        // StatusHistory DEMÓ
        // ===============================
        Console.WriteLine();
        Console.WriteLine();

        // Létrehozzuk a státusztörténet szolgáltatást.
        // Jelenleg ez egy in-memory implementáció (StatusHistoryService),
        // amely egy listában tárolja a státuszváltásokat.
        // Később ezt az implementációt ki lehet cserélni olyanra,
        // ami JSON-ba vagy SQLite adatbázisba ment.
        StatusHistoryServiceInterface statusHistoryService = new StatusHistoryService();

        // Kiinduló státusz: a rendelés jelenlegi státusza (Pending).
        var oldStatus = order.Status;

        // 1) Pending -> Assigned
        order.Status = OrderStatus.Assigned;
        statusHistoryService.CreateEntry(order.Id, oldStatus, order.Status);

        // 2) Assigned -> InTransit
        oldStatus = order.Status;
        order.Status = OrderStatus.InTransit;
        statusHistoryService.CreateEntry(order.Id, oldStatus, order.Status);

        // 3) InTransit -> Delivered
        oldStatus = order.Status;
        order.Status = OrderStatus.Delivered;
        statusHistoryService.CreateEntry(order.Id, oldStatus, order.Status);

        // A history kiírása a konzolra, hogy lássuk,
        // milyen sorrendben változott a rendelés státusza.
        Console.WriteLine("Status history for order:");
        var historyEntries = statusHistoryService.GetHistoryForOrder(order.Id);

        foreach (var entry in historyEntries)
        {
            Console.WriteLine(
                $"{entry.ChangedAt:HH:mm:ss} - {entry.OldStatus} -> {entry.NewStatus}");
        }
    }
}
