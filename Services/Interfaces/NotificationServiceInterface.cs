using System;
using package_delivery_simulator.Domain.Entities;

// Az interface csak szerződés: “ilyen metódusaim lesznek”, de nincs benne logika.

namespace package_delivery_simulator.Services.Interfaces
{
    public interface NotificationServiceInterface
    {
        void NotifyDelay(DeliveryOrder order, TimeSpan delay); // Ha késik egy rendelés, akkor értesítés küldése
        void NotifyStatusChange(DeliveryOrder order, string oldStatus, string newStatus); // Ha állapot változik egy rendelésben, akkor értesítés küldése (Pl. Pending -> In Transit)
        void NotifyDeliveryComplete(DeliveryOrder order); // Ha egy rendelés kiszállítása befejeződött, akkor értesítés küldése
    }
}
