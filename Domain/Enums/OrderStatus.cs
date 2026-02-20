namespace package_delivery_simulator.Domain.Enums;

/// <summary>
/// Represents the current status of a delivery order in the system.
/// The status flows from Pending -> Assigned -> InTransit -> Delivered.
/// Delayed can occur at any stage after assignment.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order has been created but not yet assigned to any courier.
    /// Initial state of every new order.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Order has been assigned to a courier but delivery has not started yet.
    /// Courier is preparing to pick up or is en route to pickup location.
    /// </summary>
    Assigned = 1,

    /// <summary>
    /// Order is currently being delivered by the assigned courier.
    /// Package is in transit to the customer address.
    /// </summary>
    InTransit = 2,

    /// <summary>
    /// Order has been successfully delivered to the customer.
    /// Final successful state.
    /// </summary>
    Delivered = 3,

    /// <summary>
    /// Order delivery is delayed beyond the expected delivery time.
    /// Can trigger customer notifications.
    /// </summary>
    Delayed = 4
}
