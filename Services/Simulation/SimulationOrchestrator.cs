// ============================================================
// SimulationOrchestrator.cs
// ============================================================
// Felelőssége: A teljes szimuláció vezérlése.
//
// MIT CSINÁL:
//   1. Initial assignment: GreedyAssignmentService.AssignAll()
//      → minden futár MaxCapacity-ig rendelést kap
//   2. Queue építés: a maradék Pending rendelések ConcurrentQueue-ba kerülnek
//   3. Futárloopok indítása (most szekvenciális, TPL-lel párhuzamos lesz)
//   4. RunCourierLoopAsync: futár simul → visszaér → refill → simul → ...
//   5. Összesítő OrchestratorResult visszaadása
//
// TPL-RE VALÓ FELKÉSZÍTÉS:
//   A ConcurrentQueue thread-safe — párhuzamos futárok egyszerre vehetnek
//   ki belőle rendeléseket. A foreach → Task.WhenAll csere az egyetlen lépés.
//
// ARCHITEKTÚRA:
//   Program.cs
//     └─ SimulationOrchestrator.RunAsync()
//           ├─ GreedyAssignmentService.AssignAll()   [initial batch]
//           ├─ ConcurrentQueue<DeliveryOrder>         [maradék rendelések]
//           └─ RunCourierLoopAsync() × N futár
//                 ├─ DeliverySimulationService.SimulateDeliveryAsync() × batch
//                 └─ RefillCourier() [visszatér → újratölt a queue-ból]
// ============================================================

namespace package_delivery_simulator_console_app.Services.Simulation;

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Presentation.Interfaces;
using package_delivery_simulator_console_app.Services.Assignment;

/// <summary>
/// A teljes szimuláció orchestrátora.
/// Implementálja az ISimulationOrchestrator interfészt.
/// </summary>
public class SimulationOrchestrator : ISimulationOrchestrator
{
    // ====================================================
    // FÜGGŐSÉGEK
    // ====================================================

    /// <summary>
    /// Greedy hozzárendelés — initial batch + zóna-alapú szűrés.
    /// </summary>
    private readonly GreedyAssignmentService _assignmentService;

    /// <summary>
    /// Kézbesítési szimuláció — egy futár + egy rendelés.
    /// </summary>
    private readonly DeliverySimulationService _simulationService;

    /// <summary>
    /// Logger a strukturált naplózáshoz.
    /// </summary>
    private readonly ILogger<SimulationOrchestrator> _logger;

    // ====================================================
    // KONSTRUKTOR
    // ====================================================

    /// <summary>
    /// SimulationOrchestrator létrehozása.
    /// </summary>
    public SimulationOrchestrator(
        GreedyAssignmentService assignmentService,
        DeliverySimulationService simulationService,
        ILogger<SimulationOrchestrator> logger)
    {
        _assignmentService = assignmentService;
        _simulationService = simulationService;
        _logger = logger;
    }

    // ====================================================
    // FŐ BELÉPÉSI PONT
    // ====================================================

    /// <summary>
    /// Teljes szimuláció futtatása.
    ///
    /// FOLYAMAT:
    ///   1. AssignAll → minden futár MaxCapacity-ig feltöltve (greedy, zóna-alapú)
    ///   2. Maradék Pending rendelések → ConcurrentQueue
    ///   3. Minden futár kap egy RunCourierLoopAsync-ot (most szekvenciális)
    ///   4. OrchestratorResult összegzés
    /// </summary>
    public async Task<OrchestratorResult> RunAsync(
        List<Courier> couriers,
        List<DeliveryOrder> allOrders,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // ---- LÉPÉS 1: Initial batch assignment ----
        // GreedyAssignmentService.AssignAll() zóna-szűréssel és kapacitás-ellenőrzéssel
        // tölti fel minden futárt MaxCapacity-ig.
        _logger.LogInformation(
            "━━━ Initial batch assignment: {Couriers} futár, {Orders} rendelés ━━━",
            couriers.Count, allOrders.Count);

        _assignmentService.AssignAll(allOrders, couriers);

        int initiallyAssigned = allOrders.Count(o => o.Status == OrderStatus.Assigned);
        int initiallyPending = allOrders.Count(o => o.Status == OrderStatus.Pending);

        _logger.LogInformation(
            "Initial assignment kész: {Assigned} hozzárendelve, {Pending} sorban vár",
            initiallyAssigned, initiallyPending);

        // ---- LÉPÉS 2: Queue feltöltése a maradék Pending rendelésekkel ----
        //
        // ConcurrentQueue MIÉRT KELL:
        //   - Thread-safe: TPL-lel több futár párhuzamosan vehet ki belőle rendelést
        //   - TryDequeue atomikus: nincs versenyhelyzet
        //   - Elveszti a "Pending szűrés" szükségességét, mert csak Pending kerül bele
        //
        // MEGJEGYZÉS a demo adatokhoz:
        //   5 futár × MaxCapacity=3 = 15 hely, 15 rendelés → queue üres az initial után.
        //   Ez HELYES működés! A queue-logika akkor demonstrálható, ha
        //   több rendelés van mint az initial kapacitás összege.
        //   Az architektúra ettől függetlenül korrekt és TPL-re kész.
        var orderQueue = new ConcurrentQueue<DeliveryOrder>(
            allOrders.Where(o => o.Status == OrderStatus.Pending));

        // ID → Order lookup: RunCourierLoopAsync-ban szükséges
        // (a courier csak ID-kat tárol, az objektumot innen kérjük le)
        var orderLookup = allOrders.ToDictionary(o => o.Id);

        _logger.LogInformation(
            "Queue inicializálva: {Count} rendelés vár hozzárendelésre",
            orderQueue.Count);

        // ---- LÉPÉS 3: Futárloopok indítása ----
        //
        // MOST: szekvenciális foreach
        // TPL-LEL: foreach helyett Task.WhenAll(couriers.Select(RunCourierLoopAsync))
        //
        // Minden futár dolgozik, amíg:
        //   a) van hozzárendelt rendelése, VAGY
        //   b) a queue-ban van a zónájába eső rendelés
        _logger.LogInformation("━━━ Futárloopok indítása ━━━");

        foreach (var courier in couriers)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // OffDuty futárt kihagyjuk
            if (courier.Status == CourierStatus.OffDuty)
            {
                _logger.LogInformation(
                    "⏭️  {Courier} kihagyva (OffDuty)", courier.Name);
                continue;
            }

            await RunCourierLoopAsync(
                courier, orderQueue, orderLookup, cancellationToken);
        }

        // ---- LÉPÉS 4: Összesítés ----
        sw.Stop();

        int delivered = allOrders.Count(o => o.Status == OrderStatus.Delivered);
        int delayed = allOrders.Count(o => o.WasDelayed);
        int unassigned = allOrders.Count(o => o.Status == OrderStatus.Pending);
        int failed = allOrders.Count(o =>
            o.Status != OrderStatus.Delivered &&
            o.Status != OrderStatus.Pending);

        _logger.LogInformation(
            "━━━ Szimuláció vége: {Delivered}/{Total} kézbesítve, " +
            "{Delayed} késés, {Failed} hiba, {Time:F1}s ━━━",
            delivered, allOrders.Count, delayed, failed,
            sw.Elapsed.TotalSeconds);

        return new OrchestratorResult(
            TotalOrders: allOrders.Count,
            Delivered: delivered,
            Delayed: delayed,
            Failed: failed,
            Unassigned: unassigned,
            WallClockTime: sw.Elapsed);
    }

    // ====================================================
    // FUTÁR LOOP
    // ====================================================

    /// <summary>
    /// Egy futár teljes életciklusa a szimulációban.
    ///
    /// CIKLUS:
    ///   1. Van-e hozzárendelt rendelés? Ha nincs → próbál tölteni a queue-ból
    ///   2. Batch szimulálása (minden hozzárendelt rendelés egymás után)
    ///   3. Batch vége → RefillCourier a queue-ból
    ///   4. Ha nincs több rendelés sehol → loop vége
    ///
    /// INVARIÁNS:
    ///   A ciklus mindig lefut, ha a futárnak van hozzárendelt rendelése,
    ///   VAGY a queue-ban van a zónájába eső rendelés.
    /// </summary>
    private async Task RunCourierLoopAsync(
        Courier courier,
        ConcurrentQueue<DeliveryOrder> orderQueue,
        Dictionary<int, DeliveryOrder> orderLookup,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "🚀 {Courier} loop indul — {Count} rendeléssel, queue: {QueueCount} elem",
            courier.Name, courier.AssignedOrderIds.Count, orderQueue.Count);

        int round = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ---- A: Snapshot a jelenlegi batch-ről ----
            //
            // MIÉRT SNAPSHOT (.ToList())?
            // SimulateDeliveryAsync a futár.AssignedOrderIds-ból ELTÁVOLÍTJA
            // a rendelést kézbesítés után. Ha közvetlenül iterálnánk, az
            // "collection modified during iteration" hibát okozna.
            var currentBatch = courier.AssignedOrderIds
                .ToList()
                .Select(id => orderLookup[id])
                .ToList();

            // ---- B: Ha nincs mit szimulálni → próbál tölteni ----
            if (currentBatch.Count == 0)
            {
                _logger.LogInformation(
                    "{Courier}: üres batch, refill kísérlet (queue: {Count})",
                    courier.Name, orderQueue.Count);

                var refilled = RefillCourier(courier, orderQueue, orderLookup);

                if (refilled.Count == 0)
                {
                    // Sem a queue-ban, sem hozzárendelve nincs semmi → vége
                    _logger.LogInformation(
                        "{Courier}: nincs több rendelés a zónáiban. Loop vége.",
                        courier.Name);
                    break;
                }

                currentBatch = refilled;
            }

            round++;
            _logger.LogInformation(
                "🔄 {Courier} — {Round}. kör, {Count} rendelés a batchben",
                courier.Name, round, currentBatch.Count);

            // ---- C: Batch szimulálása ----
            foreach (var order in currentBatch)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // SimulateDeliveryAsync elvégzi:
                //   futár → raktár → csomag felvétel → kézbesítés
                // A végén: courier.AssignedOrderIds.Remove(order.Id),
                //           courier.Status = Available
                await _simulationService.SimulateDeliveryAsync(
                    courier, order, cancellationToken);
            }

            // ---- D: Batch kész → refill a queue-ból ----
            if (!orderQueue.IsEmpty)
            {
                var newOrders = RefillCourier(courier, orderQueue, orderLookup);

                if (newOrders.Count > 0)
                {
                    _logger.LogInformation(
                        "📥 {Courier}: {Count} új rendelés a queue-ból",
                        courier.Name, newOrders.Count);
                }
            }

            // ---- E: Kilépési feltétel ellenőrzése ----
            // Ha sem a futárnak, sem a queue-nak nincs már semmi → vége
            if (courier.AssignedOrderIds.Count == 0 && orderQueue.IsEmpty)
            {
                _logger.LogInformation(
                    "{Courier}: queue kiürült, loop vége.", courier.Name);
                break;
            }
        }

        _logger.LogInformation(
            "✅ {Courier} loop kész — {Total} kézbesítés, {Rounds} kör",
            courier.Name, courier.TotalDeliveriesCompleted, round);
    }

    // ====================================================
    // REFILL — queue-ból tölt a futárba
    // ====================================================

    /// <summary>
    /// Futár újratöltése a ConcurrentQueue-ból.
    ///
    /// SZABÁLYOK:
    ///   - Csak courier.RemainingCapacity darabot vesz fel (nem töltjük túl)
    ///   - Csak a futár zónájába eső rendeléseket veszi fel
    ///   - Más zónás rendeléseket visszateszi a queue-ba (re-enqueue)
    ///   - maxTries = queue mérete az induláskor → nincs végtelen ciklus
    ///
    /// TPL MEGJEGYZÉS:
    ///   ConcurrentQueue.TryDequeue() atomikus → thread-safe párhuzamos futárokkal.
    ///   A "skip és re-enqueue" minta elfogadott ConcurrentQueue-val, de TPL-lel
    ///   érdemes lehet ConcurrentBag vagy priority queue felé mozdulni.
    /// </summary>
    private List<DeliveryOrder> RefillCourier(
        Courier courier,
        ConcurrentQueue<DeliveryOrder> orderQueue,
        Dictionary<int, DeliveryOrder> orderLookup)
    {
        var assigned = new List<DeliveryOrder>();
        var skipped = new List<DeliveryOrder>(); // Nem megfelelő zóna → visszatesszük

        int slotsNeeded = courier.RemainingCapacity;

        // maxTries: legfeljebb annyiszor próbálunk dequeue-t, ahány elem van most.
        // Ez megakadályozza, hogy végtelen ciklusban pörgjünk, ha nincs zóna-egyezés.
        int maxTries = orderQueue.Count;
        int tries = 0;

        while (assigned.Count < slotsNeeded && tries < maxTries)
        {
            if (!orderQueue.TryDequeue(out var order)) break;
            tries++;

            if (courier.CanWorkInZone(order.ZoneId))
            {
                // Hozzárendelés
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
                // Rossz zóna → félre, majd vissza a queue-ba
                skipped.Add(order);

                _logger.LogDebug(
                    "  ↩️  {Order} (Zóna {Zone}) nem illik {Courier} zónáihoz [{Zones}], visszatesszük",
                    order.OrderNumber, order.ZoneId, courier.Name,
                    string.Join(", ", courier.AssignedZoneIds));
            }
        }

        // Kihagyott rendelések visszakerülnek a queue végére
        // (más futár majd felveheti őket)
        foreach (var o in skipped)
            orderQueue.Enqueue(o);

        if (skipped.Count > 0)
        {
            _logger.LogDebug(
                "{Count} rendelés visszatéve a queue-ba (zóna-ütközés)",
                skipped.Count);
        }

        return assigned;
    }
}
