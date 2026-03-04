// ============================================================
// DeliverySimulationService.cs
// ============================================================
// Felelőssége: EGY futár kézbesítési útjának szimulálása.
//
// MIT NEM CSINÁL (kiszervezve más osztályokba):
//   - Adatbetöltés → CourierLoader, OrderLoader
//                    (Infrastructure/Loaders/)
//   - Hozzárendelés logika → GreedyAssignmentService
//                            (Services/Assignment/)
//
// MIT CSINÁL:
//   - Delegálja a greedy hozzárendelést (AssignOrderToNearestCourier)
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
    // HOZZÁRENDELÉS — delegálás GreedyAssignmentService-nek
    // ====================================================

    /// <summary>
    /// Greedy hozzárendelés — átpasszolja a GreedyAssignmentService-nek.
    ///
    /// Ez az osztály nem tartalmazza a greedy algoritmust,
    /// csak meghívja azt aki igen.
    /// Ez a "Single Responsibility" elv: minden osztálynak
    /// egy felelőssége van.
    /// </summary>
    public Courier? AssignOrderToNearestCourier(
        DeliveryOrder order,
        List<Courier> availableCouriers)
    {
        // A hozzárendelés logikája a GreedyAssignmentService-ben van
        // (Services/Assignment/GreedyAssignmentService.cs)
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

        // ---- 2. LÉPÉS: Raktár megkeresése ----
        // Ha a rendeléshez már el van mentve a raktár ID-ja, azt használjuk.
        // Különben most számítjuk ki (WarehouseService + Dijkstra).
        int warehouseNodeId;

        if (order.NearestWarehouseNodeId.HasValue)
        {
            warehouseNodeId = order.NearestWarehouseNodeId.Value;
        }
        else
        {
            var warehouse = _warehouseService.FindNearestWarehouse(
                order.AddressLocation);

            if (warehouse == null)
            {
                _logger.LogError(
                    "Nem található raktár a(z) {OrderNumber} rendeléshez!",
                    order.OrderNumber);
                return new SimulationResult(false, 0, 0, false);
            }

            warehouseNodeId = warehouse.Id;
            order.NearestWarehouseNodeId = warehouseNodeId;
        }

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

        courier.CurrentWarehouseNodeId = warehouseNodeId;

        _logger.LogInformation(
            "📦 {CourierName} megérkezett a raktárba, felvette a csomagot.",
            courier.Name);

        // ---- 4. LÉPÉS: Rendelés státusz: Assigned → InTransit ----
        order.Status = OrderStatus.InTransit;

        // ---- 5. LÉPÉS: Kézbesítési cím → gráf csúcs ----
        int deliveryNodeId = FindNearestNodeId(order.AddressLocation);

        // ---- 6. LÉPÉS: Ideális kézbesítési idő (forgalom NÉLKÜL) ----
        // Ez a késés számításhoz kell — mennyit kellett volna ideálisan.
        int idealTime = _cityGraph.CalculateIdealTime(warehouseNodeId, deliveryNodeId);
        order.IdealDeliveryTimeMinutes = idealTime;

        _logger.LogInformation(
            "⏱️  Ideális kézbesítési idő (forgalom nélkül): {Time} perc", idealTime);

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
        courier.CurrentWarehouseNodeId = null;

        courier.TotalDeliveriesCompleted++;
        courier.TotalDeliveryTimeMinutes += totalActualTime;

        _logger.LogInformation(
            "✅ Kézbesítve: {OrderNumber} → {CustomerName} ({Time} perc)",
            courier.Name, order.CustomerName, totalActualTime);

        // ---- 9. LÉPÉS: Késés detektálás ----
        // Késés = tényleges idő > ideális idő * 1.2 (20%-os tolerancia)
        bool wasDelayed = deliveryTime > idealTime * DelayThreshold;

        if (wasDelayed)
        {
            int delayMinutes = deliveryTime - idealTime;
            order.WasDelayed = true;
            courier.TotalDelayedDeliveries++;

            _logger.LogWarning(
                "⚠️  KÉSÉS! {OrderNumber}: +{Delay} perc. " +
                "Ideális: {Ideal} perc, Tényleges: {Actual} perc",
                order.OrderNumber, delayMinutes, idealTime, deliveryTime);

            if (!order.CustomerNotifiedOfDelay)
            {
                order.CustomerNotifiedOfDelay = true;
                _logger.LogInformation(
                    "📧 Ügyfélértesítés: {CustomerName} ({OrderNumber}) — {Delay} perc késés",
                    order.CustomerName, order.OrderNumber, delayMinutes);
            }
        }
        else
        {
            _logger.LogInformation(
                "🟢 Időben kézbesítve! {Time} perc (ideális: {Ideal} perc)",
                deliveryTime, idealTime);
        }

        // ---- 10. LÉPÉS: Futár visszaáll Available státuszra ----
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
    /// Task.Delay NEM blokkolja a szálat — ezért tud majd
    /// több futár egyszerre futni (TPL feladatnál).
    /// </summary>
    private async Task TraversePath(
        Courier courier,
        List<int> path,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            // Ha Ctrl+C érkezett, leállunk
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

            // Forgalom hatásának regisztrálása
            _cityGraph.RegisterCourierMovement(fromId, toId);
            _cityGraph.UpdateTrafficConditions();

            // Aszinkron várakozás — 1 szimulációs perc = SimulationStepDelayMs ms
            await Task.Delay(
                edge.CurrentTimeMinutes * SimulationStepDelayMs,
                cancellationToken);
        }
    }

    /// <summary>
    /// A koordinátához legközelebbi gráf-csúcs ID-jának megkeresése.
    /// Ez az EGYETLEN hely ahol Euklideszi távolságot számolunk —
    /// minden más számítás Dijkstra-alapú.
    /// </summary>
    private int FindNearestNodeId(Location location)
    {
        int nearestId = 0;
        double minDistance = double.MaxValue;

        foreach (var node in _cityGraph.Nodes)
        {
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
