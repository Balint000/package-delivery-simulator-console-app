// ============================================================
// GreedyAssignmentService.cs
// ============================================================
// Felelőssége: Rendelések hozzárendelése futárokhoz.
//
// GREEDY (mohó) ALGORITMUS:
// Nem keres globálisan optimális megoldást.
// Minden rendelésnél egyszerűen a LEGJOBB PILLANATNYI
// döntést hozza: melyik szabad futár van a legközelebb?
//
// ELŐNYE:  Gyors, egyszerű, könnyen érthető
// HÁTRÁNYA: Nem mindig az összesség szempontjából legjobb
//           (pl. lehet, hogy ha nem a legközelebbit küldjük,
//            a többi rendelés összesítve gyorsabb lenne)
//
// MIÉRT IDE KERÜL (Services/Assignment)?
// Ez üzleti logika (business logic) — egy döntési algoritmus.
// Nem infrastruktúra (nem fájlt olvas), nem prezentáció
// (nem ír a képernyőre). A Services rétegbe tartozik.
// ============================================================

namespace package_delivery_simulator_console_app.Services.Assignment;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator_console_app.Infrastructure.Graph;

/// <summary>
/// Greedy (mohó) rendelés-hozzárendelési algoritmus.
///
/// Minden rendeléshez megkeresi a legközelebbi szabad futárt,
/// és hozzárendeli a rendelést.
/// </summary>
public class GreedyAssignmentService
{
    // ====== FÜGGŐSÉGEK ======

    /// <summary>
    /// A városgráf — Dijkstra algoritmushoz kell,
    /// hogy valódi gráf-távolságot mérjünk, nem Euklideszi távolságot.
    /// </summary>
    private readonly ICityGraph _cityGraph;

    /// <summary>
    /// Logger a naplózáshoz.
    /// </summary>
    private readonly ILogger<GreedyAssignmentService> _logger;

    // ====== KONSTRUKTOR ======

    /// <summary>
    /// GreedyAssignmentService létrehozása.
    /// </summary>
    /// <param name="cityGraph">Városgráf (DI-ból jön)</param>
    /// <param name="logger">Logger (DI-ból jön)</param>
    public GreedyAssignmentService(
        ICityGraph cityGraph,
        ILogger<GreedyAssignmentService> logger)
    {
        _cityGraph = cityGraph;
        _logger = logger;
    }

    // ====== FŐ METÓDUS ======

    /// <summary>
    /// Egy rendelés hozzárendelése a legközelebbi elérhető futárhoz.
    ///
    /// LÉPÉSEK:
    ///   1. Kiszűri az Available státuszú futárokat
    ///   2. Minden futárhoz Dijkstrával kiszámolja az utat a rendelésig
    ///   3. A legrövidebb úttal rendelkező futárt választja
    ///   4. Frissíti a rendelés és a futár státuszát
    ///
    /// VISSZATÉRÉSI ÉRTÉK:
    ///   - A kiválasztott futár, ha sikerült hozzárendelni
    ///   - null, ha nincs elérhető futár
    /// </summary>
    /// <param name="order">A hozzárendelendő rendelés</param>
    /// <param name="availableCouriers">Az összes futár listája</param>
    /// <returns>A kiválasztott futár vagy null</returns>
    public Courier? AssignToNearest(
        DeliveryOrder order,
        List<Courier> availableCouriers)
    {
        _logger.LogInformation(
            "Greedy hozzárendelés: {OrderNumber} ({CustomerName})...",
            order.OrderNumber, order.CustomerName);

        // Csak a szabad futárokat vesszük figyelembe
        var freeCouriers = availableCouriers
            .Where(c => c.Status == CourierStatus.Available)
            .ToList();

        if (freeCouriers.Count == 0)
        {
            _logger.LogWarning(
                "Nincs szabad futár a(z) {OrderNumber} rendeléshez!",
                order.OrderNumber);
            return null;
        }

        _logger.LogDebug(
            "{Count} szabad futár közül keresünk legközelebbit...",
            freeCouriers.Count);

        // A rendelés célcsúcsát meghatározzuk: melyik gráf-node a legközelebb
        // a rendelés koordinátájához?
        int orderNodeId = FindNearestNodeId(order.AddressLocation);

        _logger.LogDebug(
            "Rendelés célpontja: Node {Id} ({Name})",
            orderNodeId,
            _cityGraph.GetNode(orderNodeId)?.Name ?? "?");

        // ---- Legközelebbi futár keresése ----
        Courier? bestCourier = null;
        int shortestTime = int.MaxValue; // "végtelen" — még nem találtunk senkit

        foreach (var courier in freeCouriers)
        {
            // A futár koordinátáját is node-ra képezzük
            int courierNodeId = FindNearestNodeId(courier.CurrentLocation);

            // Dijkstra: mennyi idő kell a futárnak a rendelés helyszínéig?
            // A visszatérési érték egy Tuple: (útvonal lista, összes idő)
            // Az "_" azt jelenti: "az útvonalra most nem vagyunk kíváncsiak"
            var (_, travelTime) = _cityGraph.FindShortestPath(
                courierNodeId, orderNodeId);

            _logger.LogDebug(
                "  └ {Name}: Node {NodeId} → {OrderNodeId} = {Time} perc",
                courier.Name, courierNodeId, orderNodeId, travelTime);

            // Ha ez a futár közelebb van az eddig legjobbhoz, frissítünk
            if (travelTime < shortestTime)
            {
                shortestTime = travelTime;
                bestCourier = courier;
            }
        }

        // ---- Hozzárendelés elvégzése ----
        if (bestCourier != null)
        {
            // Rendelés státusz: Pending → Assigned
            order.Status = OrderStatus.Assigned;
            order.AssignedCourierId = bestCourier.Id;

            // Futár státusz: Available → Busy
            bestCourier.Status = CourierStatus.Busy;
            bestCourier.AssignedOrderIds.Add(order.Id);

            _logger.LogInformation(
                "✅ Hozzárendelve: {OrderNumber} → {CourierName} " +
                "(becsült menetidő: {Time} perc)",
                order.OrderNumber, bestCourier.Name, shortestTime);
        }

        return bestCourier;
    }

    /// <summary>
    /// Több rendelés tömeges hozzárendelése egyszerre.
    ///
    /// FONTOS: Minden rendelésnél újra megvizsgálja a futárokat,
    /// mert az előző hozzárendelés után a futár már Busy lehet!
    ///
    /// Példa: Ha 3 rendelés van és 2 futár:
    ///   - 1. rendelés → Futár A (legközelebbi) → A most Busy
    ///   - 2. rendelés → Futár B (A már nem szabad) → B most Busy
    ///   - 3. rendelés → null (nincs szabad futár)
    /// </summary>
    /// <param name="orders">Hozzárendelendő rendelések</param>
    /// <param name="couriers">Az összes futár</param>
    /// <returns>
    /// Dictionary: rendelés ID → hozzárendelt futár (vagy null ha nem sikerült)
    /// </returns>
    public Dictionary<int, Courier?> AssignAll(
        List<DeliveryOrder> orders,
        List<Courier> couriers)
    {
        _logger.LogInformation(
            "Tömeges hozzárendelés: {OrderCount} rendelés, {CourierCount} futár",
            orders.Count, couriers.Count);

        // Az eredményeket egy szótárban tároljuk: OrderId → Futár
        var assignments = new Dictionary<int, Courier?>();

        foreach (var order in orders)
        {
            // Csak a Pending rendeléseket rendeljük hozzá
            if (order.Status != OrderStatus.Pending)
            {
                _logger.LogDebug(
                    "Kihagyva (nem Pending): {OrderNumber} [{Status}]",
                    order.OrderNumber, order.Status);
                continue;
            }

            // Greedy hozzárendelés egyenként
            var assignedCourier = AssignToNearest(order, couriers);
            assignments[order.Id] = assignedCourier;
        }

        // Összesítő log
        int successCount = assignments.Values.Count(c => c != null);
        int failCount = assignments.Values.Count(c => c == null);

        _logger.LogInformation(
            "Tömeges hozzárendelés kész: {Success} sikeres, {Fail} sikertelen",
            successCount, failCount);

        return assignments;
    }

    // ====== PRIVÁT SEGÉDMETÓDUS ======

    /// <summary>
    /// A gráfban megkeresi a koordinátához legközelebbi csúcs ID-ját.
    ///
    /// Ez az EGYETLEN hely ahol Euklideszi távolságot számolunk.
    /// Azért kell, mert koordinátából (x, y) gráf-csúcsot (node ID)
    /// kell csinálni, és ehhez nincs más módszer.
    ///
    /// Minden más távolságszámítás Dijkstra-alapú!
    /// </summary>
    /// <param name="location">Koordináta (x, y)</param>
    /// <returns>A legközelebbi csúcs ID-ja a gráfban</returns>
    private int FindNearestNodeId(Location location)
    {
        int nearestId = 0;
        double minDistance = double.MaxValue;

        foreach (var node in _cityGraph.Nodes)
        {
            // Euklideszi távolság képlete: √((x₂-x₁)² + (y₂-y₁)²)
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
