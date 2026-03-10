namespace package_delivery_simulator_console_app.Services.Simulation;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Presentation.Interfaces;
using package_delivery_simulator_console_app.Services.Interfaces;

/// <summary>
/// Egy futár kézbesítési körének szimulációja.
///
/// FELELŐSSÉG (és CSAK ez):
///   Egy futár + egy rendelés teljes útjának szimulálása:
///   futár pozíció → raktár → csomag felvétel → kézbesítési cím
///
/// AMI KIKERÜLT EBBŐL AZ OSZTÁLYBÓL:
///   - Warehouse-választás logika → WarehouseService.FindBestWarehouseForCourier()
///   - Késési értesítés logika    → INotificationService.NotifyDelay()
///   - LoadCouriersAsync / LoadOrdersAsync → CourierLoader / OrderLoader
///   - AssignOrderToNearestCourier → GreedyAssignmentService
///
/// FÜGGŐSÉGEK:
///   ICityGraph           — Dijkstra + útvonal bejárás
///   IWarehouseService    — legjobb warehouse meghatározása a futárhoz
///   INotificationService — késési értesítés küldése
///
/// ÚJ a korábbi verzióhoz képest:
///   ILiveConsoleRenderer injection — a szimuláció kulcspontjain
///   frissíti a futár státuszát és eseményeket naplóz.
///   Ha null a renderer (pl. teszteléskor), minden csendben fut.
/// </summary>
public class DeliverySimulationService : IDeliverySimulationService
{
    // ── Függőségek ───────────────────────────────────────────────
    private readonly ICityGraph _cityGraph;
    private readonly IWarehouseService _warehouseService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DeliverySimulationService> _logger;

    /// <summary>
    /// Opcionális renderer — ha null, nincs élő UI.
    /// Null-safe hívásokkal használjuk: _renderer?.LogEvent(...)
    /// </summary>
    private readonly ILiveConsoleRenderer? _renderer;

    // ── Konstansok ───────────────────────────────────────────────

    /// <summary>
    /// Szimulációs lépés késleltetése milliszekundumban.
    /// Egy él bejárásakor várunk edge.CurrentTimeMinutes * ez sok ms-t.
    /// </summary>
    private const int SimulationStepDelayMs = 200;

    /// <summary>
    /// Késési küszöb: ha a tényleges idő > ideális * 1.05, késésnek számít.
    /// Azaz 5%-os tolerancia van.
    /// </summary>
    private const double DelayThreshold = 1.05;

    // ── Konstruktor ──────────────────────────────────────────────
    public DeliverySimulationService(
        ICityGraph cityGraph,
        IWarehouseService warehouseService,
        INotificationService notificationService,
        ILogger<DeliverySimulationService> logger,
        ILiveConsoleRenderer? renderer = null)   // ← opcionális, alapból null
    {
        _cityGraph = cityGraph;
        _warehouseService = warehouseService;
        _notificationService = notificationService;
        _logger = logger;
        _renderer = renderer;
    }

    // ────────────────────────────────────────────────────────────
    // INTERFÉSZ — AssignOrderToNearestCourier (delegált)
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Delegál a GreedyAssignmentService-hez.
    /// Az IDeliverySimulationService interfész írja elő, de a munka
    /// a GreedyAssignmentService-ben történik.
    /// </summary>
    public Courier? AssignOrderToNearestCourier(
        DeliveryOrder order,
        List<Courier> availableCouriers)
    {
        var svc = new Assignment.GreedyAssignmentService(
            _cityGraph,
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<Assignment.GreedyAssignmentService>.Instance);
        return svc.AssignToNearest(order, availableCouriers);
    }

    // ────────────────────────────────────────────────────────────
    // FŐ SZIMULÁCIÓ
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Egy futár + egy rendelés teljes kézbesítési körének szimulációja.
    ///
    /// LÉPÉSEK:
    ///   1. Warehouse meghatározása (WarehouseService dönt)
    ///   2. Futár bemegy a raktárba (ha nem ott van)
    ///   3. Csomag felvétel
    ///   4. Ideális idő kiszámítása (forgalom nélkül, teljes út)
    ///   5. Kézbesítési útvonal bejárása
    ///   6. Kézbesítés sikeres
    ///   7. Késés detektálás + értesítés küldése ha késett (NotificationService végzi)
    ///   8. Futár státusz visszaállítása
    ///
    /// Minden lépésnél a renderer frissíti az élő UI-t (ha be van kötve).
    /// </summary>
    public async Task<SimulationResult> SimulateDeliveryAsync(
        Courier courier,
        DeliveryOrder order,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "🚚 Szimuláció indul: {CourierName} → {OrderNumber} ({CustomerName})",
            courier.Name, order.OrderNumber, order.CustomerName);

        int totalActualTime = 0;

        // ── 1. Warehouse meghatározása ──────────────────────────
        // A WarehouseService dönt: saját zóna → Dijkstra legközelebbi → fallback
        var bestWarehouse = _warehouseService.FindBestWarehouseForCourier(courier);

        if (bestWarehouse == null)
        {
            _logger.LogError(
                "Nem található warehouse {CourierName} futárhoz ({OrderNumber})",
                courier.Name, order.OrderNumber);
            return new SimulationResult(false, 0, 0, false);
        }

        int warehouseNodeId = bestWarehouse.Id;

        _logger.LogInformation(
            "📦 Csomagfelvételi raktár: {WName} (Node {WId})",
            bestWarehouse.Name, warehouseNodeId);

        // ── 2. Futár bemegy a raktárba (ha nem ott van) ─────────
        int courierStartNodeId = courier.CurrentNodeId;

        if (courierStartNodeId != warehouseNodeId)
        {
            _logger.LogInformation(
                "🏃 {CourierName} megy a raktárba: Node {From} → Node {To}",
                courier.Name, courierStartNodeId, warehouseNodeId);

            // ── UI: futár mozog a raktár felé ───────────────────
            _renderer?.UpdateCourierStatus(
                courierId: courier.Id,
                courierName: courier.Name,
                status: "moving",
                currentLocation: _cityGraph.GetNode(courierStartNodeId)?.Name ?? "?",
                targetLocation: bestWarehouse.Name,
                completedDeliveries: courier.TotalDeliveriesCompleted);

            _renderer?.LogEvent(
                "moving",
                $"{courier.Name} → raktárba: {bestWarehouse.Name}");

            var (warehousePath, warehouseTime) =
                _cityGraph.FindShortestPath(courierStartNodeId, warehouseNodeId);

            if (warehousePath.Count == 0)
            {
                _logger.LogError(
                    "Raktár nem elérhető! WH Node: {WId}, Futár Node: {CId}",
                    warehouseNodeId, courierStartNodeId);
                return new SimulationResult(false, 0, 0, false);
            }

            await TraversePath(courier, warehousePath, cancellationToken);
            totalActualTime += warehouseTime;
        }

        // ── 3. Csomag felvétel ──────────────────────────────────
        courier.CurrentWarehouseNodeId = warehouseNodeId;
        order.Status = OrderStatus.InTransit;

        // ── UI: csomag felvétel ──────────────────────────────────
        _renderer?.UpdateCourierStatus(
            courierId: courier.Id,
            courierName: courier.Name,
            status: "loading",
            currentLocation: bestWarehouse.Name,
            targetLocation: order.AddressText,
            completedDeliveries: courier.TotalDeliveriesCompleted);

        _renderer?.LogEvent(
            "pickup",
            $"{courier.Name} felvette: {order.OrderNumber} ({order.CustomerName})");

        _logger.LogInformation(
            "📦 {CourierName} felvette a csomagot a raktárból.", courier.Name);

        // ── 4. Ideális idő (forgalom nélkül, teljes út) ─────────
        // Teljes út: futár kiindulás → raktár → kézbesítési cím
        int idealWarehouseTime =
            _cityGraph.CalculateIdealTime(courierStartNodeId, warehouseNodeId);
        int idealDeliveryTime =
            _cityGraph.CalculateIdealTime(warehouseNodeId, order.AddressNodeId);
        int idealTime = idealWarehouseTime + idealDeliveryTime;

        order.IdealDeliveryTimeMinutes = idealTime;

        _logger.LogInformation(
            "⏱️  Ideális kézbesítési idő (forgalom nélkül): {Total} perc " +
            "(raktárhoz: {WH} + kézbesítés: {Del})",
            idealTime, idealWarehouseTime, idealDeliveryTime);

        // ── 5. Kézbesítési útvonal bejárása ─────────────────────
        var deliveryNode = _cityGraph.GetNode(order.AddressNodeId);

        // ── UI: futár úton a kézbesítési cím felé ───────────────
        _renderer?.UpdateCourierStatus(
            courierId: courier.Id,
            courierName: courier.Name,
            status: "moving",
            currentLocation: bestWarehouse.Name,
            targetLocation: deliveryNode?.Name ?? order.AddressText,
            completedDeliveries: courier.TotalDeliveriesCompleted,
            estimatedTimeMinutes: idealDeliveryTime);

        _logger.LogInformation(
            "🚗 {CourierName} indul: {Address}",
            courier.Name, order.AddressText);

        var (deliveryPath, deliveryTime) =
            _cityGraph.FindShortestPath(warehouseNodeId, order.AddressNodeId);

        if (deliveryPath.Count == 0)
        {
            _logger.LogError(
                "Kézbesítési cím nem elérhető! Node: {Id}", order.AddressNodeId);
            return new SimulationResult(false, totalActualTime, idealTime, false);
        }

        await TraversePath(courier, deliveryPath, cancellationToken);
        totalActualTime += deliveryTime;
        order.ActualDeliveryTimeMinutes = deliveryTime;

        // ── 6. Kézbesítés sikeres ────────────────────────────────
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.Now;
        courier.CurrentNodeId = order.AddressNodeId;
        courier.CurrentWarehouseNodeId = null;
        courier.TotalDeliveriesCompleted++;
        courier.TotalDeliveryTimeMinutes += totalActualTime;

        _logger.LogInformation(
            "✅ Kézbesítve: {CourierName} → {CustomerName} ({Time} perc)",
            courier.Name, order.CustomerName, totalActualTime);

        // ── 7. Késés detektálás + értesítés ─────────────────────
        bool wasDelayed = totalActualTime > idealTime * DelayThreshold;

        if (wasDelayed)
        {
            int delayMinutes = totalActualTime - idealTime;
            order.WasDelayed = true;
            courier.TotalDelayedDeliveries++;

            _logger.LogWarning(
                "⚠️  KÉSÉS: {OrderNumber} +{Delay} perc " +
                "(tényleges: {Actual}, ideális: {Ideal})",
                order.OrderNumber, delayMinutes, totalActualTime, idealTime);

            // Az értesítés küldése a NotificationService felelőssége
            // (idempotens: ha már értesítve volt, nem küld újra)
            _notificationService.NotifyDelay(order, delayMinutes);

            // ── UI: késett kézbesítés ────────────────────────────
            _renderer?.LogEvent(
                "delay",
                $"{courier.Name} → {order.CustomerName} ({order.OrderNumber}) " +
                $"+{delayMinutes} perc késés");
        }
        else
        {
            _logger.LogInformation(
                "🟢 Időben kézbesítve! {Time} perc (ideális: {Ideal} perc)",
                totalActualTime, idealTime);

            // ── UI: sikeres kézbesítés ───────────────────────────
            _renderer?.LogEvent(
                "delivery",
                $"{courier.Name} → {order.CustomerName} ({order.OrderNumber}) {totalActualTime} perc");
        }

        // ── 8. Futár visszaáll Available státuszra ───────────────
        courier.Status = CourierStatus.Available;
        courier.AssignedOrderIds.Remove(order.Id);

        // ── UI: futár státusz frissítés (várakozik a következőre) ─
        _renderer?.UpdateCourierStatus(
            courierId: courier.Id,
            courierName: courier.Name,
            status: "idle",
            currentLocation: deliveryNode?.Name ?? "?",
            completedDeliveries: courier.TotalDeliveriesCompleted);

        _logger.LogInformation("💤 {CourierName} újra elérhető.", courier.Name);

        return new SimulationResult(
            Success: true,
            ActualTimeMinutes: totalActualTime,
            IdealTimeMinutes: idealTime,
            WasDelayed: wasDelayed);
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — TraversePath
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Szimulált mozgás egy útvonal mentén, node-ról node-ra.
    ///
    /// Minden él bejárásakor:
    ///   - Futár pozíciója frissül (node ID)
    ///   - Forgalom változik (UpdateTrafficConditions)
    ///   - Késleltetés szimulál (edge.CurrentTimeMinutes * SimulationStepDelayMs ms)
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
                _logger.LogWarning("Hiányzó él a útvonalon: {From} → {To}", fromId, toId);
                continue;
            }

            _logger.LogDebug(
                "  ↪ {From} → {To} ({Time} perc, {Traffic:F2}x forgalom)",
                fromNode.Name, toNode.Name,
                edge.CurrentTimeMinutes, edge.TrafficMultiplier);

            // Futár pozíciója node ID-val frissül
            courier.CurrentNodeId = toId;

            _cityGraph.RegisterCourierMovement(fromId, toId);
            _cityGraph.UpdateTrafficConditions();

            await Task.Delay(
                edge.CurrentTimeMinutes * SimulationStepDelayMs,
                cancellationToken);
        }
    }
}
