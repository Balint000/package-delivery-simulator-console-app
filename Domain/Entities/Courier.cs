using package_delivery_simulator.Domain.Enums;

namespace package_delivery_simulator.Domain.Entities;

/// <summary>
/// Egy futárt (kézbesítő személyt) reprezentál a rendszerben.
/// A futárok meghatározott zónákban dolgoznak, és max. 3 rendelést
/// vihetnek egyszerre.
/// </summary>
public class Courier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A futár aktuális pozíciója — gráf csúcs ID-ja.
    /// JSON-ból töltjük (startNodeId mezőként), szimuláció közben frissül.
    ///
    /// MIÉRT NODE ID ÉS NEM KOORDINÁTA?
    /// Az útvonalkeresés (Dijkstra) gráf csúcsokon dolgozik, nem koordinátákon.
    /// A koordináta → node mapping Euklideszi közelítés volt, ami hibás
    /// eredményeket adhatott. Node ID → közvetlen, pontos gráf-hivatkozás.
    /// </summary>
    public int CurrentNodeId { get; set; }

    public CourierStatus Status { get; set; } = CourierStatus.Available;
    public List<int> AssignedZoneIds { get; set; } = new();
    public List<int> AssignedOrderIds { get; set; } = new();

    /// <summary>
    /// Egyszerre vihető rendelések maximális száma (alapértelmezett: 3).
    /// JSON-ból felülírható.
    /// </summary>
    public int MaxCapacity { get; set; } = 3;

    public int? CurrentWarehouseNodeId { get; set; }
    public int TotalDeliveriesCompleted { get; set; } = 0;
    public int TotalDelayedDeliveries { get; set; } = 0;
    public int TotalDeliveryTimeMinutes { get; set; } = 0;

    public bool HasAssignedOrders => AssignedOrderIds.Count > 0;
    public bool IsAtWarehouse => CurrentWarehouseNodeId.HasValue;
    public bool HasCapacity => AssignedOrderIds.Count < MaxCapacity;
    public int RemainingCapacity => MaxCapacity - AssignedOrderIds.Count;

    public double AverageDeliveryTime =>
        TotalDeliveriesCompleted > 0
            ? (double)TotalDeliveryTimeMinutes / TotalDeliveriesCompleted
            : 0;

    public double DelayRate =>
        TotalDeliveriesCompleted > 0
            ? (double)TotalDelayedDeliveries / TotalDeliveriesCompleted
            : 0;

    public bool CanWorkInZone(int zoneId) => AssignedZoneIds.Contains(zoneId);
}
