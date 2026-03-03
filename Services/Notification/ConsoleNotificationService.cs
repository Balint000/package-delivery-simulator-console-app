namespace package_delivery_simulator.Services.Notification;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Interfaces;

/// <summary>
/// Konzol-alapú értesítési szolgáltatás.
/// Késleltetett kézbesítés esetén naplóz (később bővíthető email, SMS, stb.).
///
/// FONTOS: Az ILogger-t használjuk Console.WriteLine helyett!
/// Ez .NET best practice konzolos alkalmazásoknál is.
/// </summary>
public class ConsoleNotificationService : INotificationService
{
    private readonly ILogger<ConsoleNotificationService> _logger;

    /// <summary>
    /// Konstruktor - DI-ből kapja az ILogger-t.
    /// </summary>
    public ConsoleNotificationService(ILogger<ConsoleNotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Késleltetett kézbesítés értesítése.
    /// Jelenleg logol, később bővíthető valódi értesítésekkel.
    /// </summary>
    public Task NotifyDelayAsync(DeliveryOrder order, int delayMinutes)
    {
        // ILogger.LogWarning() - figyelmeztetés szintű log
        // Strukturált logging: {OrderNumber}, {DelayMinutes} később könnyen kereshető
        _logger.LogWarning(
            "⚠️  KÉSLELTETÉS! Rendelés: {OrderNumber}, Ügyfél: {CustomerName}, Késés: {DelayMinutes} perc",
            order.OrderNumber,
            order.CustomerName,
            delayMinutes
        );

        // Aszinkron metódus, de most nincs valódi async művelet
        // Task.CompletedTask = azonnal befejezett Task
        return Task.CompletedTask;
    }
}
