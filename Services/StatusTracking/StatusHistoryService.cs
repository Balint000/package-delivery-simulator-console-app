using System;
using System.Collections.Generic;
using System.Linq;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Services.Interfaces;

namespace package_delivery_simulator.Services.StatusTracking
{
    /// <summary>
    /// Egyszerű, memória alapú státusztörténet szolgáltatás.
    /// - A státuszváltásokról StatusHistory objektumokat hoz létre.
    /// - Ezeket egy belső listában tárolja.
    /// Később könnyen lecserélhető olyan implementációra,
    /// ami JSON fájlba vagy SQLite adatbázisba ment.
    /// </summary>
    public class StatusHistoryService : StatusHistoryServiceInterface
    {
        /// <summary>
        /// Belső lista, ami a státusztörténet bejegyzéseket tárolja.
        /// Jelenleg csak a program futása alatt él (in-memory).
        /// </summary>
        private readonly List<StatusHistory> _entries = new();

        /// <summary>
        /// Egyszerű számláló az Id mező kitöltéséhez.
        /// Adatbázis használatakor ezt majd az adatbázis kezeli.
        /// </summary>
        private int _nextId = 1;

        /// <summary>
        /// Új státusztörténet bejegyzést hoz létre egy rendeléshez.
        /// Ezt kell hívni minden alkalommal, amikor egy rendelés státusza megváltozik.
        /// </summary>
        /// <param name="orderId">A rendelés azonosítója.</param>
        /// <param name="oldStatus">A korábbi státusz.</param>
        /// <param name="newStatus">Az új státusz.</param>
        /// <returns>A létrehozott StatusHistory objektum.</returns>
        public StatusHistory CreateEntry(int orderId, OrderStatus oldStatus, OrderStatus newStatus)
        {
            var entry = new StatusHistory
            {
                Id = _nextId++,              // egyszerű, növekvő azonosító
                OrderId = orderId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedAt = DateTime.Now     // a változtatás idejét itt állítjuk be
            };

            _entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// Visszaadja egy adott rendelés státusztörténetét időrendben.
        /// Ezt később használhatjuk riportoláshoz, debughoz, késések elemzéséhez.
        /// </summary>
        /// <param name="orderId">A rendelés azonosítója.</param>
        /// <returns>A rendeléshez tartozó StatusHistory bejegyzések listája.</returns>
        public IReadOnlyList<StatusHistory> GetHistoryForOrder(int orderId)
        {
            return _entries
                .Where(e => e.OrderId == orderId)
                .OrderBy(e => e.ChangedAt)
                .ToList();
        }
    }
}
