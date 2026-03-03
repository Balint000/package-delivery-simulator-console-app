using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;

namespace package_delivery_simulator.Domain.Entities;

/// <summary>
/// Represents a courier (delivery person) in the system.
/// Couriers pick up and deliver orders inside specific zones.
/// </summary>
public class Courier
{
    /// <summary>
    /// Unique identifier of the courier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Full name of the courier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current location of the courier on the city map.
    /// Updated during the simulation as the courier moves.
    /// </summary>
    public Location CurrentLocation { get; set; } = new(0, 0);

    /// <summary>
    /// Current working status of the courier
    /// (available, busy, or off duty).
    /// </summary>
    public CourierStatus Status { get; set; } = CourierStatus.Available;

    /// <summary>
    /// List of zone IDs where this courier is allowed to work.
    /// </summary>
    public List<int> AssignedZoneIds { get; set; } = new();

    /// <summary>
    /// List of order IDs currently assigned to the courier.
    /// </summary>
    public List<int> AssignedOrderIds { get; set; } = new();

    // ===========================
    // ====== ÚJ PROPERTY-K ======
    // ===========================

    /// <summary>
    /// Jelenleg melyik warehouse-ban van (vagy melyikhez van legközelebb).
    /// Null, ha nem warehouse-ban van.
    /// </summary>
    public int? CurrentWarehouseNodeId { get; set; }

    /// <summary>
    /// Teljesítmény statisztika: összes kézbesítés száma.
    /// </summary>
    public int TotalDeliveriesCompleted { get; set; } = 0;

    /// <summary>
    /// Teljesítmény statisztika: késések száma.
    /// </summary>
    public int TotalDelayedDeliveries { get; set; } = 0;

    /// <summary>
    /// Összes megtett idő (percben) a kézbesítések során.
    /// Használjuk az átlagos sebesség számításához.
    /// </summary>
    public int TotalDeliveryTimeMinutes { get; set; } = 0;

    // ====== HELPER METÓDUSOK ======

    /// <summary>
    /// Van-e jelenleg hozzárendelt rendelése?
    /// </summary>
    public bool HasAssignedOrders => AssignedOrderIds.Count > 0;

    /// <summary>
    /// Warehouse-ban van-e jelenleg?
    /// </summary>
    public bool IsAtWarehouse => CurrentWarehouseNodeId.HasValue;

    /// <summary>
    /// Átlagos kézbesítési idő (percben).
    /// </summary>
    public double AverageDeliveryTime =>
        TotalDeliveriesCompleted > 0
            ? (double)TotalDeliveryTimeMinutes / TotalDeliveriesCompleted
            : 0;

    /// <summary>
    /// Késések aránya (0.0 - 1.0).
    /// </summary>
    public double DelayRate =>
        TotalDeliveriesCompleted > 0
            ? (double)TotalDelayedDeliveries / TotalDeliveriesCompleted
            : 0;

    /// <summary>
    /// Dolgozhat-e ebben a zónában?
    /// </summary>
    public bool CanWorkInZone(int zoneId) => AssignedZoneIds.Contains(zoneId);
}
