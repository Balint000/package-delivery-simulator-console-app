using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;

namespace package_delivery_simulator.Domain.Entities;

/// <summary>
/// Represents a single delivery order in the system.
/// One order = one package to deliver to one address.
/// </summary>
public class DeliveryOrder
{
    /// <summary>
    /// Unique identifier of the order.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Human-readable order number (for example: "ORD-0001").
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Name of the customer who will receive the package.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Text version of the address (street, city, etc.).
    /// For display and reporting.
    /// </summary>
    public string AddressText { get; set; } = string.Empty;

    /// <summary>
    /// Geographic location of the delivery address on the map.
    /// Used for distance and routing calculations.
    /// </summary>
    public Location AddressLocation { get; set; } = new(0, 0);

    /// <summary>
    /// ID of the zone where this address belongs.
    /// Used for assigning orders to couriers per zone.
    /// </summary>
    public int ZoneId { get; set; }

    /// <summary>
    /// Current status of the order in the delivery process.
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>
    /// Time when the order was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Promised delivery time (SLA).
    /// If actual delivery is later than this, the order is delayed.
    /// </summary>
    public DateTime ExpectedDeliveryTime { get; set; }

    /// <summary>
    /// Time when the order was actually delivered.
    /// Null if not delivered yet.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Id of the courier assigned to this order.
    /// Null while no courier is assigned.
    /// </summary>
    public int? AssignedCourierId { get; set; }

    // ===========================
    // ====== ÚJ PROPERTY-K ======
    // ===========================

    /// <summary>
    /// Legközelebbi warehouse node ID, ahonnan fel kell venni.
    /// Számítjuk a rendelés létrehozásakor.
    /// </summary>
    public int? NearestWarehouseNodeId { get; set; }

    /// <summary>
    /// Ideális kézbesítési idő (warehouse-tól a címig, forgalom nélkül).
    /// Késés detektáláshoz használjuk.
    /// </summary>
    public int? IdealDeliveryTimeMinutes { get; set; }

    /// <summary>
    /// Tényleges kézbesítési idő (warehouse-tól a címig).
    /// </summary>
    public int? ActualDeliveryTimeMinutes { get; set; }

    /// <summary>
    /// Késés volt-e (actualTime > idealTime * 1.2).
    /// </summary>
    public bool WasDelayed { get; set; } = false;

    /// <summary>
    /// Ügyfél értesítve lett-e a késésről.
    /// </summary>
    public bool CustomerNotifiedOfDelay { get; set; } = false;

    // ====== HELPER PROPERTY-K ======

    /// <summary>
    /// Hozzá van-e rendelve futárhoz?
    /// </summary>
    public bool IsAssigned => AssignedCourierId.HasValue;

    /// <summary>
    /// Kézbesítve lett-e?
    /// </summary>
    public bool IsDelivered => Status == OrderStatus.Delivered;

    /// <summary>
    /// Késés ideje percekben (ha volt késés).
    /// </summary>
    public int DelayMinutes =>
        WasDelayed && IdealDeliveryTimeMinutes.HasValue && ActualDeliveryTimeMinutes.HasValue
            ? ActualDeliveryTimeMinutes.Value - IdealDeliveryTimeMinutes.Value
            : 0;
}
