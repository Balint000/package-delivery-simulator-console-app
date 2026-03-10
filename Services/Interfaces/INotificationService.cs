namespace package_delivery_simulator_console_app.Services.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Ügyfélértesítési service interfésze.
///
/// FELELŐSSÉG: Kizárólag az értesítések küldése.
/// A szimuláció eldönti, hogy KELL-E értesíteni — ez a service csak HOGYAN-t tudja.
///
/// MIÉRT KÜLÖN INTERFÉSZ?
/// A jövőben az értesítés módja cserélhető:
///   - ConsoleNotificationService  → konzolra ír (jelenlegi)
///   - EmailNotificationService    → e-mailt küld
///   - SmsNotificationService      → SMS-t küld
/// Az interfész nem változik, csak az implementáció.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Ügyfélértesítés késett kézbesítés esetén.
    ///
    /// A metódus felelős azért, hogy:
    ///   - Az értesítés megjelenjen (konzol / e-mail / SMS)
    ///   - Az order.CustomerNotifiedOfDelay flag true-ra álljon
    ///
    /// MEGHÍVÁSI FELTÉTEL: Csak akkor hívandó, ha order.WasDelayed == true
    /// és order.CustomerNotifiedOfDelay == false!
    /// </summary>
    /// <param name="order">A késett rendelés</param>
    /// <param name="delayMinutes">Késés mértéke percben</param>
    void NotifyDelay(DeliveryOrder order, int delayMinutes);
}
