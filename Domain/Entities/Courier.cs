using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;

namespace package_delivery_simulator.Domain.Entities;

/// <summary>
/// Egy futárt (kézbesítő személyt) reprezentál a rendszerben.
/// A futárok meghatározott zónákban dolgoznak, és max. 3 rendelést
/// vihetnek egyszerre.
/// </summary>
public class Courier
{
    /// <summary>
    /// Egyedi azonosító.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Futár teljes neve.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Futár aktuális koordinátája a térképen.
    /// Szimuláció közben folyamatosan frissül.
    /// </summary>
    public Location CurrentLocation { get; set; } = new(0, 0);

    /// <summary>
    /// Futár aktuális munkastátusza (szabad, foglalt, szabadnapos).
    /// </summary>
    public CourierStatus Status { get; set; } = CourierStatus.Available;

    /// <summary>
    /// Azok a zóna ID-k, amelyekben ez a futár dolgozhat.
    /// </summary>
    public List<int> AssignedZoneIds { get; set; } = new();

    /// <summary>
    /// Jelenleg hozzárendelt rendelések ID listája.
    /// Maximum MaxCapacity elemű lehet!
    /// </summary>
    public List<int> AssignedOrderIds { get; set; } = new();

    // ===========================
    // ====== KAPACITÁS ==========
    // ===========================

    /// <summary>
    /// Egyszerre vihető rendelések maximális száma.
    ///
    /// MIÉRT 3 AZ ALAPÉRTELMEZETT?
    /// Reális kompromisszum: elég sok rendelés, hogy hatékony legyen,
    /// de nem annyira sok, hogy a szimuláció elveszítse az értelmét.
    /// JSON-ból felülírható, ha egy futár más kapacitással dolgozik.
    ///
    /// Példa a Courier.json-ban:
    ///   "maxCapacity": 5  → ez a futár 5 rendelést vihet
    ///   (ha nincs megadva, az alapértelmezett 3 lesz)
    /// </summary>
    public int MaxCapacity { get; set; } = 3;

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
    /// Van-e még szabad kapacitása új rendelés felvételére?
    ///
    /// Példa: MaxCapacity=3, AssignedOrderIds.Count=2 → true (van 1 szabad hely)
    ///        MaxCapacity=3, AssignedOrderIds.Count=3 → false (tele van)
    /// </summary>
    public bool HasCapacity => AssignedOrderIds.Count < MaxCapacity;

    /// <summary>
    /// Hány szabad hely van még a kapacitásból?
    /// </summary>
    public int RemainingCapacity => MaxCapacity - AssignedOrderIds.Count;

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
