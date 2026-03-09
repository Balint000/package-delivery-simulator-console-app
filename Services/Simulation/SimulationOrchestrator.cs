namespace package_delivery_simulator_console_app.Services.Simulation;

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Services.Interfaces;
using package_delivery_simulator_console_app.Services.Assignment;
using package_delivery_simulator_console_app.Services.Routing;

/// <summary>
/// A teljes szimuláció orchestrátora — "karmester".
///
/// FELELŐSSÉG:
///   Nem dolgozik maga — vezényli a többi service-t:
///   - GreedyAssignmentService:       rendelések kiosztása futárokhoz
///   - NearestNeighborRouteService:   kézbesítési sorrend optimalizálása
///   - DeliverySimulationService:     egy futár + egy rendelés szimulációja
///
/// FOLYAMAT:
///   1. Initial batch: minden futár MaxCapacity-ig rendelést kap (greedy)
///   2. Maradék rendelések → ConcurrentQueue (TPL-re kész)
///   3. Minden futár saját loop-ban dolgozik:
///        a) Batch sorrendjét NN optimalizálja (futár aktuális pozíciójából)
///        b) Optimális sorrendben kézbesít
///        c) Visszatér → refill a queue-ból → újra optimalizál → folytatja
///   4. OrchestratorResult összegzés
/// </summary>
public class SimulationOrchestrator : ISimulationOrchestrator
{
    // ── Függőségek ───────────────────────────────────────────────
    private readonly GreedyAssignmentService _assignmentService;
    private readonly DeliverySimulationService _simulationService;
    private readonly NearestNeighborRouteService _routeService;
    private readonly ILogger<SimulationOrchestrator> _logger;

    // ── Konstruktor ──────────────────────────────────────────────
    public SimulationOrchestrator(
        GreedyAssignmentService assignmentService,
        DeliverySimulationService simulationService,
        NearestNeighborRouteService routeService,
        ILogger<SimulationOrchestrator> logger)
    {
        _assignmentService = assignmentService;
        _simulationService = simulationService;
        _routeService = routeService;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────
    // FŐ BELÉPÉSI PONT
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Teljes szimuláció futtatása.
    /// </summary>
    public async Task<OrchestratorResult> RunAsync(
        List<Courier> couriers,
        List<DeliveryOrder> allOrders,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // ── 1. Initial batch assignment ──────────────────────────
        _logger.LogInformation(
            "━━━ Initial batch: {Couriers} futár, {Orders} rendelés ━━━",
            couriers.Count, allOrders.Count);

        _assignmentService.AssignAll(allOrders, couriers);

        _logger.LogInformation(
            "Initial kész: {Assigned} hozzárendelve, {Pending} sorban vár",
            allOrders.Count(o => o.Status == OrderStatus.Assigned),
            allOrders.Count(o => o.Status == OrderStatus.Pending));

        // ── 2. Maradék rendelések → ConcurrentQueue ──────────────
        // Thread-safe: TPL-lel több futár párhuzamosan vehet ki belőle.
        var orderQueue = new ConcurrentQueue<DeliveryOrder>(
            allOrders.Where(o => o.Status == OrderStatus.Pending));
        var orderLookup = allOrders.ToDictionary(o => o.Id);

        _logger.LogInformation("Queue: {Count} rendelés vár", orderQueue.Count);

        // ── 3. Futárloopok ───────────────────────────────────────
        // MOST: szekvenciális foreach
        // TPL:  await Task.WhenAll(couriers.Select(c => RunCourierLoopAsync(...)))
        _logger.LogInformation("━━━ Futárloopok indítása ━━━");

        foreach (var courier in couriers)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (courier.Status == CourierStatus.OffDuty)
            {
                _logger.LogInformation("⏭️  {Courier} kihagyva (OffDuty)", courier.Name);
                continue;
            }

            await RunCourierLoopAsync(courier, orderQueue, orderLookup, cancellationToken);
        }

        // ── 4. Összesítés ────────────────────────────────────────
        sw.Stop();

        int delivered = allOrders.Count(o => o.Status == OrderStatus.Delivered);
        int delayed = allOrders.Count(o => o.WasDelayed);
        int unassigned = allOrders.Count(o => o.Status == OrderStatus.Pending);
        int failed = allOrders.Count(o =>
            o.Status != OrderStatus.Delivered &&
            o.Status != OrderStatus.Pending);

        _logger.LogInformation(
            "━━━ Vége: {Del}/{Tot} kézbesítve, {Delay} késés, {Fail} hiba, {T:F1}s ━━━",
            delivered, allOrders.Count, delayed, failed, sw.Elapsed.TotalSeconds);

        return new OrchestratorResult(
            TotalOrders: allOrders.Count,
            Delivered: delivered,
            Delayed: delayed,
            Failed: failed,
            Unassigned: unassigned,
            WallClockTime: sw.Elapsed);
    }

    // ────────────────────────────────────────────────────────────
    // FUTÁR LOOP
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Egy futár teljes életciklusa.
    ///
    /// ÚJ a korábbi verzióhoz képest:
    ///   A batch szimulálása ELŐTT a NearestNeighborRouteService
    ///   optimális sorrendbe rendezi a rendeléseket.
    ///
    ///   Kiindulás: courier.CurrentNodeId (ahol éppen áll — warehouse vagy utolsó cím)
    ///   → legközelebbi rendelés először
    ///   → onnan a következő legközelebbi
    ///   → ...
    /// </summary>
    private async Task RunCourierLoopAsync(
        Courier courier,
        ConcurrentQueue<DeliveryOrder> orderQueue,
        Dictionary<int, DeliveryOrder> orderLookup,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "🚀 {Courier} loop indul — {Count} rendelés, queue: {Q} elem",
            courier.Name, courier.AssignedOrderIds.Count, orderQueue.Count);

        int round = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Snapshot a jelenlegi batch-ről.
            // MIÉRT .ToList()? A SimulateDeliveryAsync kézbesítés után
            // eltávolítja a rendelést az AssignedOrderIds-ból —
            // ha közvetlenül iterálnánk rajta, "collection modified" hibát kapnánk.
            var currentBatch = courier.AssignedOrderIds
                .ToList()
                .Select(id => orderLookup[id])
                .ToList();

            // Ha üres → refill kísérlet a queue-ból
            if (currentBatch.Count == 0)
            {
                var refilled = RefillCourier(courier, orderQueue, orderLookup);
                if (refilled.Count == 0)
                {
                    _logger.LogInformation(
                        "{Courier}: nincs több rendelés. Loop vége.", courier.Name);
                    break;
                }
                currentBatch = refilled;
            }

            round++;

            // ── Nearest Neighbor optimalizálás ───────────────────
            // A futár jelenlegi pozíciójából (CurrentNodeId) optimalizálja
            // a kézbesítési sorrendet. Ez a pozíció az első körben a warehouse,
            // a következő körökben az előző kézbesítés utáni helyzet.
            //
            // Ha csak 1 rendelés van → az NN azonnal visszaadja változatlanul.
            var optimizedBatch = _routeService.OptimizeRoute(
                startNodeId: courier.CurrentNodeId,
                orders: currentBatch);

            _logger.LogInformation(
                "🔄 {Courier} — {R}. kör, {C} rendelés (NN sorrendben)",
                courier.Name, round, optimizedBatch.Count);

            // Batch szimulálása az optimalizált sorrendben
            foreach (var order in optimizedBatch)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await _simulationService.SimulateDeliveryAsync(
                    courier, order, cancellationToken);
            }

            // Batch kész → refill ha van még a queue-ban
            if (!orderQueue.IsEmpty)
            {
                var newOrders = RefillCourier(courier, orderQueue, orderLookup);
                if (newOrders.Count > 0)
                    _logger.LogInformation(
                        "📥 {Courier}: {C} új rendelés a queue-ból",
                        courier.Name, newOrders.Count);
            }

            // Kilépési feltétel
            if (courier.AssignedOrderIds.Count == 0 && orderQueue.IsEmpty)
            {
                _logger.LogInformation("{Courier}: queue kiürült, vége.", courier.Name);
                break;
            }
        }

        _logger.LogInformation(
            "✅ {Courier} kész — {Total} kézbesítés, {R} kör",
            courier.Name, courier.TotalDeliveriesCompleted, round);
    }

    // ────────────────────────────────────────────────────────────
    // REFILL — queue-ból tölt a futárba
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Futár újratöltése a ConcurrentQueue-ból.
    ///
    /// SZABÁLYOK:
    ///   - Max courier.RemainingCapacity rendelést vesz fel
    ///   - Csak a futár zónájába eső rendelések kerülnek rá
    ///   - Más zónás rendeléseket visszateszi a queue végére
    ///   - maxTries = kezdeti queue méret → nem pörög végtelen ciklusban
    /// </summary>
    private List<DeliveryOrder> RefillCourier(
        Courier courier,
        ConcurrentQueue<DeliveryOrder> orderQueue,
        Dictionary<int, DeliveryOrder> orderLookup)
    {
        var assigned = new List<DeliveryOrder>();
        var skipped = new List<DeliveryOrder>();

        int slotsNeeded = courier.RemainingCapacity;
        int maxTries = orderQueue.Count;
        int tries = 0;

        while (assigned.Count < slotsNeeded && tries < maxTries)
        {
            if (!orderQueue.TryDequeue(out var order)) break;
            tries++;

            if (courier.CanWorkInZone(order.ZoneId))
            {
                order.Status = OrderStatus.Assigned;
                order.AssignedCourierId = courier.Id;
                courier.AssignedOrderIds.Add(order.Id);

                if (courier.Status == CourierStatus.Available)
                    courier.Status = CourierStatus.Busy;

                assigned.Add(order);

                _logger.LogInformation(
                    "  📥 {Order} → {Courier} (Zóna {Zone}, queue-ból)",
                    order.OrderNumber, courier.Name, order.ZoneId);
            }
            else
            {
                // Rossz zóna → visszaadjuk, más futár veszi fel
                skipped.Add(order);
            }
        }

        foreach (var o in skipped)
            orderQueue.Enqueue(o);

        if (skipped.Count > 0)
            _logger.LogDebug(
                "{Count} rendelés visszatéve (zóna-ütközés)", skipped.Count);

        return assigned;
    }
}
