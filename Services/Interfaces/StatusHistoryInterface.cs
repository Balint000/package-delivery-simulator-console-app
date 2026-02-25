using System.Collections.Generic;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;

namespace package_delivery_simulator.Services.Interfaces
{
    /// <summary>
    /// A rendelés(ek) státusztörténetének kezelésére szolgáló szolgáltatás ,,szerződése".
    /// Csak azt írja le, mit tud a szolgáltatás, azt nem, hogy hogyan valósítjuk meg.
    /// Amíg nincs teljesen implementálva, addit in-memory megvalósítás lesz,
    /// később JSON vagy SQLite implementáció is.
    /// </summary>
    public interface StatusHistoryServiceInterface
    {
        /// <summary>
        /// Létrehoz egy új státusz history bejegyzést egy adott rendeléshez.
        /// Ezt akkor fogjuk hívni, amikor egy rendelés státusza megváltozik
        /// (pl. Pending -> Assigned, Assigned -> InTransit, stb.).
        /// </summary>
        /// <param name="orderId">
        /// Annak a rendelésnek az azonosítója, amelyiknek a státusza változott.
        /// </param>
        /// <param name="oldStatus">
        /// A rendelés korábbi státusza (változás előtti érték).
        /// </param>
        /// <param name="newStatus">
        /// A rendelés új státusza (változás utáni érték).
        /// </param>
        /// <returns>
        /// A létrehozott StatusHistory objektum, amelyet el is mentünk
        /// a háttérben (pl. memóriában, később adatbázisban).
        /// </returns>
        StatusHistory CreateEntry(int orderId, OrderStatus oldStatus, OrderStatus newStatus);

        /// <summary>
        /// Visszaadja egy adott rendeléshez tartozó státusztörténetet.
        /// Ezt használhatjuk riportolásnál, debugnál, vagy késések elemzésénél.
        /// </summary>
        /// <param name="orderId">
        /// Annak a rendelésnek az azonosítója, amelynek a history-ját kérjük.
        /// </param>
        /// <returns>
        /// A rendeléshez tartozó StatusHistory bejegyzések listája.
        /// A sorrendet az implementáció fogja meghatározni (általában időrend).
        /// </returns>
        IReadOnlyList<StatusHistory> GetHistoryForOrder(int orderId);
    }
}
