using System;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Services.Interfaces;

// Csak kiírásért felelős. Nincs logika.
// NotificationServiceInterface-ben létrehozott osztály megvalósítása.

namespace package_delivery_simulator.Services.Notification
{
    public class NotificationService : NotificationServiceInterface
    {
        public void NotifyDelay(DeliveryOrder order, TimeSpan delay)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("⚠️  KÉSÉSI ÉRTESÍTÉS");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.ResetColor();

            Console.WriteLine($"Rendelésszám: {order.OrderNumber}");
            Console.WriteLine($"Ügyfél: {order.CustomerName}");
            Console.WriteLine($"Cím: {order.AddressText}");
            Console.WriteLine($"Várható érkezés: {order.ExpectedDeliveryTime.AddMinutes(delay.TotalMinutes):yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Késés: {delay.TotalMinutes:F0} perc");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.ResetColor();
            Console.WriteLine();
        }

        public void NotifyStatusChange(DeliveryOrder order, string oldStatus, string newStatus)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"📦 Rendelés {order.OrderNumber}: ");
            Console.ResetColor();
            Console.Write($"{oldStatus}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" → ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{newStatus}");
            Console.ResetColor();
        }

        public void NotifyDeliveryComplete(DeliveryOrder order)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("✅ SIKERES KÉZBESÍTÉS");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.ResetColor();

            Console.WriteLine($"Rendelésszám: {order.OrderNumber}");
            Console.WriteLine($"Ügyfél: {order.CustomerName}");
            Console.WriteLine($"Cím: {order.AddressText}");
            Console.WriteLine($"Kézbesítve: {order.DeliveredAt.Value:yyyy-MM-dd HH:mm}"); // Null tud lenni, nehány esetben error-ra fut.

            if (order.DeliveredAt.HasValue)
            {
                var deliveryTime = order.DeliveredAt.Value - order.CreatedAt;
                Console.WriteLine($"Teljes idő: {deliveryTime.TotalMinutes:F0} perc");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
