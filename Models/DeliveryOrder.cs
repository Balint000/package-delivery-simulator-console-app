namespace PackageDelivery.Models;

/// <summary>
/// Egy konkrét rendelés adatait tárolja.
/// </summary>
public class DeliveryOrder
{
    public int Id { get; set; }

    // Hova kell vinni? (Koordináták az útvonal-optimalizáláshoz)
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestX { get; set; }
    public double DestY { get; set; }

    // Időzítés
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime Deadline { get; set; } // Mikorra kellene odaérni?
    public DateTime? DeliveredAt { get; set; } // Tényleges érkezési idő

    // Kapcsolatok
    public int ZoneId { get; set; } // Melyik zónához tartozik a cím?
    public int? AssignedCourierId { get; set; } // Melyik futár viszi? (Nullable, mert eleinte nincs hozzárendelve)

    // Státusz (Pending, InProgress, Delivered, Delayed)
    public string Status { get; set; } = "Pending";

    // Extra: Késés esetén értesítés küldve?
    public bool WasDelayNotificationSent { get; set; } = false;
}
