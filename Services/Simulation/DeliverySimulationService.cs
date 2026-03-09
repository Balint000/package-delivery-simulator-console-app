// ============================================================
// DeliverySimulationService.cs
// ============================================================
// Változás: FindNearestNodeId(Location) eltávolítva.
//
// RÉGEN (koordináta-alapú, pontatlan):
//   int courierStartNodeId = FindNearestNodeId(courier.CurrentLocation);
//   int deliveryNodeId     = FindNearestNodeId(order.AddressLocation);
//
// MOST (node ID, pontos):
//   int courierStartNodeId = courier.CurrentNodeId;
//   int deliveryNodeId     = order.AddressNodeId;
//
// A futár pozícióját is node ID-val tartjuk:
//   courier.CurrentNodeId = deliveryNodeId;   (kézbesítés után)
//   courier.CurrentNodeId = toId;             (TraversePath minden lépésénél)
// ============================================================

namespace package_delivery_simulator_console_app.Services.Simulation;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Services.Interfaces;

public class DeliverySimulationService : IDeliverySimulationService
{
    private readonly ICityGraph _cityGraph;
    private readonly IWarehouseService _warehouseService;
    private readonly ILogger<DeliverySimulationService> _logger;

    private const int SimulationStepDelayMs = 200;
    private const double DelayThreshold = 1.05;

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
    // FŐ SZIMULÁCIÓ
    // ====================================================

    public async Task<SimulationResult> SimulateDeliveryAsync(
        Courier courier,
        DeliveryOrder order,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "🚚 Szimuláció indul: {CourierName} → {OrderNumber} ({CustomerName})",
            courier.Name, order.OrderNumber, order.CustomerName);

        int totalActualTime = 0;

        // ---- 1. LÉPÉS: Futár kiindulópontja ----
        // Közvetlenül a node ID — nincs koordináta-konverzió!
        int courierStartNodeId = courier.CurrentNodeId;

        _logger.LogDebug(
            "Futár kiindulópontja: Node {Id} ({Name})",
            courierStartNodeId,
            _cityGraph.GetNode(courierStartNodeId)?.Name ?? "?");

        // ---- 2. LÉPÉS: Raktár megkeresése (zóna alapján, futárhoz legközelebbi) ----
        var allWarehouses = _warehouseService.GetAllWarehouses();

        var courierZoneWarehouses = allWarehouses
            .Where(w => w.ZoneId.HasValue && courier.AssignedZoneIds.Contains(w.ZoneId.Value))
            .ToList();

        GraphNode? bestWarehouse = null;
        int shortestWhTime = int.MaxValue;

        if (courierZoneWarehouses.Count == 0)
        {
            _logger.LogWarning(
                "{CourierName} zónáiban nincs warehouse, fallback: legközelebbi.",
                courier.Name);
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
            _logger.LogError("Nem található warehouse: {OrderNumber}", order.OrderNumber);
            return new SimulationResult(false, 0, 0, false);
        }

        int warehouseNodeId = bestWarehouse.Id;

        _logger.LogInformation(
            "📦 Csomagfelvételi hely: Node {Id} ({Name})",
            warehouseNodeId, _cityGraph.GetNode(warehouseNodeId)?.Name ?? "?");

        // ---- 3. LÉPÉS: Futár bemegy a raktárba (ha nem ott van) ----
        if (courierStartNodeId != warehouseNodeId)
        {
            _logger.LogInformation("🏃 {CourierName} megy a raktárba...", courier.Name);

            var (warehousePath, warehouseTime) = _cityGraph.FindShortestPath(
                courierStartNodeId, warehouseNodeId);

            if (warehousePath.Count == 0)
            {
                _logger.LogError(
                    "A raktár nem elérhető! WH: {WId}, Futár: {CId}",
                    warehouseNodeId, courierStartNodeId);
                return new SimulationResult(false, 0, 0, false);
            }

            await TraversePath(courier, warehousePath, cancellationToken);
            totalActualTime += warehouseTime;
        }

        courier.CurrentWarehouseNodeId = warehouseNodeId;

        _logger.LogInformation(
            "📦 {CourierName} megérkezett a raktárba, felvette a csomagot.", courier.Name);

        order.Status = OrderStatus.InTransit;

        // ---- 4. LÉPÉS: Kézbesítési cím node ID (KÖZVETLEN — nincs konverzió!) ----
        int deliveryNodeId = order.AddressNodeId;

        _logger.LogDebug(
            "Kézbesítési cím: Node {Id} ({Name})",
            deliveryNodeId, _cityGraph.GetNode(deliveryNodeId)?.Name ?? "?");

        // ---- 5. LÉPÉS: Ideális kézbesítési idő (forgalom nélkül, teljes út) ----
        int idealWarehouseTime = _cityGraph.CalculateIdealTime(courierStartNodeId, warehouseNodeId);
        int idealDeliveryTime = _cityGraph.CalculateIdealTime(warehouseNodeId, deliveryNodeId);
        int idealTime = idealWarehouseTime + idealDeliveryTime;
        order.IdealDeliveryTimeMinutes = idealTime;

        _logger.LogInformation(
            "⏱️  Ideális kézbesítési idő (forgalom nélkül): {Time} perc " +
            "(raktárhoz: {WH} + kézbesítés: {Del})",
            idealTime, idealWarehouseTime, idealDeliveryTime);

        // ---- 6. LÉPÉS: Kézbesítés ----
        _logger.LogInformation(
            "🚗 {CourierName} indul: {Address}", courier.Name, order.AddressText);

        var (deliveryPath, deliveryTime) = _cityGraph.FindShortestPath(
            warehouseNodeId, deliveryNodeId);

        if (deliveryPath.Count == 0)
        {
            _logger.LogError("Kézbesítési cím nem elérhető! Node: {Id}", deliveryNodeId);
            return new SimulationResult(false, totalActualTime, idealTime, false);
        }

        await TraversePath(courier, deliveryPath, cancellationToken);
        totalActualTime += deliveryTime;
        order.ActualDeliveryTimeMinutes = deliveryTime;

        // ---- 7. LÉPÉS: Kézbesítés sikeres ----
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.Now;

        // Futár pozíciója frissítve — node ID-val, koordináta nélkül
        courier.CurrentNodeId = deliveryNodeId;
        courier.CurrentWarehouseNodeId = null;

        courier.TotalDeliveriesCompleted++;
        courier.TotalDeliveryTimeMinutes += totalActualTime;

        _logger.LogInformation(
            "✅ Kézbesítve: {CourierName} → {CustomerName} ({Time} perc)",
            courier.Name, order.CustomerName, totalActualTime);

        // ---- 8. LÉPÉS: Késés detektálás ----
        bool wasDelayed = totalActualTime > idealTime * DelayThreshold;

        if (wasDelayed)
        {
            int delayMinutes = totalActualTime - idealTime;
            order.WasDelayed = true;
            courier.TotalDelayedDeliveries++;

            _logger.LogWarning(
                "⚠️  KÉSÉS! {OrderNumber}: +{Delay} perc. Ideális: {Ideal} perc, Tényleges: {Actual} perc",
                order.OrderNumber, delayMinutes, idealTime, totalActualTime);

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

        // ---- 9. LÉPÉS: Futár visszaáll szabaddá ----
        courier.Status = CourierStatus.Available;
        courier.AssignedOrderIds.Remove(order.Id);

        _logger.LogInformation("💤 {CourierName} újra elérhető.", courier.Name);

        return new SimulationResult(
            Success: true,
            ActualTimeMinutes: totalActualTime,
            IdealTimeMinutes: idealTime,
            WasDelayed: wasDelayed);
    }

    // ====================================================
    // PRIVÁT: TraversePath
    // ====================================================

    /// <summary>
    /// Szimulált mozgás egy útvonal mentén.
    /// Futár pozíciója node ID-val frissül — koordináta nem érintett.
    /// </summary>
    private async Task TraversePath(
        Courier courier,
        List<int> path,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int fromId = path[i];
            int toId = path[i + 1];

            var fromNode = _cityGraph.GetNode(fromId);
            var toNode = _cityGraph.GetNode(toId);
            var edge = _cityGraph.GetEdge(fromId, toId);

            if (fromNode == null || toNode == null || edge == null)
            {
                _logger.LogWarning("Hiányzó él: {From} → {To}", fromId, toId);
                continue;
            }

            _logger.LogDebug(
                "  ↪ {From} → {To} ({Time} perc, {Traffic:F2}x)",
                fromNode.Name, toNode.Name,
                edge.CurrentTimeMinutes, edge.TrafficMultiplier);

            // Pozíció frissítése: node ID, nem koordináta
            courier.CurrentNodeId = toId;

            _cityGraph.RegisterCourierMovement(fromId, toId);
            _cityGraph.UpdateTrafficConditions();

            await Task.Delay(
                edge.CurrentTimeMinutes * SimulationStepDelayMs,
                cancellationToken);
        }
    }
}
