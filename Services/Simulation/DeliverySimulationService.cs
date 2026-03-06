namespace package_delivery_simulator_console_app.Services.Simulation;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator_console_app.Infrastructure.Database;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Loaders;
using package_delivery_simulator_console_app.Services.Interfaces;

public class DeliverySimulationService : IDeliverySimulationService
{
    // ====================================================
    // FÜGGŐSÉGEK
    // ====================================================

    private readonly ICityGraph _cityGraph;
    private readonly IWarehouseService _warehouseService;
    private readonly ILogger<DeliverySimulationService> _logger;

    /// <summary>
    /// Adatbázis inicializáló — a LoadCouriersAsync / LoadOrdersAsync
    /// delegált metódusokhoz kell, hogy a loaderek megkapják a
    /// connection string-et.
    /// </summary>
    private readonly DatabaseInitializer _dbInitializer;

    // ====================================================
    // KONFIGURÁCIÓ
    // ====================================================

    private const int SimulationStepDelayMs = 200;
    private const double DelayThreshold = 1.2;

    // ====================================================
    // KONSTRUKTOR
    // ====================================================

    public DeliverySimulationService(
        ICityGraph cityGraph,
        IWarehouseService warehouseService,
        ILogger<DeliverySimulationService> logger,
        DatabaseInitializer dbInitializer)
    {
        _cityGraph        = cityGraph;
        _warehouseService = warehouseService;
        _logger           = logger;
        _dbInitializer    = dbInitializer;
    }

    // ====================================================
    // INTERFÉSZ METÓDUSOK — betöltés és hozzárendelés
    // ====================================================

    public async Task<List<Courier>> LoadCouriersAsync(
        CancellationToken cancellationToken = default)
    {
        var loader = new CourierLoader(
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<CourierLoader>.Instance,
            _dbInitializer);   // <-- most már átadjuk

        return await loader.LoadAsync(cancellationToken);
    }

    public async Task<List<DeliveryOrder>> LoadOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var loader = new OrderLoader(
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<OrderLoader>.Instance,
            _dbInitializer);   // <-- most már átadjuk

        return await loader.LoadAsync(cancellationToken);
    }

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

    public async Task<SimulationResult> SimulateDeliveryAsync(
        Courier courier,
        DeliveryOrder order,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "🚚 Szimuláció indul: {CourierName} → {OrderNumber} ({CustomerName})",
            courier.Name, order.OrderNumber, order.CustomerName);

        int totalActualTime = 0;

        // ---- 1. Futár pozíciója → gráf csúcs ----
        int courierStartNodeId = FindNearestNodeId(courier.CurrentLocation);

        _logger.LogDebug(
            "Futár kiindulópontja: Node {Id} ({Name})",
            courierStartNodeId,
            _cityGraph.GetNode(courierStartNodeId)?.Name ?? "?");

        // ---- 2. Raktár megkeresése (zóna alapján, soha nem cache-ből) ----
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
                    bestWarehouse  = wh;
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

        // ---- 3. Futár bemegy a raktárba ----
        if (courierStartNodeId != warehouseNodeId)
        {
            _logger.LogInformation("🏃 {CourierName} megy a raktárba...", courier.Name);

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

        // ---- 4. Rendelés státusz: InTransit ----
        order.Status = OrderStatus.InTransit;

        // ---- 5. Kézbesítési cím → gráf csúcs ----
        int deliveryNodeId = FindNearestNodeId(order.AddressLocation);

        _logger.LogDebug(
            "Kézbesítési cím csúcsa: Node {Id} ({Name})",
            deliveryNodeId,
            _cityGraph.GetNode(deliveryNodeId)?.Name ?? "?");

        // ---- 6. Ideális idő (forgalom nélkül, teljes út) ----
        int idealWarehouseTime = _cityGraph.CalculateIdealTime(courierStartNodeId, warehouseNodeId);
        int idealDeliveryTime  = _cityGraph.CalculateIdealTime(warehouseNodeId, deliveryNodeId);
        int idealTime          = idealWarehouseTime + idealDeliveryTime;
        order.IdealDeliveryTimeMinutes = idealTime;

        _logger.LogInformation(
            "⏱️  Ideális kézbesítési idő (forgalom nélkül): {Time} perc " +
            "(raktárhoz: {WH} + kézbesítés: {Del})",
            idealTime, idealWarehouseTime, idealDeliveryTime);

        // ---- 7. Futár a kézbesítési helyszínre megy ----
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

        // ---- 8. Kézbesítés sikeres ----
        order.Status    = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.Now;
        courier.CurrentLocation        = _cityGraph.GetNode(deliveryNodeId)!.Location;
        courier.CurrentWarehouseNodeId = null;

        courier.TotalDeliveriesCompleted++;
        courier.TotalDeliveryTimeMinutes += totalActualTime;

        _logger.LogInformation(
            "✅ Kézbesítve: {OrderNumber} → {CustomerName} ({Time} perc)",
            courier.Name, order.CustomerName, totalActualTime);

        // ---- 9. Késés detektálás ----
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

        // ---- 10. Futár visszaáll szabaddá ----
        courier.Status = CourierStatus.Available;
        courier.AssignedOrderIds.Remove(order.Id);

        _logger.LogInformation("💤 {CourierName} újra elérhető.", courier.Name);

        return new SimulationResult(
            Success:            true,
            ActualTimeMinutes:  totalActualTime,
            IdealTimeMinutes:   idealTime,
            WasDelayed:         wasDelayed);
    }

    // ====================================================
    // PRIVÁT SEGÉDMETÓDUSOK
    // ====================================================

    private async Task TraversePath(
        Courier courier,
        List<int> path,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int fromId = path[i];
            int toId   = path[i + 1];

            var fromNode = _cityGraph.GetNode(fromId);
            var toNode   = _cityGraph.GetNode(toId);
            var edge     = _cityGraph.GetEdge(fromId, toId);

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

            courier.CurrentLocation = toNode.Location;
            _cityGraph.RegisterCourierMovement(fromId, toId);
            _cityGraph.UpdateTrafficConditions();

            await Task.Delay(
                edge.CurrentTimeMinutes * SimulationStepDelayMs,
                cancellationToken);
        }
    }

    private int FindNearestNodeId(Location location)
    {
        int nearestId    = 0;
        double minDistance = double.MaxValue;

        foreach (var node in _cityGraph.Nodes)
        {
            double dx = node.Location.X - location.X;
            double dy = node.Location.Y - location.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestId   = node.Id;
            }
        }

        return nearestId;
    }
}