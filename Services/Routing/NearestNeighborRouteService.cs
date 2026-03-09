namespace package_delivery_simulator_console_app.Services.Routing;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator_console_app.Infrastructure.Graph;

/// <summary>
/// Nearest Neighbor (legközelebbi szomszéd) útvonal-optimalizáló.
///
/// PROBLÉMA AMIT MEGOLD:
///   Ha egy futárnak egyszerre 3 rendelése van, a hozzárendelés sorrendje
///   véletlenszerű — nem feltétlenül optimális. Pl. a futár oda-vissza
///   keresztezi a várost, ahelyett hogy logikus sorrendben haladna.
///
/// ALGORITMUS (Nearest Neighbor TSP közelítés):
///   1. Kiindulás: warehouse node (ahol a futár felvette a csomagokat)
///   2. A még nem kézbesített rendelések közül kiválasztja a legközelebbit
///      (Dijkstra alapján — gráf-távolság, nem légvonal!)
///   3. "Odamegy", az lesz a következő kézbesítés
///   4. Az új pozícióból ismétli, amíg van rendelés
///
/// PÉLDA (3 rendelés, raktárból indulva):
///   Warehouse(0) → legközelebbi: Node 7 (5 perc) → legközelebbi: Node 11 (8 perc) → Node 3 (6 perc)
///   vs. eredeti sorrend: Node 11 → Node 3 → Node 7 (esetleg 20+ perc kerülővel)
///
/// FONTOS:
///   Ez egy KÖZELÍTŐ algoritmus — nem garantál optimális megoldást (az NP-nehéz),
///   de a véletlenszerű sorrendnél mindig jobb vagy egyenlő eredményt ad.
///
/// KOORDINÁTA-MENTES:
///   Minden távolságmérés Dijkstra-alapú node ID-val — nincs koordináta-közelítés.
/// </summary>
public class NearestNeighborRouteService
{
    // ── Függőségek ───────────────────────────────────────────────
    private readonly ICityGraph _cityGraph;
    private readonly ILogger<NearestNeighborRouteService> _logger;

    // ── Konstruktor ──────────────────────────────────────────────
    public NearestNeighborRouteService(
        ICityGraph cityGraph,
        ILogger<NearestNeighborRouteService> logger)
    {
        _cityGraph = cityGraph;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────
    // FŐ METÓDUS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rendelések optimális kézbesítési sorrendjének meghatározása.
    ///
    /// BEMENET:
    ///   startNodeId — a warehouse node ID-ja (ahonnan a futár indul)
    ///   orders      — a kézbesítendő rendelések (sorrendtől független lista)
    ///
    /// KIMENET:
    ///   Ugyanazok a rendelések, de optimális sorrendben.
    ///   Az első elem a raktárhoz legközelebbi, az utolsó a legmesszebb eső.
    ///
    /// EDGE CASE-EK:
    ///   - 0 rendelés → üres lista vissza
    ///   - 1 rendelés → változatlan lista vissza (nincs mit optimalizálni)
    ///   - Nem elérhető node → a rendelés a lista végére kerül
    /// </summary>
    /// <param name="startNodeId">Kiindulási node (warehouse)</param>
    /// <param name="orders">Optimalizálandó rendelések</param>
    /// <returns>Optimális sorrendű rendelés-lista</returns>
    public List<DeliveryOrder> OptimizeRoute(int startNodeId, List<DeliveryOrder> orders)
    {
        // Edge case: 0 vagy 1 rendelés esetén nincs mit optimalizálni
        if (orders.Count <= 1)
        {
            _logger.LogDebug(
                "NN optimalizálás kihagyva: {Count} rendelés (min. 2 kell)",
                orders.Count);
            return new List<DeliveryOrder>(orders);
        }

        _logger.LogInformation(
            "NN útvonal-optimalizálás: {Count} rendelés, kiindulás: Node {Start} ({StartName})",
            orders.Count,
            startNodeId,
            _cityGraph.GetNode(startNodeId)?.Name ?? "?");

        // Még nem látogatott rendelések — ezekből választjuk mindig a legközelebbit
        var remaining = new List<DeliveryOrder>(orders);

        // Az optimális sorrendű eredménylista — ide kerülnek sorban a kiválasztottak
        var optimized = new List<DeliveryOrder>(orders.Count);

        // Az aktuális pozíció: kezdetben a warehouse, majd mindig az utolsó kézbesítési cím
        int currentNodeId = startNodeId;

        // ── Nearest Neighbor ciklus ──────────────────────────────
        // Addig fut, amíg van kézbesítetlen rendelés
        while (remaining.Count > 0)
        {
            DeliveryOrder? nearest = null;
            int shortestTime = int.MaxValue;

            // Megkeressük a remaining listából az aktuális pozícióhoz legközelebbit
            foreach (var order in remaining)
            {
                // Dijkstra: aktuális pozíció → kézbesítési cím
                var (_, travelTime) = _cityGraph.FindShortestPath(
                    currentNodeId, order.AddressNodeId);

                // Nem elérhető node → kihagyjuk (de nem dobjuk el — a végére kerül)
                if (travelTime == int.MaxValue)
                {
                    _logger.LogWarning(
                        "NN: {OrderNumber} (Node {NodeId}) nem elérhető Node {Current}-ből, kihagyva",
                        order.OrderNumber, order.AddressNodeId, currentNodeId);
                    continue;
                }

                _logger.LogDebug(
                    "  NN jelölt: {OrderNumber} → Node {NodeId} ({NodeName}), {Time} perc",
                    order.OrderNumber,
                    order.AddressNodeId,
                    _cityGraph.GetNode(order.AddressNodeId)?.Name ?? "?",
                    travelTime);

                // Ez-e a legjobb eddig?
                if (travelTime < shortestTime)
                {
                    shortestTime = travelTime;
                    nearest = order;
                }
            }

            if (nearest != null)
            {
                // Megtaláltuk a legközelebbit → kiválasztjuk és lépünk
                optimized.Add(nearest);
                remaining.Remove(nearest);

                _logger.LogInformation(
                    "  NN választás: {OrderNumber} → {NodeName} ({Time} perc)",
                    nearest.OrderNumber,
                    _cityGraph.GetNode(nearest.AddressNodeId)?.Name ?? "?",
                    shortestTime);

                // Az új "jelenlegi pozíció" a most kiválasztott rendelés címe
                // Innen keressük majd a következő legközelebbit
                currentNodeId = nearest.AddressNodeId;
            }
            else
            {
                // Minden maradék rendelés elérhetetlen — a lista végére fűzzük őket
                _logger.LogWarning(
                    "NN: {Count} rendelés nem elérhető, a lista végére kerül", remaining.Count);
                optimized.AddRange(remaining);
                break;
            }
        }

        // Összehasonlítás logolása (debug célra)
        LogRouteComparison(startNodeId, orders, optimized);

        return optimized;
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT — Debug segédmetódus
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiírja az eredeti és az optimalizált sorrend becsült össz-idejét.
    /// Segít látni, hogy az optimalizálás mennyit javított.
    ///
    /// MEGJEGYZÉS: Ez csak közelítő összehasonlítás — nem tartalmazza
    /// a raktárhoz vezető utat, csak a kézbesítések közötti utakat.
    /// </summary>
    private void LogRouteComparison(
        int startNodeId,
        List<DeliveryOrder> original,
        List<DeliveryOrder> optimized)
    {
        // Eredeti sorrend becsült ideje
        int originalTime = EstimateRouteTime(startNodeId, original);

        // Optimalizált sorrend becsült ideje
        int optimizedTime = EstimateRouteTime(startNodeId, optimized);

        int savedMinutes = originalTime - optimizedTime;

        _logger.LogInformation(
            "NN eredmény: eredeti ~{Original} perc → optimalizált ~{Optimized} perc " +
            "({Saved} perc megtakarítás)",
            originalTime, optimizedTime, savedMinutes);
    }

    /// <summary>
    /// Egy rendelés-sorrend becsült össz-útvonal-ideje (Dijkstra, aktuális forgalommal).
    /// Csak összehasonlításhoz — a raktártól az első kézbesítésig, majd tovább.
    /// </summary>
    private int EstimateRouteTime(int startNodeId, List<DeliveryOrder> orders)
    {
        if (orders.Count == 0) return 0;

        int totalTime = 0;
        int currentNode = startNodeId;

        foreach (var order in orders)
        {
            var (_, time) = _cityGraph.FindShortestPath(currentNode, order.AddressNodeId);

            // Ha nem elérhető, nagy büntetőértéket adunk
            if (time == int.MaxValue)
                totalTime += 9999;
            else
                totalTime += time;

            currentNode = order.AddressNodeId;
        }

        return totalTime;
    }
}
