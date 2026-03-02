namespace package_delivery_simulator.Services.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Értesítési szolgáltatás interface.
/// Felelős: késések esetén értesítés küldése (feladat szerint: "késés esetén értesítés").
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Késleltetett kézbesítés értesítése.
    /// Meghívódik, amikor egy rendelés a várható idő után érkezik meg.
    /// </summary>
    /// <param name="order">A késleltetett rendelés</param>
    /// <param name="delayMinutes">Késés mértéke percekben</param>
    Task NotifyDelayAsync(DeliveryOrder order, int delayMinutes);
}
