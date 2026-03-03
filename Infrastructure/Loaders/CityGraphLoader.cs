namespace package_delivery_simulator.Infrastructure.Loaders;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator_console_app.Domain.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Configuration;
using package_delivery_simulator.Infrastructure.Graph;
using package_delivery_simulator_console_app.Data.Dto;

/// <summary>
/// CityGraph betöltése a Data/city-graph.json fájlból.
/// </summary>
public sealed class CityGraphLoader : ICityGraphLoader
{
    private readonly ILogger<CityGraphLoader> _logger;
    private readonly IOptions<DataOptions> _options;

    public CityGraphLoader(
        ILogger<CityGraphLoader> logger,
        IOptions<DataOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<CityGraph> LoadAsync(CancellationToken cancellationToken)
    {
        // 1. Fájlelérési út összeállítása konfigurációból
        var basePath = _options.Value.BasePath;          // pl. "Data"
        var fileName = _options.Value.CityGraphFileName; // pl. "city-graph.json"
        var fullPath = Path.Combine(basePath, fileName);

        _logger.LogInformation("Loading city graph from {Path}", fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("city-graph.json not found", fullPath);
        }

        // 2. Fájl beolvasása aszinkron
        await using var stream = File.OpenRead(fullPath);

        var json = await JsonSerializer.DeserializeAsync<CityGraphDto>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        if (json is null)
        {
            throw new InvalidOperationException("Failed to deserialize city-graph.json");
        }

        // 3. CityGraph példány létrehozása a csúcsok számával
        var maxNodeId = json.Nodes.Any() ? json.Nodes.Max(n => n.Id) : 0;
        var maxNodeCount = maxNodeId + 1;

        var graph = new CityGraph(maxNodeCount);

        // 4. Csúcsok hozzáadása a gráfhoz
        // FONTOS: a CityGraph megköveteli, hogy node.Id == lista index
        var orderedNodes = json.Nodes.OrderBy(n => n.Id).ToList();

        for (var expectedId = 0; expectedId < orderedNodes.Count; expectedId++)
        {
            var nodeJson = orderedNodes[expectedId];

            if (nodeJson.Id != expectedId)
            {
                throw new InvalidOperationException(
                    $"Node IDs must be continuous from 0..N. Expected {expectedId}, got {nodeJson.Id}.");
            }

            // String → NodeType enum map-pelés
            var nodeType = Enum.Parse<NodeType>(nodeJson.Type, ignoreCase: true);

            // JSON koordináták → Location value object
            var location = new Location(nodeJson.Location.X, nodeJson.Location.Y);

            var graphNode = new GraphNode(
                id: nodeJson.Id,
                name: nodeJson.Name,
                type: nodeType,
                location: location,
                zoneId: nodeJson.ZoneId
            );

            graph.AddNode(graphNode);
        }

        // 5. Élek hozzáadása (irányítatlan gráf)
        foreach (var edgeJson in json.Edges)
        {
            graph.AddEdge(edgeJson.From, edgeJson.To, edgeJson.IdealTimeMinutes);
        }

        _logger.LogInformation(
            "City graph loaded successfully. Nodes: {NodeCount}, Edges: {EdgeCount}",
            graph.NodeCount,
            graph.EdgeCount);

        return graph;
    }
}
