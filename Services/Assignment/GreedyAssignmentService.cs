// ============================================================
// GreedyAssignmentService.cs — FRISSÍTVE
// ============================================================
// Új szabályok:
//   1. ZÓNA-SZŰRÉS: futár csak a saját zónájában lévő rendelést kaphat
//   2. KAPACITÁS:   futár max MaxCapacity (alapban 3) rendelést vihet
//   3. BUSY + VAN HELY: Busy futár is kaphat új rendelést, ha van szabad helye
//   4. AssignNextBatch: warehouse visszatéréskor tölt fel max MaxCapacity-ig
// ============================================================

namespace package_delivery_simulator_console_app.Services.Assignment;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
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
    /// SZŰRÉSI FELTÉTELEK (mind teljesüljön):
    ///   1. Status != OffDuty
    ///   2. HasCapacity (AssignedOrderIds.Count < MaxCapacity)
    ///   3. CanWorkInZone(order.ZoneId) — futár zónalistájában szerepel
    ///
    /// KIVÁLASZTÁS: Dijkstra alapján a legközelebbi alkalmas futár.
    /// Ha ugyanolyan távolságban van több is → az első a listában nyer.
    /// </summary>
    public Courier? AssignToNearest(
        DeliveryOrder order,
        List<Courier> allCouriers)
    {
        _logger.LogInformation(
            "Greedy hozzárendelés: {OrderNumber} ({CustomerName}, Zóna {ZoneId})...",
            order.OrderNumber, order.CustomerName, order.ZoneId);

        // Szűrés: ki kaphat rendelést?
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

        _logger.LogDebug(
            "{Count} alkalmas futár a Zóna {ZoneId} rendeléshez",
            eligibleCouriers.Count, order.ZoneId);

        // Legközelebbi keresése Dijkstrával
        int orderNodeId = FindNearestNodeId(order.AddressLocation);

        Courier? bestCourier = null;
        int shortestTime = int.MaxValue;

        foreach (var courier in eligibleCouriers)
        {
            int courierNodeId = FindNearestNodeId(courier.CurrentLocation);
            var (_, travelTime) = _cityGraph.FindShortestPath(courierNodeId, orderNodeId);

            if (travelTime == int.MaxValue)
            {
                _logger.LogDebug("  └ {Name}: nem elérhető (nincs útvonal)", courier.Name);
                continue;
            }

            _logger.LogDebug(
                "  └ {Name} [{Status}, {Assigned}/{Max}]: {Time} perc",
                courier.Name, courier.Status,
                courier.AssignedOrderIds.Count, courier.MaxCapacity,
                travelTime);

            if (travelTime < shortestTime)
            {
                shortestTime = travelTime;
                bestCourier = courier;
            }
        }

        // Hozzárendelés elvégzése
        if (bestCourier != null)
        {
            order.Status = OrderStatus.Assigned;
            order.AssignedCourierId = bestCourier.Id;
            bestCourier.AssignedOrderIds.Add(order.Id);

            // Available → Busy (ha Busy volt és van helye, státusz marad Busy)
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

    /// <summary>
    /// Több rendelés tömeges hozzárendelése.
    ///
    /// MOST MÁR: egy futár MaxCapacity-ig több rendelést is kaphat!
    /// Például MaxCapacity=3, 5 futár, 15 rendelés esetén:
    ///   - Körönként mindenki kap 1-et amíg van szabad hely
    ///   - Összesen 15 rendelés kiosztható (5 × 3 = 15)
    /// </summary>
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
            {
                _logger.LogDebug(
                    "Kihagyva (nem Pending): {OrderNumber} [{Status}]",
                    order.OrderNumber, order.Status);
                continue;
            }

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

    /// <summary>
    /// Dinamikus újrahozzárendelés — futár visszaér a warehouse-ba,
    /// és feltölti a csomagjait a szabad helyekre (max MaxCapacity-ig).
    ///
    /// FOLYAMAT A SZIMULÁCIÓBAN:
    ///   1. Futár kézbesít, AssignedOrderIds.Count csökken
    ///   2. Ha üres → visszamegy a legközelebbi SAJÁT ZÓNÁS warehouse-ba
    ///   3. Ott meghívjuk AssignNextBatch()-t
    ///   4. Visszakapja az új rendeléseket, kimegy velük
    ///
    /// KÜLÖNBSÉG AssignToNearest-től:
    ///   - Nem egy rendelést rendel hozzá, hanem annyit, amennyi fér
    ///   - A futár már a warehouse-nál van (nem kell odamenni)
    ///   - Csak a saját zónájában lévő pending rendelésekből válogat
    /// </summary>
    /// <param name="courier">A visszatért futár (már a warehouse-nál van)</param>
    /// <param name="pendingOrders">Az összes még ki nem osztott rendelés</param>
    /// <returns>Az újonnan hozzárendelt rendelések listája</returns>
    public List<DeliveryOrder> AssignNextBatch(
        Courier courier,
        List<DeliveryOrder> pendingOrders)
    {
        _logger.LogInformation(
            "Új köteg hozzárendelése: {CourierName} " +
            "(szabad helyek: {Remaining}/{Max})",
            courier.Name, courier.RemainingCapacity, courier.MaxCapacity);

        // Csak a futár zónájában lévő, még ki nem osztott rendelések
        var eligibleOrders = pendingOrders
            .Where(o =>
                o.Status == OrderStatus.Pending
                && courier.CanWorkInZone(o.ZoneId))
            .ToList();

        if (eligibleOrders.Count == 0)
        {
            _logger.LogInformation(
                "{CourierName}: nincs várakozó rendelés a zónáiban [{Zones}]",
                courier.Name,
                string.Join(", ", courier.AssignedZoneIds));
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
            "{CourierName}: {Count} új rendelés hozzárendelve " +
            "(kapacitás most: {Current}/{Max})",
            courier.Name, newlyAssigned.Count,
            courier.AssignedOrderIds.Count, courier.MaxCapacity);

        return newlyAssigned;
    }

    // ====================================================
    // Segédmetódus
    // ====================================================

    /// <summary>
    /// A koordinátához legközelebbi gráf-csúcs ID-ja.
    /// Ez az EGYETLEN hely ahol Euklideszi távolságot számolunk.
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
