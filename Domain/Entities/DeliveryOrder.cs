using package_delivery_simulator.Domain.Enums;

namespace package_delivery_simulator.Domain.Entities;

/// <summary>
/// Egy kézbesítési rendelést reprezentál.
/// </summary>
public class DeliveryOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Kézbesítési cím szövege — megjelenítéshez és riportokhoz.
    /// </summary>
    public string AddressText { get; set; } = string.Empty;

    /// <summary>
    /// A kézbesítési cím gráf csúcs ID-ja.
    /// JSON-ból töltjük (addressNodeId mező).
    ///
    /// MIÉRT NODE ID ÉS NEM KOORDINÁTA?
    /// A Dijkstra-alapú útvonalkeresés közvetlenül node ID-val dolgozik.
    /// Koordináta → node mapping csak közelítés volt, race conditiont
    /// okozhatott azonos koordinátájú node-ok esetén.
    /// </summary>
    public int AddressNodeId { get; set; }

    public int ZoneId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpectedDeliveryTime { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int? AssignedCourierId { get; set; }

    public int? IdealDeliveryTimeMinutes { get; set; }
    public int? ActualDeliveryTimeMinutes { get; set; }
    public bool WasDelayed { get; set; } = false;
    public bool CustomerNotifiedOfDelay { get; set; } = false;

    public bool IsAssigned => AssignedCourierId.HasValue;
    public bool IsDelivered => Status == OrderStatus.Delivered;

    public int DelayMinutes =>
        WasDelayed && IdealDeliveryTimeMinutes.HasValue && ActualDeliveryTimeMinutes.HasValue
            ? ActualDeliveryTimeMinutes.Value - IdealDeliveryTimeMinutes.Value
            : 0;
}
