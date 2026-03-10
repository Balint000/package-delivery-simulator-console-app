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
///   2. Maradék rendelések → ConcurrentQueue (thread-safe)
///   3. Minden futár PÁRHUZAMOSAN dolgozik (Task.WhenAll):
///        a) Batch sorrendjét NN optimalizálja (futár aktuális pozíciójából)
///        b) Optimális sorrendben kézbesít
///        c) Visszatér → refill a queue-ból → újra optimalizál → folytatja
///   4. OrchestratorResult összegzés
///
/// TPL — HOGYAN MŰKÖDIK?
///   Task.WhenAll elindítja az összes futár loopját egyszerre,
///   és megvárja, amíg MINDENKI végzett.
///   Ez olyan, mint amikor egyszerre küldöd útnak az összes futárt,
///   ahelyett hogy megvárnád az egyiket mielőtt a másik elindul.
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
        //
        // MIÉRT ConcurrentQueue és nem sima List?
        //
        // A List nem thread-safe: ha két futár egyszerre próbál kivenni
        // egy elemet belőle, az adat megsérülhet — race condition.
        //
        // A ConcurrentQueue.TryDequeue() atomikus: garantálja, hogy
        // ugyanazt az elemet két futár soha nem kapja meg egyszerre.
        // Ez az egyetlen "kapocs" a párhuzamos futárloopok között.
        var orderQueue = new ConcurrentQueue<DeliveryOrder>(
            allOrders.Where(o => o.Status == OrderStatus.Pending));

        // Minden rendelést ID → objektum szótárban tartunk.
        // MIÉRT nem kell itt ConcurrentDictionary?
        // Mert ez a szótár csak OLVASÁSRA van — létrehozás után
        // senki sem ír bele. Az olvasás párhuzamosan biztonságos.
        var orderLookup = allOrders.ToDictionary(o => o.Id);

        _logger.LogInformation("Queue: {Count} rendelés vár", orderQueue.Count);

        // ── 3. Futárloopok — PÁRHUZAMOSAN (Task.WhenAll) ─────────
        //
        // SZEKVENCIÁLIS (régi, lassú):
        //   foreach (var courier in couriers)
        //       await RunCourierLoopAsync(courier, ...);
        //   → Kovács végez → Nagy elindul → Tóth elindul → ...
        //   → Az összes futár sorban, egyik megvárja a másikat.
        //
        // PÁRHUZAMOS (új, gyors):
        //   await Task.WhenAll(couriers.Select(...));
        //   → Kovács, Nagy, Tóth, Szabó, Kiss EGYSZERRE indul.
        //   → Mindenki a saját loopján dolgozik, egymástól függetlenül.
        //   → Amikor MINDENKI végzett, megy tovább a program.
        //
        // HOGYAN MŰKÖDIK A Select() ITT?
        //   couriers.Select(courier => RunCourierLoopAsync(courier, ...))
        //   → Minden futárhoz létrehoz egy Task-ot (ígéretet a munkára).
        //   → A Task elindítja az aszinkron munkát, de NEM várja meg.
        //   → Task.WhenAll() összegyűjti az összes ígéretet,
        //     és egyszerre megvárja MINDEGYIKET.
        //
        // MIÉRT BIZTONSÁGOS?
        //   - Minden futárnak saját AssignedOrderIds listája van → nincs megosztás
        //   - A queue ConcurrentQueue → atomikus TryDequeue(), nincs race condition
        //   - A városgráf csak OLVASÁSRA van (FindShortestPath, CalculateIdealTime)
        //     Az utóbbit is javítottuk: már nem írja az _adjacencyMatrix-ot
        //   - Az orderLookup szótár csak olvasott → biztonságos
        _logger.LogInformation("━━━ Futárloopok indítása (párhuzamosan) ━━━");

        await Task.WhenAll(
            couriers
                .Where(c => c.Status != CourierStatus.OffDuty)
                .Select(courier =>
                    RunCourierLoopAsync(courier, orderQueue, orderLookup, cancellationToken)));

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
    /// Egy futár teljes életciklusa — párhuzamosan fut a többivel.
    ///
    /// MIÉRT BIZTONSÁGOS PÁRHUZAMOSAN?
    ///
    ///   Minden futárnak SAJÁT adatai vannak:
    ///     - courier.AssignedOrderIds  → csak ez a futár módosítja
    ///     - courier.CurrentNodeId     → csak ez a futár módosítja
    ///     - courier.TotalDeliveries*  → csak ez a futár módosítja
    ///
    ///   A MEGOSZTOTT erőforrás egyetlen dolog:
    ///     - orderQueue (ConcurrentQueue) → de a TryDequeue() atomikus,
    ///       tehát két futár soha nem veszi ki ugyanazt a rendelést.
    ///
    ///   A cityGraph és orderLookup CSAK OLVASÁSRA van — biztonságos.
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
            // MIÉRT .ToList()?
            //   A SimulateDeliveryAsync kézbesítés végén eltávolítja
            //   a rendelést az AssignedOrderIds-ból. Ha közvetlenül
            //   iterálnánk rajta, "collection modified" kivételt kapnánk.
            //   A .ToList() pillanatkép — biztonságos iterálható másolat.
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
            // A futár jelenlegi pozíciójából optimalizálja a sorrendet.
            // Thread-safe: saját currentBatch listán és a cityGraph-on
            // (csak olvas Dijkstrával) dolgozik — más futárokat nem érinti.
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
    /// THREAD-SAFETY:
    ///   TryDequeue() atomikus — ha két futár egyszerre hívja,
    ///   mindkettő más elemet kap vissza. Nem kell lock.
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
