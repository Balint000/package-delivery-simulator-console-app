namespace package_delivery_simulator_console_app.Services.Notification;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator_console_app.Services.Interfaces;

/// <summary>
/// Konzol-alapú ügyfélértesítő service.
///
/// FELELŐSSÉG: Késett rendelések esetén értesítést ír a konzolra.
/// Ez az egyetlen hely ahol az értesítési logika él — a szimuláció
/// csak meghívja, nem tudja hogyan történik az értesítés.
///
/// BŐVÍTÉSI LEHETŐSÉG:
/// Ha valódi e-mail / SMS értesítés kell, elég ezt az osztályt
/// lecserélni — az INotificationService interfész nem változik.
/// </summary>
public class NotificationService : INotificationService
{
    // ── Függőségek ───────────────────────────────────────────────
    private readonly ILogger<NotificationService> _logger;

    // ── Konstruktor ──────────────────────────────────────────────
    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────
    // ÉRTESÍTÉS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Késési értesítés küldése konzolra.
    ///
    /// MŰKÖDÉS:
    ///   1. Ellenőrzi, hogy nem küldtük-e már el az értesítést
    ///   2. Beállítja a CustomerNotifiedOfDelay flaget (idempotens)
    ///   3. Kiírja az értesítést a konzolra
    ///
    /// IDEMPOTENCIA: Többszöri hívás esetén csak egyszer értesít.
    /// </summary>
    /// <param name="order">A késett rendelés</param>
    /// <param name="delayMinutes">Késés mértéke percben</param>
    public void NotifyDelay(DeliveryOrder order, int delayMinutes)
    {
        // Idempotencia: ha már értesítettük az ügyfelet, ne csináljuk újra
        if (order.CustomerNotifiedOfDelay)
        {
            _logger.LogDebug(
                "{OrderNumber} ügyfele ({CustomerName}) már értesítve lett, kihagyva.",
                order.OrderNumber, order.CustomerName);
            return;
        }

        // Értesítés flag beállítása
        order.CustomerNotifiedOfDelay = true;

        // Konzolra írás — ez az "értesítés" jelenlegi formája
        _logger.LogWarning(
            "📧 ÜGYFÉLÉRTESÍTÉS → {CustomerName} | {OrderNumber} | " +
            "{DelayMinutes} perces késés | Cím: {Address}",
            order.CustomerName,
            order.OrderNumber,
            delayMinutes,
            order.AddressText);
    }
}
