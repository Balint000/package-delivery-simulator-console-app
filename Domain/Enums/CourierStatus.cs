namespace package_delivery_simulator.Domain.Enums;

/// <summary>
/// Represents the current availability status of a courier.
/// Determines whether the courier can accept new delivery assignments.
/// </summary>
public enum CourierStatus
{
    /// <summary>
    /// Courier is available and ready to accept new delivery assignments.
    /// Not currently delivering any packages.
    /// </summary>
    Available = 0,

    /// <summary>
    /// Courier is currently busy delivering packages.
    /// Should not be assigned new orders until current deliveries are completed.
    /// </summary>
    Busy = 1,

    /// <summary>
    /// Courier is off duty and not available for any assignments.
    /// Could be on break, end of shift, or not working today.
    /// </summary>
    OffDuty = 2
}
