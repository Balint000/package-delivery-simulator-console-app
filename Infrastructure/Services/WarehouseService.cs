namespace package_delivery_simulator_console_app.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;

/// <summary>
/// Warehouse kezelő service implementáció.
///
/// FONTOS: Minden távolság számítás a GRÁF ALAPJÁN történik (Dijkstra),
/// NEM Euklideszi koordinátákkal!
/// </summary>
public class WarehouseService : IWarehouseService
{
    private readonly ICityGraph _cityGraph;
    private readonly ILogger<WarehouseService> _logger;

    // Cache: Warehouse node-ok gyors eléréshez
    private List<GraphNode> _warehouseNodes = new();
    private bool _isInitialized = false;

    public WarehouseService(
        ICityGraph cityGraph,
        ILogger<WarehouseService> logger)
    {
        _cityGraph = cityGraph;
        _logger = logger;
    }

    // ====== INICIALIZÁLÁS ======

    public void Initialize()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("WarehouseService already initialized, skipping.");
            return;
        }

        _logger.LogInformation("Initializing WarehouseService...");

        // Megkeressük az összes warehouse típusú node-ot
        _warehouseNodes = _cityGraph.Nodes
            .Where(node => node.Type == NodeType.Warehouse)
            .ToList();

        if (_warehouseNodes.Count == 0)
        {
            _logger.LogError("No warehouse nodes found in the city graph!");
            throw new InvalidOperationException(
                "City graph must contain at least one Warehouse node.");
        }

        _logger.LogInformation(
            "Found {Count} warehouse(s): {Names}",
            _warehouseNodes.Count,
            string.Join(", ", _warehouseNodes.Select(w => $"{w.Name} (Node {w.Id}, Zone {w.ZoneId})")));

        _isInitialized = true;
    }

    // ====== LEKÉRDEZÉSEK ======

    public IReadOnlyList<GraphNode> GetAllWarehouses()
    {
        EnsureInitialized();
        return _warehouseNodes.AsReadOnly();
    }

    public GraphNode? FindNearestWarehouse(Location location)
    {
        EnsureInitialized();

        // Először megkeressük a legközelebbi NODE-ot a koordinátához
        // (Ez még Euklideszi, mert koordinátából node-ot kell csinálni)
        var nearestNode = FindNearestNode(location);
        if (nearestNode == null)
        {
            _logger.LogWarning("Could not find any node near location {Location}", location);
            return null;
        }

        _logger.LogDebug("Location {Location} mapped to node: {NodeName} (ID: {NodeId})",
            location, nearestNode.Name, nearestNode.Id);

        // Most ETTŐL A NODE-TÓL keressük a legközelebbi warehouse-t GRÁF ALAPJÁN!
        return FindNearestWarehouseFromNode(nearestNode.Id);
    }

    public GraphNode? FindNearestWarehouseFromNode(int nodeId)
    {
        EnsureInitialized();

        var startNode = _cityGraph.GetNode(nodeId);
        if (startNode == null)
        {
            _logger.LogWarning("Node {NodeId} not found in graph", nodeId);
            return null;
        }

        // Ha maga a node már warehouse, akkor visszaadjuk
        if (startNode.Type == NodeType.Warehouse)
        {
            _logger.LogDebug("Node {NodeId} ({Name}) is already a warehouse",
                nodeId, startNode.Name);
            return startNode;
        }

        GraphNode? nearestWarehouse = null;
        int shortestPathTime = int.MaxValue;

        // Végigmegyünk az összes warehouse-on és DIJKSTRA-val számolunk!
        foreach (var warehouse in _warehouseNodes)
        {
            // Legrövidebb útvonal ideje a gráf élei alapján (figyelembe veszi a forgalmat is!)
            var (_, pathTime) = _cityGraph.FindShortestPath(nodeId, warehouse.Id);

            // Ha nem elérhető (pathTime == int.MaxValue), akkor ugrjuk át
            if (pathTime == int.MaxValue)
            {
                _logger.LogWarning(
                    "Warehouse {WarehouseName} (ID: {WarehouseId}) not reachable from node {NodeId}",
                    warehouse.Name, warehouse.Id, nodeId);
                continue;
            }

            if (pathTime < shortestPathTime)
            {
                shortestPathTime = pathTime;
                nearestWarehouse = warehouse;
            }
        }

        if (nearestWarehouse != null)
        {
            _logger.LogInformation(
                "Nearest warehouse from node {NodeId} ({NodeName}): {WarehouseName} (ID: {WarehouseId}) - {Time} minutes via graph",
                nodeId, startNode.Name, nearestWarehouse.Name, nearestWarehouse.Id, shortestPathTime);
        }
        else
        {
            _logger.LogError("No reachable warehouse found from node {NodeId}", nodeId);
        }

        return nearestWarehouse;
    }

    public bool IsWarehouse(int nodeId)
    {
        EnsureInitialized();
        return _warehouseNodes.Any(w => w.Id == nodeId);
    }

    public int? GetWarehouseInZone(int zoneId)
    {
        EnsureInitialized();

        var warehouse = _warehouseNodes.FirstOrDefault(w => w.ZoneId == zoneId);

        if (warehouse != null)
        {
            _logger.LogDebug(
                "Found warehouse in zone {ZoneId}: {Name} (Node ID: {NodeId})",
                zoneId, warehouse.Name, warehouse.Id);
            return warehouse.Id;
        }

        _logger.LogDebug("No warehouse found in zone {ZoneId}", zoneId);
        return null;
    }

    // ====== HELPER METÓDUSOK ======

    /// <summary>
    /// Legközelebbi node keresése egy koordináta alapján.
    ///
    /// CSAK EZT AZ EGY HELYET használjuk Euklideszi távolságot,
    /// mert koordinátából node-ot kell csinálni!
    /// Minden más számítás GRÁF ALAPÚ!
    /// </summary>
    private GraphNode? FindNearestNode(Location location)
    {
        GraphNode? nearest = null;
        double minDistance = double.MaxValue;

        foreach (var node in _cityGraph.Nodes)
        {
            // Euklideszi távolság (CSAK koordináta → node mapping-hez!)
            double dx = node.Location.X - location.X;
            double dy = node.Location.Y - location.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = node;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Ellenőrzi, hogy a service inicializálva lett-e.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "WarehouseService not initialized. Call Initialize() first.");
        }
    }
}
