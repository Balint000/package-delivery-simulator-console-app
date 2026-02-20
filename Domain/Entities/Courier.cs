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
}
