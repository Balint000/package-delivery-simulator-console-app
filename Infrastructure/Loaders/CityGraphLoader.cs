namespace package_delivery_simulator.Infrastructure.Loaders
{
    using System.Text.Json;
    using package_delivery_simulator.Data.Dto;
    using package_delivery_simulator.Domain.Entities;
    using package_delivery_simulator.Domain.Enums;
    using package_delivery_simulator.Domain.ValueObjects;
    using package_delivery_simulator.Infrastructure.Graph;

    /// <summary>
    /// Város gráf betöltése JSON fájlból.
    /// </summary>
    public static class CityGraphLoader
    {
        /// <summary>
        /// CityGraph betöltése JSON fájlból.
        /// </summary>
        /// <param name="jsonFilePath">JSON fájl elérési útja</param>
        /// <returns>Betöltött CityGraph</returns>
        public static CityGraph LoadFromJson(string jsonFilePath)
        {
            Console.WriteLine($"📂 Loading city graph from: {jsonFilePath}");

            // 1. JSON fájl beolvasása
            if (!File.Exists(jsonFilePath))
            {
                throw new FileNotFoundException($"JSON file not found: {jsonFilePath}");
            }

            string jsonContent = File.ReadAllText(jsonFilePath);

            // 2. JSON deszerializálás
            var dto = JsonSerializer.Deserialize<CityGraphDto>(jsonContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

            if (dto == null)
            {
                throw new InvalidOperationException("Failed to deserialize JSON");
            }

            Console.WriteLine($"✅ Loaded: {dto.CityName}");
            Console.WriteLine($"   {dto.Description}");
            Console.WriteLine($"   Nodes: {dto.Nodes.Count}, Edges: {dto.Edges.Count}");

            // 3. CityGraph létrehozása
            var graph = new CityGraph(maxNodeCount: dto.Nodes.Count * 2);

            // 4. Csúcsok hozzáadása
            Console.WriteLine("\n📍 Adding nodes...");
            foreach (var nodeDto in dto.Nodes.OrderBy(n => n.Id))
            {
                var nodeType = ParseNodeType(nodeDto.Type);
                var location = new Location(nodeDto.Location.X, nodeDto.Location.Y);

                var node = new GraphNode(
                    id: nodeDto.Id,
                    name: nodeDto.Name,
                    type: nodeType,
                    location: location,
                    zoneId: nodeDto.ZoneId
                );

                graph.AddNode(node);
            }

            // 5. Élek hozzáadása
            Console.WriteLine("\n🔗 Adding edges...");
            foreach (var edgeDto in dto.Edges)
            {
                graph.AddEdge(
                    nodeId1: edgeDto.From,
                    nodeId2: edgeDto.To,
                    idealTimeMinutes: edgeDto.IdealTimeMinutes
                );
            }

            Console.WriteLine("\n✅ City graph loaded successfully!\n");

            return graph;
        }

        /// <summary>
        /// String → NodeType enum konverzió.
        /// </summary>
        private static NodeType ParseNodeType(string typeString)
        {
            return typeString.ToLower() switch
            {
                "warehouse" => NodeType.Warehouse,
                "deliverypoint" => NodeType.DeliveryPoint,
                "intersection" => NodeType.Intersection,
                _ => throw new ArgumentException($"Unknown node type: {typeString}")
            };
        }
    }
}
