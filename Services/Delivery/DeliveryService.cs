using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Services.Interfaces;

namespace package_delivery_simulator.Services.Delivery;

/// <summary>
/// Kézbesítési szolgáltatás - TPL alapú párhuzamos futár szimuláció.
/// Thread-safe gyűjteményekkel dolgozik (ConcurrentBag).
///
/// Felelősség:
/// - Futárok és rendelések kezelése
/// - Párhuzamos futár szimulációk indítása (Task-okkal)
/// - Rendelés-futár hozzárendelés (greedy: legközelebbi futár)
/// - Statisztikák gyűjtése
/// - Késleltetés-értesítés (NotificationService-en keresztül)
///
/// ÚJ: Most már implementálja az IDeliveryService interface-t (DI-hez).
/// ÚJ: Routing és Notification szolgáltatások DI-vel beinjektálva.
/// ÚJ: ILogger használata Console.WriteLine helyett.
/// </summary>
public class DeliveryService : IDeliveryService
{
    // Thread-safe gyűjtemények (több Task is hozzáférhet egyidejűleg)
    private readonly ConcurrentBag<Courier> _couriers;
    private readonly ConcurrentBag<DeliveryOrder> _orders;

    // Gráf modell referencia (útvonal kereséshez)
    // FONTOS: A te gráf osztályodat használd itt!
    // Pl: CityGraph, GraphModel, stb.
    private readonly object _cityGraph;

    // Statisztikák (Interlocked műveletekkel frissítve - thread-safe)
    private int _totalDeliveries = 0;
    private int _totalDelays = 0;

    // ===== DEPENDENCY INJECTION SZOLGÁLTATÁSOK =====
    // Ezeket a konstruktorban kapjuk meg, a Generic Host tölti be őket
    private readonly IRouteOptimizationService _routeOptimization;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DeliveryService> _logger;

    /// <summary>
    /// Konstruktor - inicializálja a service-t DI-vel.
    ///
    /// FONTOS VÁLTOZÁS:
    /// - Már nem csak a cityGraph-ot kapja, hanem routing, notification és logger-t is!
    /// - Ezeket a Generic Host automatikusan beinjektálja.
    /// </summary>
    /// <param name="cityGraph">Város gráf modell (úthálózat)</param>
    /// <param name="routeOptimization">Útvonal-optimalizáló szolgáltatás (greedy)</param>
    /// <param name="notificationService">Értesítési szolgáltatás (késésekhez)</param>
    /// <param name="logger">Logger példány (strukturált naplózás)</param>
    public DeliveryService(
        object cityGraph,
        IRouteOptimizationService routeOptimization,
        INotificationService notificationService,
        ILogger<DeliveryService> logger)
    {
        _cityGraph = cityGraph ?? throw new ArgumentNullException(nameof(cityGraph));
        _routeOptimization = routeOptimization ?? throw new ArgumentNullException(nameof(routeOptimization));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _couriers = new ConcurrentBag<Courier>();
        _orders = new ConcurrentBag<DeliveryOrder>();
    }

    /// <summary>
    /// Futár hozzáadása a szimulációhoz.
    /// </summary>
    public void AddCourier(Courier courier)
    {
        if (courier == null)
            throw new ArgumentNullException(nameof(courier));

        _couriers.Add(courier);
    }

    /// <summary>
    /// Rendelés hozzáadása a szimulációhoz.
    /// </summary>
    public void AddOrder(DeliveryOrder order)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        _orders.Add(order);
    }

    /// <summary>
    /// Összes futár lekérése (read-only snapshot).
    /// </summary>
    public IEnumerable<Courier> GetCouriers() => _couriers.ToList();

    /// <summary>
    /// Összes rendelés lekérése (read-only snapshot).
    /// </summary>
    public IEnumerable<DeliveryOrder> GetOrders() => _orders.ToList();

    /// <summary>
    /// Statisztikák lekérése (thread-safe).
    /// </summary>
    public (int TotalDeliveries, int TotalDelays) GetStatistics()
    {
        // Interlocked.Read biztosítja a thread-safe olvasást
        return (
            Interlocked.CompareExchange(ref _totalDeliveries, 0, 0),
            Interlocked.CompareExchange(ref _totalDelays, 0, 0)
        );
    }

    /// <summary>
    /// PÁRHUZAMOS SZIMULÁCIÓ INDÍTÁSA (TPL).
    ///
    /// Működés:
    /// 1. Minden futárnak létrehoz egy saját Task-ot
    /// 2. Task.Run() - thread pool-ból vesz szálat
    /// 3. Task.WhenAll() - párhuzamosan futnak, várunk mindegyikre
    /// 4. CancellationToken - CTRL+C leállítás támogatás
    /// </summary>
    /// <param name="cancellationToken">Leállítási token (CTRL+C kezeléshez)</param>
    public async Task RunSimulationAsync(CancellationToken cancellationToken)
    {
        // Naplózás: szimuláció indítása
        _logger.LogInformation("🚀 Párhuzamos szimuláció indítása {CourierCount} futárral", _couriers.Count);

        // Futár Task-ok lista (minden futárnak külön Task)
        var courierTasks = new List<Task>();

        // Minden futárnak létrehozunk egy Task-ot
        foreach (var courier in _couriers)
        {
            // Task.Run() = új aszinkron Task a thread pool-ból
            // A lambda kifejezés PÁRHUZAMOSAN fog futni minden futárra!
            var task = Task.Run(async () =>
            {
                // Ez a metódus szimulálja EGY futár munkáját
                await SimulateCourierAsync(courier, cancellationToken);
            }, cancellationToken);

            courierTasks.Add(task);
        }

        // Task.WhenAll = várunk, amíg MINDEN Task befejeződik
        // Párhuzamos végrehajtás: mindegyik Task egyszerre fut!
        try
        {
            await Task.WhenAll(courierTasks);
        }
        catch (OperationCanceledException)
        {
            // Normális leállítás (CTRL+C)
            _logger.LogInformation("⏹️  Szimuláció leállítva (felhasználó által)");
        }
    }

    /// <summary>
    /// EGY FUTÁR SZIMULÁCIÓJA (párhuzamosan fut más futárokkal).
    ///
    /// Ez a metódus egy végtelen ciklusban fut, amíg:
    /// - Van rendelés VAGY
    /// - Meg nem állítják (CancellationToken)
    ///
    /// Lépések:
    /// 1. Keres egy szabad rendelést (GREEDY: legközelebbi)
    /// 2. Hozzárendeli magához
    /// 3. "Utazik" a cím felé (gráfon keresztül)
    /// 4. Kézbesít
    /// 5. Visszamegy a raktárba
    /// 6. Ismétlés
    /// </summary>
    private async Task SimulateCourierAsync(Courier courier, CancellationToken cancellationToken)
    {
        // Futár a raktárban kezd (node 0)
        courier.CurrentNodeId = 0;
        courier.Status = CourierStatus.Available;

        _logger.LogInformation("Futár {CourierName} (ID: {CourierId}) elindult a raktárból", courier.Name, courier.Id);

        // Végtelen ciklus - folyamatosan keres új munkát
        while (!cancellationToken.IsCancellationRequested)
        {
            // 1. KERESÜNK EGY SZABAD RENDELÉST (GREEDY: legközelebbi)
            var availableOrder = FindNearestAvailableOrder(courier);

            if (availableOrder != null)
            {
                // 2. RENDELÉS HOZZÁRENDELÉSE
                AssignOrderToCourier(courier, availableOrder);

                // 3. KÉZBESÍTÉS VÉGREHAJTÁSA
                // TODO: Itt kell majd a gráf algoritmus!
                // await DeliverOrderAsync(courier, availableOrder, cancellationToken);

                // ÁTMENETI MEGOLDÁS: egyszerű várakozás
                _logger.LogDebug(
                    "Futár {CourierName} úton van: {OrderNumber} -> {Address}",
                    courier.Name,
                    availableOrder.OrderNumber,
                    availableOrder.AddressText
                );

                await Task.Delay(5000, cancellationToken); // 5 másodperc "kézbesítés"

                // 4. KÉZBESÍTÉS BEFEJEZÉS
                await CompleteDeliveryAsync(courier, availableOrder);

                // 5. VISSZATÉRÉS A RAKTÁRBA (szintén TODO gráf)
                // await ReturnToWarehouseAsync(courier, cancellationToken);
                courier.CurrentNodeId = 0; // Egyszerűsítve
            }
            else
            {
                // Nincs rendelés -> várunk egy kicsit
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("Futár {CourierName} befejezte a munkát", courier.Name);
    }

    /// <summary>
    /// GREEDY: Legközelebbi szabad rendelés keresése a futár pozíciójához.
    ///
    /// ÚJ LOGIKA:
    /// - Már nem az első Pending order-t adja vissza!
    /// - A RouteOptimizationService-t használja (dependency injection).
    /// - Ez választja ki a legközelebbi rendelést távolság alapján.
    /// </summary>
    private DeliveryOrder? FindNearestAvailableOrder(Courier courier)
    {
        // Szabad (Pending) rendelések, amelyekhez még nincs futár hozzárendelve
        var availableOrders = _orders
            .Where(o => o.Status == OrderStatus.Pending && o.AssignedCourierId == null)
            .ToList();

        // Ha nincs szabad rendelés, térjünk vissza null-lal
        if (!availableOrders.Any())
            return null;

        // Routing szolgáltatás: legközelebbi rendelés kiválasztása
        var nearestOrder = _routeOptimization.FindNearestOrder(courier, availableOrders);

        return nearestOrder;
    }

    /// <summary>
    /// Rendelés hozzárendelése futárhoz (thread-safe).
    ///
    /// FONTOS: Mivel több Task is futhat párhuzamosan,
    /// elképzelhető, hogy két futár is ugyanazt a rendelést akarja elkapni.
    /// Ezt az AssignedCourierId null check oldja meg.
    /// </summary>
    private void AssignOrderToCourier(Courier courier, DeliveryOrder order)
    {
        // Csak akkor rendelünk hozzá, ha még nincs futár
        if (order.AssignedCourierId == null)
        {
            // Futár frissítése
            courier.Status = CourierStatus.Delivering;
            courier.AssignedOrderIds.Add(order.Id);

            // Rendelés frissítése
            order.AssignedCourierId = courier.Id;
            order.Status = OrderStatus.InTransit;

            _logger.LogInformation(
                "✅ Rendelés hozzárendelve: {OrderNumber} -> Futár: {CourierName}",
                order.OrderNumber,
                courier.Name
            );
        }
    }

    /// <summary>
    /// Kézbesítés befejezése - rendelés leszállítva.
    ///
    /// ÚJ FUNKCIÓ:
    /// - Ellenőrzi, hogy késett-e a kézbesítés.
    /// - Ha igen, meghívja a NotificationService-t (értesítés).
    /// </summary>
    private async Task CompleteDeliveryAsync(Courier courier, DeliveryOrder order)
    {
        // Rendelés frissítése
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.Now;

        // Futár frissítése
        courier.AssignedOrderIds.Remove(order.Id);
        courier.TotalDeliveries++;
        courier.Status = CourierStatus.Available;

        // Statisztika frissítés (thread-safe Interlocked)
        Interlocked.Increment(ref _totalDeliveries);

        // ===== KÉSÉS ELLENŐRZÉS ÉS ÉRTESÍTÉS =====
        if (order.DeliveredAt > order.ExpectedDeliveryTime)
        {
            // Késés volt!
            Interlocked.Increment(ref _totalDelays);

            // Késés mértéke percekben
            var delayMinutes = (int)(order.DeliveredAt.Value - order.ExpectedDeliveryTime).TotalMinutes;

            // Értesítés küldése (NotificationService)
            await _notificationService.NotifyDelayAsync(order, delayMinutes);
        }
        else
        {
            // Időben érkezett
            _logger.LogInformation(
                "📦 Kézbesítve időben: {OrderNumber} - Futár: {CourierName}",
                order.OrderNumber,
                courier.Name
            );
        }
    }
}
