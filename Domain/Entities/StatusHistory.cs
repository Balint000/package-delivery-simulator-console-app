using System;
using package_delivery_simulator.Domain.Enums;

namespace package_delivery_simulator.Domain.Entities
{
    /// <summary>
    /// Egy rendelés státuszváltozását írja le.
    /// Ezzel később vissza tudjuk nézni, hogy egy csomag
    /// mikor milyen állapoton ment keresztül.
    /// (Domain.Enums.OrderStatus-ban megtalálhatóak, hogy mik ezek)
    /// Tiszta domain osztály, nem tud semmit JSON-ról (vagy SQLite-ról) –> ne keveredjenek a rétegek.
    /// Minden státusztváltozáskor egy új példányt kell létrehozni.
    /// Az OldStatus és a NewStatus gyakorlatilag összekötő mezők.
    /// </summary>
    public class StatusHistory
    {
        /// <summary>
        /// Egyedi azonosító. Jelenleg csak in-memory,
        /// de később adatbázis (JSON vagy ha lesz idő, akkor SQLite) ID is lehet.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Annak a rendelésnek az azonosítója,
        /// amelyikhez ez a státuszváltozás tartozik.
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// A rendelés korábbi státusza.
        /// </summary>
        public OrderStatus OldStatus { get; set; }

        /// <summary>
        /// A rendelés új státusza.
        /// </summary>
        public OrderStatus NewStatus { get; set; }

        /// <summary>
        /// Mikor történt a státuszváltozás.
        /// </summary>
        public DateTime ChangedAt { get; set; }
    }
}
