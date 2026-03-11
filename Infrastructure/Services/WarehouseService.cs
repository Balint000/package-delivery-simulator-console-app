namespace package_delivery_simulator_console_app.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;

/// <summary>
/// Warehouse kezelő service — kizárólag gráf-alapú logika.
///
/// KOORDINÁTA-MENTES: Minden metódus node ID-val és Dijkstrával dolgozik.
///
/// EGYETLEN VÁLTOZÁS a korábbi verzióhoz képest:
///   + FindBestWarehouseForCourier() hozzáadva
///     (korábban ez a logika a DeliverySimulationService-ben élt)
/// </summary>
public class WarehouseService : IWarehouseService
{
    // ── Függőségek ───────────────────────────────────────────────
    private readonly ICityGraph _cityGraph;
    private readonly ILogger<WarehouseService> _logger;

    // ── Belső cache ──────────────────────────────────────────────
    /// <summary>
    /// Az összes warehouse node a gráfban.
    /// Initialize() tölti fel egyszer, utána csak olvasunk belőle.
    /// </summary>
    private List<GraphNode> _warehouseNodes = new();
    private bool _isInitialized = false;

    // ── Konstruktor ──────────────────────────────────────────────
    public WarehouseService(ICityGraph cityGraph, ILogger<WarehouseService> logger)
    {
        _cityGraph = cityGraph;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────
    // INICIALIZÁLÁS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Megkeresi az összes Warehouse típusú node-ot és cache-eli.
    /// Alkalmazás indulásakor egyszer kell meghívni.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("WarehouseService már inicializálva, kihagyva.");
            return;
        }

        _warehouseNodes = _cityGraph.Nodes
            .Where(node => node.Type == NodeType.Warehouse)
            .ToList();

        if (_warehouseNodes.Count == 0)
            throw new InvalidOperationException(
                "A gráfban nincs egyetlen Warehouse node sem! " +
                "Legalább egy szükséges a szimulációhoz.");

        _logger.LogInformation(
            "{Count} raktár megtalálva: {Names}",
            _warehouseNodes.Count,
            string.Join(", ", _warehouseNodes.Select(w =>
                $"{w.Name} (Node {w.Id}, Zóna {w.ZoneId?.ToString() ?? "—"})")));

        _isInitialized = true;
    }

    // ────────────────────────────────────────────────────────────
    // LEKÉRDEZÉSEK
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Az összes raktár node (cache-ből, gyors).
    /// </summary>
    public IReadOnlyList<GraphNode> GetAllWarehouses()
    {
        EnsureInitialized();
        return _warehouseNodes.AsReadOnly();
    }

    /// <summary>
    /// Legközelebbi warehouse egy node-tól — Dijkstra alapján.
    ///
    /// Ha a node maga warehouse → azonnal visszaadja (0 perc).
    /// Egyébként minden warehouse-ra Dijkstrát futtat, a legrövidebbet adja vissza.
    /// </summary>
    public GraphNode? FindNearestWarehouseFromNode(int nodeId)
    {
        EnsureInitialized();

        var startNode = _cityGraph.GetNode(nodeId);
        if (startNode == null)
        {
            _logger.LogWarning("Node {NodeId} nem található a gráfban.", nodeId);
            return null;
        }

        // Ha maga is warehouse, nincs mit keresni
        if (startNode.Type == NodeType.Warehouse)
            return startNode;

        return FindClosestWarehouseFromList(nodeId, _warehouseNodes);
    }

    /// <summary>
    /// A futárhoz legjobb warehouse meghatározása.
    ///
    /// LOGIKA:
    ///   1. Szűrés: a futár saját zónáiban lévő warehouse-ok
    ///   2. Ezek közül Dijkstra szerinti legközelebbi
    ///   3. Fallback: ha a futár zónáiban nincs warehouse → abszolút legközelebbi
    ///
    /// Ez korábban a DeliverySimulationService-ben volt szétszórva —
    /// most egyetlen helyen, egyetlen felelősséggel él.
    /// </summary>
    public GraphNode? FindBestWarehouseForCourier(Courier courier)
    {
        EnsureInitialized();

        // 1. Futár zónáiban lévő warehouse-ok
        var zoneWarehouses = _warehouseNodes
            .Where(w => w.ZoneId.HasValue
                        && courier.AssignedZoneIds.Contains(w.ZoneId.Value))
            .ToList();

        if (zoneWarehouses.Count > 0)
        {
            // 2. Ezek közül a Dijkstra szerinti legközelebbi
            var best = FindClosestWarehouseFromList(courier.CurrentNodeId, zoneWarehouses);

            if (best != null)
            {
                _logger.LogDebug(
                    "{Courier} legjobb warehouse: {WName} (Node {WId}, saját zóna)",
                    courier.Name, best.Name, best.Id);
                return best;
            }
        }

        // 3. Fallback: nincs zónás warehouse → abszolút legközelebbi
        _logger.LogWarning(
            "{Courier} zónáiban ({Zones}) nincs elérhető warehouse — " +
            "fallback: abszolút legközelebbi.",
            courier.Name,
            string.Join(", ", courier.AssignedZoneIds));

        return FindNearestWarehouseFromNode(courier.CurrentNodeId);
    }

    /// <summary>
    /// Igaz, ha a node warehouse.
    /// </summary>
    public bool IsWarehouse(int nodeId)
    {
        EnsureInitialized();
        return _warehouseNodes.Any(w => w.Id == nodeId);
    }

    /// <summary>
    /// Az adott zóna warehouse node ID-ja (első találat).
    /// Null ha nincs warehouse a zónában.
    /// </summary>
    public int? GetWarehouseInZone(int zoneId)
    {
        EnsureInitialized();
        return _warehouseNodes.FirstOrDefault(w => w.ZoneId == zoneId)?.Id;
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT SEGÉDMETÓDUSOK
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Egy megadott warehouse-lista közül a Dijkstra szerinti legközelebbit adja vissza.
    ///
    /// MEGOSZTOTT LOGIKA: FindNearestWarehouseFromNode és FindBestWarehouseForCourier
    /// is ezt hívja — egyszer van megírva, kétszer használva.
    /// </summary>
    /// <param name="fromNodeId">Kiindulási node</param>
    /// <param name="warehouses">Jelölt warehouse-ok listája</param>
    /// <returns>Legközelebbi warehouse Dijkstra szerint, vagy null</returns>
    private GraphNode? FindClosestWarehouseFromList(int fromNodeId, List<GraphNode> warehouses)
    {
        GraphNode? nearest = null;
        int shortestTime = int.MaxValue;

        foreach (var warehouse in warehouses)
        {
            var (_, pathTime) = _cityGraph.FindShortestPath(fromNodeId, warehouse.Id);

            if (pathTime == int.MaxValue)
            {
                _logger.LogWarning(
                    "Raktár {WName} (Node {WId}) nem elérhető Node {NodeId}-ből.",
                    warehouse.Name, warehouse.Id, fromNodeId);
                continue;
            }

            if (pathTime < shortestTime)
            {
                shortestTime = pathTime;
                nearest = warehouse;
            }
        }

        if (nearest == null)
            _logger.LogError("Node {NodeId}-ből egyetlen jelölt warehouse sem elérhető!", fromNodeId);

        return nearest;
    }

    /// <summary>
    /// Ellenőrzi, hogy Initialize() meg lett-e hívva.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException(
                "WarehouseService nincs inicializálva — hívd meg az Initialize()-t!");
    }
}
