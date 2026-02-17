using System;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;

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
    }
}
