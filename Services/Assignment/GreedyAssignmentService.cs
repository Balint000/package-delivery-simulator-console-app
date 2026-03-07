// ============================================================
// GreedyAssignmentService.cs
// ============================================================
// Változás: FindNearestNodeId(Location) eltávolítva.
// Most courier.CurrentNodeId és order.AddressNodeId közvetlenül
// kerül a Dijkstra hívásba — nincs koordináta-approximáció.
// ============================================================

namespace package_delivery_simulator_console_app.Services.Assignment;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;

public class GreedyAssignmentService
{
    private readonly ICityGraph _cityGraph;
    private readonly ILogger<GreedyAssignmentService> _logger;

    public GreedyAssignmentService(
        ICityGraph cityGraph,
        ILogger<GreedyAssignmentService> logger)
    {
        _cityGraph = cityGraph;
        _logger = logger;
    }

    // ====================================================
    // AssignToNearest — egy rendelés hozzárendelése
    // ====================================================

    /// <summary>
    /// Egy rendelés hozzárendelése a legalkalmasabb futárhoz.
    ///
    /// SZŰRÉSI FELTÉTELEK:
    ///   1. Status != OffDuty
    ///   2. HasCapacity
    ///   3. CanWorkInZone(order.ZoneId)
    ///
    /// TÁVOLSÁGMÉRÉS: courier.CurrentNodeId → order.AddressNodeId (Dijkstra)
    /// Nincs koordináta-konverzió.
    /// </summary>
    public Courier? AssignToNearest(
        DeliveryOrder order,
        List<Courier> allCouriers)
    {
        _logger.LogInformation(
            "Greedy hozzárendelés: {OrderNumber} ({CustomerName}, Zóna {ZoneId})...",
            order.OrderNumber, order.CustomerName, order.ZoneId);

        var eligibleCouriers = allCouriers
            .Where(c =>
                c.Status != CourierStatus.OffDuty
                && c.HasCapacity
                && c.CanWorkInZone(order.ZoneId))
            .ToList();

        if (eligibleCouriers.Count == 0)
        {
            _logger.LogWarning(
                "Nincs alkalmas futár a(z) {OrderNumber} rendeléshez! " +
                "(Zóna: {ZoneId}, {Total} futárból egyik sem felel meg)",
                order.OrderNumber, order.ZoneId, allCouriers.Count);
            return null;
        }

        Courier? bestCourier = null;
        int shortestTime = int.MaxValue;

        foreach (var courier in eligibleCouriers)
        {
            // KÖZVETLEN node ID → node ID Dijkstra, koordináta-konverzió nélkül
            var (_, travelTime) = _cityGraph.FindShortestPath(
                courier.CurrentNodeId, order.AddressNodeId);

            if (travelTime == int.MaxValue)
            {
                _logger.LogDebug("  └ {Name}: nem elérhető (nincs útvonal)", courier.Name);
                continue;
            }

            _logger.LogDebug(
                "  └ {Name} [Node {Node}, {Assigned}/{Max}]: {Time} perc",
                courier.Name, courier.CurrentNodeId,
                courier.AssignedOrderIds.Count, courier.MaxCapacity,
                travelTime);

            if (travelTime < shortestTime)
            {
                shortestTime = travelTime;
                bestCourier = courier;
            }
        }

        if (bestCourier != null)
        {
            order.Status = OrderStatus.Assigned;
            order.AssignedCourierId = bestCourier.Id;
            bestCourier.AssignedOrderIds.Add(order.Id);

            if (bestCourier.Status == CourierStatus.Available)
                bestCourier.Status = CourierStatus.Busy;

            _logger.LogInformation(
                "✅ Hozzárendelve: {OrderNumber} → {CourierName} " +
                "(Zóna {ZoneId}, ~{Time} perc, kapacitás: {Assigned}/{Max})",
                order.OrderNumber, bestCourier.Name,
                order.ZoneId, shortestTime,
                bestCourier.AssignedOrderIds.Count, bestCourier.MaxCapacity);
        }

        return bestCourier;
    }

    // ====================================================
    // AssignAll — tömeges hozzárendelés
    // ====================================================

    public Dictionary<int, Courier?> AssignAll(
        List<DeliveryOrder> orders,
        List<Courier> couriers)
    {
        _logger.LogInformation(
            "Tömeges hozzárendelés: {OrderCount} rendelés, {CourierCount} futár",
            orders.Count, couriers.Count);

        var assignments = new Dictionary<int, Courier?>();

        foreach (var order in orders)
        {
            if (order.Status != OrderStatus.Pending)
                continue;

            var assignedCourier = AssignToNearest(order, couriers);
            assignments[order.Id] = assignedCourier;
        }

        int successCount = assignments.Values.Count(c => c != null);
        int failCount = assignments.Values.Count(c => c == null);

        _logger.LogInformation(
            "Tömeges hozzárendelés kész: {Success} sikeres, {Fail} sikertelen",
            successCount, failCount);

        return assignments;
    }

    // ====================================================
    // AssignNextBatch — warehouse visszatérés utáni feltöltés
    // ====================================================

    public List<DeliveryOrder> AssignNextBatch(
        Courier courier,
        List<DeliveryOrder> pendingOrders)
    {
        _logger.LogInformation(
            "Új köteg hozzárendelése: {CourierName} (szabad helyek: {Remaining}/{Max})",
            courier.Name, courier.RemainingCapacity, courier.MaxCapacity);

        var eligibleOrders = pendingOrders
            .Where(o =>
                o.Status == OrderStatus.Pending
                && courier.CanWorkInZone(o.ZoneId))
            .ToList();

        if (eligibleOrders.Count == 0)
        {
            _logger.LogInformation(
                "{CourierName}: nincs várakozó rendelés a zónáiban [{Zones}]",
                courier.Name, string.Join(", ", courier.AssignedZoneIds));
            return new List<DeliveryOrder>();
        }

        int slotsAvailable = courier.RemainingCapacity;
        var newlyAssigned = new List<DeliveryOrder>();

        foreach (var order in eligibleOrders)
        {
            if (newlyAssigned.Count >= slotsAvailable) break;

            order.Status = OrderStatus.Assigned;
            order.AssignedCourierId = courier.Id;
            courier.AssignedOrderIds.Add(order.Id);

            if (courier.Status == CourierStatus.Available)
                courier.Status = CourierStatus.Busy;

            newlyAssigned.Add(order);

            _logger.LogInformation(
                "  📦 {OrderNumber} → {CourierName} (Zóna {ZoneId})",
                order.OrderNumber, courier.Name, order.ZoneId);
        }

        _logger.LogInformation(
            "{CourierName}: {Count} új rendelés hozzárendelve ({Current}/{Max})",
            courier.Name, newlyAssigned.Count,
            courier.AssignedOrderIds.Count, courier.MaxCapacity);

        return newlyAssigned;
    }
}
