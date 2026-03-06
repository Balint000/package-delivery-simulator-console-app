// ============================================================
// DeliverySimulationService.cs
// ============================================================
// Felelőssége: EGY futár kézbesítési útjának szimulálása.
//
// MIT NEM CSINÁL MÁR (kiszervezve):
//   - Adatbetöltés → CourierLoader, OrderLoader
//                    (Infrastructure/Loaders/)
//   - Hozzárendelés → GreedyAssignmentService
//                     (Services/Assignment/)
//
// MIT CSINÁL:
//   - Végigvezeti a futárt a teljes kézbesítési folyamaton
//   - Kezeli az állapotátmeneteket (Available → Busy → Available)
//   - Méri az ideális és tényleges kézbesítési időt
//   - Detektálja a késést és "értesíti" az ügyfelet
//   - Node-ról node-ra léptet a városgráfon (TraversePath)
//
// ÁLLAPOTGÉP (state machine) — a futár életciklusa:
//
//   AVAILABLE
//       ↓  [raktárba megy]
//   BUSY (raktárban felveszi a csomagot)
//       ↓  [elindul a kézbesítési helyre]
//   BUSY + InTransit
//       ↓  [megérkezik, kézbesít]
//   AVAILABLE (újra szabad)
// ============================================================

namespace package_delivery_simulator_console_app.Services.Simulation;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Services.Interfaces;

/// <summary>
/// A kézbesítési szimuláció végrehajtója.
/// Megvalósítja az IDeliverySimulationService interfészt.
/// </summary>
public class DeliverySimulationService : IDeliverySimulationService
{
    // ====================================================
    // FÜGGŐSÉGEK (Dependency Injection)
    // ====================================================

    /// <summary>
    /// A városgráf — útvonalkeresés és forgalom szimulálás.
    /// </summary>
    private readonly ICityGraph _cityGraph;

    /// <summary>
    /// Warehouse kezelő — megmondja, melyik raktárból
    /// kell felvenni az adott rendelést.
    /// </summary>
    private readonly IWarehouseService _warehouseService;

    /// <summary>
    /// Logger — strukturált naplózás.
    /// </summary>
    private readonly ILogger<DeliverySimulationService> _logger;

    // ====================================================
    // KONFIGURÁCIÓ
    // ====================================================

    /// <summary>
    /// Hány milliszekundum telik el a valóságban egy szimulációs percért.
    /// 200 ms = egy szimulált perc 0.2 másodpercig tart a képernyőn.
    /// </summary>
    private const int SimulationStepDelayMs = 200;

    /// <summary>
    /// Hány százalékkal lehet lassabb a kézbesítés mielőtt "késésnek" számít.
    /// 1.2 = 20%-os tolerancia (pl. 10 perc ideális → 12 percig még OK)
    /// </summary>
    private const double DelayThreshold = 1.2;

    // ====================================================
    // KONSTRUKTOR
    // ====================================================

    /// <summary>
    /// DeliverySimulationService létrehozása.
    /// </summary>
    /// <param name="cityGraph">Városgráf (DI-ból)</param>
    /// <param name="warehouseService">Warehouse kezelő (DI-ból)</param>
    /// <param name="logger">Logger (DI-ból)</param>
    public DeliverySimulationService(
        ICityGraph cityGraph,
        IWarehouseService warehouseService,
        ILogger<DeliverySimulationService> logger)
    {
        _cityGraph = cityGraph;
        _warehouseService = warehouseService;
        _logger = logger;
    }

    // ====================================================
    // INTERFÉSZ METÓDUSOK — betöltés és hozzárendelés
    // ====================================================
    // Az IDeliverySimulationService interfész elvárja ezeket,
    // de a valódi munkát a dedikált osztályok végzik.
    // Ez az osztály csak "átirányítja" a hívást a megfelelő helyre.
    //
    // MEGJEGYZÉS: Amikor Program.cs-ben bekötjük a DI-t,
    // ezeket a loader-eket is a konstruktoron keresztül adjuk majd be.
    // Egyelőre közvetlenül példányosítjuk őket (ez nem ideális, de működik).

    /// <summary>
    /// Futárok betöltése — delegálja a CourierLoader-nek.
    /// </summary>
    public async Task<List<Courier>> LoadCouriersAsync(
        CancellationToken cancellationToken = default)
    {
        var loader = new Infrastructure.Loaders.CourierLoader(
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<Infrastructure.Loaders.CourierLoader>.Instance);

        return await loader.LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Rendelések betöltése — delegálja az OrderLoader-nek.
    /// </summary>
    public async Task<List<DeliveryOrder>> LoadOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var loader = new Infrastructure.Loaders.OrderLoader(
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<Infrastructure.Loaders.OrderLoader>.Instance);

        return await loader.LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Greedy hozzárendelés — delegálja a GreedyAssignmentService-nek.
    /// </summary>
    public Courier? AssignOrderToNearestCourier(
        DeliveryOrder order,
        List<Courier> availableCouriers)
    {
        var assignmentService = new Assignment.GreedyAssignmentService(
            _cityGraph,
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<Assignment.GreedyAssignmentService>.Instance);

        return assignmentService.AssignToNearest(order, availableCouriers);
    }

    // ====================================================
    // FŐ SZIMULÁCIÓ
    // ====================================================

    /// <summary>
    /// Egy futár kézbesítési útjának teljes szimulálása.
    ///
    /// FOLYAMAT:
    ///   1. Futár pozíciójának node-ra leképezése
    ///   2. Legközelebbi raktár megkeresése
    ///   3. Futár bemegy a raktárba (mozgás szimulálása)
    ///   4. Csomag felvétele (rendelés státusz: InTransit)
    ///   5. Futár elmegy a kézbesítési helyszínre (mozgás szimulálása)
    ///   6. Csomag átadása (rendelés státusz: Delivered)
    ///   7. Késés detektálás + ügyfélértesítés ha szükséges
    ///   8. Futár visszaáll Available státuszra
    /// </summary>
    /// <param name="courier">A szimulált futár</param>
    /// <param name="order">A kézbesítendő rendelés</param>
    /// <param name="cancellationToken">Megszakítási jel (Ctrl+C kezeléshez)</param>
    /// <returns>SimulationResult: siker/kudarc, idők, késés-e</returns>
    public async Task<SimulationResult> SimulateDeliveryAsync(
        Courier courier,
        DeliveryOrder order,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "🚚 Szimuláció indul: {CourierName} → {OrderNumber} ({CustomerName})",
            courier.Name, order.OrderNumber, order.CustomerName);

        // Összes megtett idő számlálója percben
        int totalActualTime = 0;

        // ---- 1. LÉPÉS: Futár pozíciója → gráf csúcs ----
        int courierStartNodeId = FindNearestNodeId(courier.CurrentLocation);

        _logger.LogDebug(
            "Futár kiindulópontja: Node {Id} ({Name})",
            courierStartNodeId,
            _cityGraph.GetNode(courierStartNodeId)?.Name ?? "?");

        // ---- 2. LÉPÉS: Raktár megkeresése (MINDIG zóna alapján, soha nem cache-ből) ----
        //
        // SZABÁLY: A futár a SAJÁT ZÓNÁJÁBAN lévő, hozzá LEGKÖZELEBB LÉVŐ
        // warehouse-ból veszi fel a csomagot.
        //
        // MIÉRT NEM CACHE-ELÜNK (order.NearestWarehouseNodeId-ba)?
        // Különböző futárok különböző zónákban dolgoznak. Ha egy korábbi futár
        // beállította a cache-t, egy másik zónás futár rossz warehouse-t kapna.
        //
        // LÉPÉSEK:
        //   1. Lekérjük az összes warehouse-t
        //   2. Szűrjük a futár saját zónáira (courier.AssignedZoneIds)
        //   3. Dijkstrával megkeressük a legközelebbit a futárhoz
        //   4. Fallback: ha a futár zónájában nincs warehouse → legközelebbi bármely zónából

        var allWarehouses = _warehouseService.GetAllWarehouses();

        var courierZoneWarehouses = allWarehouses
            .Where(w => w.ZoneId.HasValue && courier.AssignedZoneIds.Contains(w.ZoneId.Value))
            .ToList();

        GraphNode? bestWarehouse = null;
        int shortestWhTime = int.MaxValue;

        if (courierZoneWarehouses.Count == 0)
        {
            _logger.LogWarning(
                "{CourierName} zónáiban ({Zones}) nincs warehouse! Fallback: legközelebbi.",
                courier.Name, string.Join(", ", courier.AssignedZoneIds));
            bestWarehouse = _warehouseService.FindNearestWarehouseFromNode(courierStartNodeId);
        }
        else
        {
            foreach (var wh in courierZoneWarehouses)
            {
                var (_, whTime) = _cityGraph.FindShortestPath(courierStartNodeId, wh.Id);
                if (whTime < shortestWhTime)
                {
                    shortestWhTime = whTime;
                    bestWarehouse = wh;
                }
            }
        }

        if (bestWarehouse == null)
        {
            _logger.LogError(
                "Nem található warehouse a(z) {OrderNumber} rendeléshez!",
                order.OrderNumber);
            return new SimulationResult(false, 0, 0, false);
        }

        int warehouseNodeId = bestWarehouse.Id;

        _logger.LogInformation(
            "📦 Csomagfelvételi hely: Node {Id} ({Name})",
            warehouseNodeId,
            _cityGraph.GetNode(warehouseNodeId)?.Name ?? "?");

        // ---- 3. LÉPÉS: Futár bemegy a raktárba (ha nem ott van) ----
        if (courierStartNodeId != warehouseNodeId)
        {
            _logger.LogInformation(
                "🏃 {CourierName} megy a raktárba...", courier.Name);

            var (warehousePath, warehouseTime) = _cityGraph.FindShortestPath(
                courierStartNodeId, warehouseNodeId);

            if (warehousePath.Count == 0)
            {
                _logger.LogError(
                    "A raktár nem elérhető! Raktár: {WId}, Futár pozíció: {CId}",
                    warehouseNodeId, courierStartNodeId);
                return new SimulationResult(false, 0, 0, false);
            }

            await TraversePath(courier, warehousePath, cancellationToken);
            totalActualTime += warehouseTime;
        }

        // Futár most a raktárban van
        courier.CurrentWarehouseNodeId = warehouseNodeId;

        _logger.LogInformation(
            "📦 {CourierName} megérkezett a raktárba, felvette a csomagot.",
            courier.Name);

        // ---- 4. LÉPÉS: Rendelés státusz frissítése ----
        // Assigned → InTransit (szállítás alatt)
        order.Status = OrderStatus.InTransit;

        // ---- 5. LÉPÉS: Kézbesítési cím → gráf csúcs ----
        int deliveryNodeId = FindNearestNodeId(order.AddressLocation);

        _logger.LogDebug(
            "Kézbesítési cím csúcsa: Node {Id} ({Name})",
            deliveryNodeId,
            _cityGraph.GetNode(deliveryNodeId)?.Name ?? "?");

        // ---- 6. LÉPÉS: Ideális kézbesítési idő (forgalom NÉLKÜL) ----
        // TELJES ÚT: futár → raktár → kézbesítési cím, forgalom nélkül.
        // Ez a késés detektáláshoz és az összesítőhöz is ez lesz az alap.
        int idealWarehouseTime = _cityGraph.CalculateIdealTime(courierStartNodeId, warehouseNodeId);
        int idealDeliveryTime = _cityGraph.CalculateIdealTime(warehouseNodeId, deliveryNodeId);
        int idealTime = idealWarehouseTime + idealDeliveryTime;
        order.IdealDeliveryTimeMinutes = idealTime;

        _logger.LogInformation(
            "⏱️  Ideális kézbesítési idő (forgalom nélkül): {Time} perc " +
            "(raktárhoz: {WH} + kézbesítés: {Del})",
            idealTime, idealWarehouseTime, idealDeliveryTime);

        // ---- 7. LÉPÉS: Futár elmegy a kézbesítési helyszínre ----
        _logger.LogInformation(
            "🚗 {CourierName} indul: {Address}", courier.Name, order.AddressText);

        var (deliveryPath, deliveryTime) = _cityGraph.FindShortestPath(
            warehouseNodeId, deliveryNodeId);

        if (deliveryPath.Count == 0)
        {
            _logger.LogError(
                "A kézbesítési cím nem elérhető! Node: {Id}", deliveryNodeId);
            return new SimulationResult(false, totalActualTime, idealTime, false);
        }

        await TraversePath(courier, deliveryPath, cancellationToken);
        totalActualTime += deliveryTime;
        order.ActualDeliveryTimeMinutes = deliveryTime;

        // ---- 8. LÉPÉS: Kézbesítés sikeres ----
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.Now;
        courier.CurrentLocation = _cityGraph.GetNode(deliveryNodeId)!.Location;
        courier.CurrentWarehouseNodeId = null; // Már nem raktárban van

        // Futár statisztikák frissítése
        courier.TotalDeliveriesCompleted++;
        courier.TotalDeliveryTimeMinutes += totalActualTime;

        _logger.LogInformation(
            "✅ Kézbesítve: {OrderNumber} → {CustomerName} ({Time} perc)",
            courier.Name, order.CustomerName, totalActualTime);

        // ---- 9. LÉPÉS: Késés detektálás ----
        // Késés = TELJES tényleges idő > TELJES ideális idő * 1.2 (20%-os tolerancia)
        // Mindkét oldal ugyanazt méri: futár→raktár→cím
        bool wasDelayed = totalActualTime > idealTime * DelayThreshold;

        if (wasDelayed)
        {
            int delayMinutes = totalActualTime - idealTime;
            order.WasDelayed = true;
            courier.TotalDelayedDeliveries++;

            _logger.LogWarning(
                "⚠️  KÉSÉS! {OrderNumber}: +{Delay} perc. " +
                "Ideális: {Ideal} perc, Tényleges: {Actual} perc",
                order.OrderNumber, delayMinutes, idealTime, totalActualTime);

            // Ügyfélértesítés szimulálása (egyelőre csak log, később saját service)
            if (!order.CustomerNotifiedOfDelay)
            {
                order.CustomerNotifiedOfDelay = true;
                _logger.LogInformation(
                    "📧 Ügyfélértesítés: {CustomerName} ({OrderNumber}) — {Delay} perces késés",
                    order.CustomerName, order.OrderNumber, delayMinutes);
            }
        }
        else
        {
            _logger.LogInformation(
                "🟢 Időben kézbesítve! {Time} perc (ideális: {Ideal} perc)",
                totalActualTime, idealTime);
        }

        // ---- 10. LÉPÉS: Futár visszaáll szabaddá ----
        courier.Status = CourierStatus.Available;
        courier.AssignedOrderIds.Remove(order.Id);

        _logger.LogInformation(
            "💤 {CourierName} újra elérhető.", courier.Name);

        return new SimulationResult(
            Success: true,
            ActualTimeMinutes: totalActualTime,
            IdealTimeMinutes: idealTime,
            WasDelayed: wasDelayed);
    }

    // ====================================================
    // PRIVÁT SEGÉDMETÓDUSOK
    // ====================================================

    /// <summary>
    /// Szimulált mozgás egy útvonal mentén, node-ról node-ra.
    ///
    /// HOGYAN MŰKÖDIK:
    /// - Végigmegy az útvonal csúcsain (pl. [0, 5, 1])
    /// - Minden lépésnél (él bejárásakor):
    ///     a) Frissíti a futár pozícióját
    ///     b) Regisztrálja a forgalomhatást az élen
    ///     c) Vár a szimulációs lépésnyi időt (aszinkron, nem blokkoló!)
    ///
    /// THREAD-SAFETY:
    /// Task.Delay NEM blokkolja a szálat!
    /// Ezért tud majd több futár egyszerre futni (TPL feladatnál).
    /// </summary>
    /// <param name="courier">A mozgó futár</param>
    /// <param name="path">Csúcs ID-k listája az útvonalban</param>
    /// <param name="cancellationToken">Megszakítási jel</param>
    private async Task TraversePath(
        Courier courier,
        List<int> path,
        CancellationToken cancellationToken)
    {
        // Végigmegy a szomszédos csúcspárokon
        // Pl. path = [0, 5, 1] → (0→5), (5→1)
        for (int i = 0; i < path.Count - 1; i++)
        {
            // Minden lépés előtt ellenőrizzük: kaptunk-e leállítási jelet?
            cancellationToken.ThrowIfCancellationRequested();

            int fromId = path[i];
            int toId = path[i + 1];

            var fromNode = _cityGraph.GetNode(fromId);
            var toNode = _cityGraph.GetNode(toId);
            var edge = _cityGraph.GetEdge(fromId, toId);

            if (fromNode == null || toNode == null || edge == null)
            {
                _logger.LogWarning(
                    "Hiányzó él a gráfban: {From} → {To}", fromId, toId);
                continue;
            }

            _logger.LogDebug(
                "  ↪ {From} → {To} ({Time} perc, {Traffic:F2}x forgalom)",
                fromNode.Name, toNode.Name,
                edge.CurrentTimeMinutes, edge.TrafficMultiplier);

            // Futár koordinátájának frissítése az új csúcsra
            courier.CurrentLocation = toNode.Location;

            // Forgalom növelése ezen az élen
            _cityGraph.RegisterCourierMovement(fromId, toId);

            // Kis véletlenszerű forgalomváltozás (realisztikusabb szimuláció)
            _cityGraph.UpdateTrafficConditions();

            // Aszinkron várakozás — minden szimulációs percért SimulationStepDelayMs ms
            // Task.Delay NEM blokkolja a többi szálat (→ párhuzamos futárok a TPL részben)
            await Task.Delay(
                edge.CurrentTimeMinutes * SimulationStepDelayMs,
                cancellationToken);
        }
    }

    /// <summary>
    /// A koordinátához legközelebbi gráf-csúcs ID-jának megkeresése.
    ///
    /// Ez az EGYETLEN hely ahol Euklideszi távolságot számolunk,
    /// mert koordinátából (x, y) gráf-node-ot kell csinálni,
    /// és erre nincs Dijkstra-alapú módszer.
    ///
    /// Minden más távolságszámítás Dijkstra-alapú!
    /// </summary>
    private int FindNearestNodeId(Location location)
    {
        int nearestId = 0;
        double minDistance = double.MaxValue;

        foreach (var node in _cityGraph.Nodes)
        {
            // Euklideszi távolság: √((x₂-x₁)² + (y₂-y₁)²)
            double dx = node.Location.X - location.X;
            double dy = node.Location.Y - location.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestId = node.Id;
            }
        }

        return nearestId;
    }
}
