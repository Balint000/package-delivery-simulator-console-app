namespace package_delivery_simulator_console_app.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;

/// <summary>
/// Warehouse kezelő service — KIZÁRÓLAG gráf-alapú logika.
///
/// EGYSZERŰSÍTÉS a korábbi verzióhoz képest:
/// - FindNearestWarehouse(Location) TÖRÖLVE — koordinátás Euklideszi közelítés volt
/// - FindNearestNode(Location) privát helper TÖRÖLVE — koordinátás volt
///
/// Mivel a rendszerben minden pozíció node ID-val van megadva
/// (courier.CurrentNodeId, order.AddressNodeId), nincs szükség
/// koordinátából node-ot keresni. Mindig van node ID-nk.
///
/// Az egyetlen logika ami marad: node ID → legközelebbi warehouse (Dijkstra).
/// </summary>
public class WarehouseService : IWarehouseService
{
    // ── Függőségek ──────────────────────────────────────────────
    private readonly ICityGraph _cityGraph;
    private readonly ILogger<WarehouseService> _logger;

    // ── Belső állapot ────────────────────────────────────────────
    /// <summary>
    /// Cache: az összes warehouse node a gráfban.
    /// Initialize() tölti fel, utána csak olvasunk belőle.
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
    /// Megkeresi a gráfban az összes Warehouse típusú node-ot és cache-eli.
    /// Elég egyszer az alkalmazás indulásakor meghívni.
    /// </summary>
    public void Initialize()
    {
        // Ne inicializáljuk kétszer
        if (_isInitialized)
        {
            _logger.LogWarning("WarehouseService már inicializálva van, kihagyva.");
            return;
        }

        // Megkeressük az összes Warehouse típusú node-ot a gráfban
        _warehouseNodes = _cityGraph.Nodes
            .Where(node => node.Type == NodeType.Warehouse)
            .ToList();

        // Ha nincs egyetlen warehouse sem, a szimuláció nem tud futni
        if (_warehouseNodes.Count == 0)
        {
            throw new InvalidOperationException(
                "A városgráfban nincs egyetlen Warehouse típusú node sem! " +
                "Legalább egy warehouse szükséges a szimulációhoz.");
        }

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
    /// Visszaadja az összes raktár node-ot (cache-ből, gyors).
    /// </summary>
    public IReadOnlyList<GraphNode> GetAllWarehouses()
    {
        EnsureInitialized();
        return _warehouseNodes.AsReadOnly();
    }

    /// <summary>
    /// A megadott node-tól legközelebbi warehouse megkeresése.
    ///
    /// MŰKÖDÉS (tisztán gráf-alapú, koordináta NÉLKÜL):
    ///   1. Ha a node maga warehouse → azonnal visszaadjuk
    ///   2. Minden ismert warehouse-ra lefuttatjuk Dijkstrát
    ///   3. A legkisebb útvonal-időjű warehouse-t adjuk vissza
    ///
    /// Az útvonal-idő az aktuális forgalmat (TrafficMultiplier) is figyelembe veszi.
    /// </summary>
    /// <param name="nodeId">Kiindulási node ID</param>
    /// <returns>Legközelebbi warehouse node (Dijkstra szerint), vagy null</returns>
    public GraphNode? FindNearestWarehouseFromNode(int nodeId)
    {
        EnsureInitialized();

        // Ellenőrzés: létezik-e a node a gráfban?
        var startNode = _cityGraph.GetNode(nodeId);
        if (startNode == null)
        {
            _logger.LogWarning("Node {NodeId} nem található a gráfban.", nodeId);
            return null;
        }

        // Ha a node maga warehouse → rögtön visszaadjuk (0 perc)
        if (startNode.Type == NodeType.Warehouse)
        {
            _logger.LogDebug(
                "Node {NodeId} ({Name}) maga is warehouse — nem kell keresni.",
                nodeId, startNode.Name);
            return startNode;
        }

        // Dijkstrával megkeressük a legközelebbi warehouse-t
        GraphNode? nearest = null;
        int shortestTime = int.MaxValue;

        foreach (var warehouse in _warehouseNodes)
        {
            // Legrövidebb útvonal az adott node-tól ehhez a warehouse-hoz
            var (_, pathTime) = _cityGraph.FindShortestPath(nodeId, warehouse.Id);

            // Ha nem elérhető (pl. összefüggetlen gráf), kihagyjuk
            if (pathTime == int.MaxValue)
            {
                _logger.LogWarning(
                    "Raktár {WName} (Node {WId}) nem elérhető Node {NodeId}-ből.",
                    warehouse.Name, warehouse.Id, nodeId);
                continue;
            }

            // Jobb útvonalat találtunk?
            if (pathTime < shortestTime)
            {
                shortestTime = pathTime;
                nearest = warehouse;
            }
        }

        if (nearest == null)
        {
            _logger.LogError(
                "Node {NodeId}-ből egyetlen warehouse sem elérhető!", nodeId);
        }
        else
        {
            _logger.LogDebug(
                "Legközelebbi raktár Node {NodeId}-től: {WName} (Node {WId}) — {Time} perc",
                nodeId, nearest.Name, nearest.Id, shortestTime);
        }

        return nearest;
    }

    /// <summary>
    /// Igaz, ha a megadott node warehouse.
    /// </summary>
    public bool IsWarehouse(int nodeId)
    {
        EnsureInitialized();
        return _warehouseNodes.Any(w => w.Id == nodeId);
    }

    /// <summary>
    /// Megadja az adott zóna warehouse node ID-ját.
    /// Ha a zónában nincs warehouse, null-t ad vissza.
    /// </summary>
    public int? GetWarehouseInZone(int zoneId)
    {
        EnsureInitialized();

        var warehouse = _warehouseNodes.FirstOrDefault(w => w.ZoneId == zoneId);

        if (warehouse == null)
        {
            _logger.LogDebug("Zóna {ZoneId}-ban nincs warehouse.", zoneId);
            return null;
        }

        _logger.LogDebug(
            "Zóna {ZoneId} warehouse: {Name} (Node {NodeId})",
            zoneId, warehouse.Name, warehouse.Id);

        return warehouse.Id;
    }

    // ────────────────────────────────────────────────────────────
    // PRIVÁT HELPER
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ellenőrzi, hogy Initialize() meg lett-e hívva.
    /// Ha nem, kivételt dob — így nem lehet véletlenül inicializálatlan
    /// service-t használni.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException(
                "WarehouseService nincs inicializálva. Hívd meg az Initialize()-t először!");
    }
}
