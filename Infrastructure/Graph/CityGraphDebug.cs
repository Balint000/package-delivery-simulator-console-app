namespace package_delivery_simulator.Infrastructure.Graph
{
    /// <summary>
    /// CityGraph debug és kiíratási funkciók.
    /// PARTIAL CLASS folytatása.
    /// </summary>
    public partial class CityGraph
    {
        /// <summary>
        /// Teljes gráf kiírása a konzolra.
        /// </summary>
        public void PrintGraph()
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("                    CITY GRAPH");
            Console.WriteLine(new string('=', 60));

            Console.WriteLine($"📍 Nodes: {_nodes.Count} / {_nodeCount}");
            Console.WriteLine($"🔗 Edges: {EdgeCount}");
            Console.WriteLine();

            // Csúcsok
            Console.WriteLine("NODES:");
            Console.WriteLine(new string('-', 60));
            foreach (var node in _nodes)
            {
                string zoneInfo = node.ZoneId.HasValue
                    ? $"Zone {node.ZoneId.Value}"
                    : "No Zone";

                Console.WriteLine(
                    $"  [{node.Id,2}] {node.Name,-20} " +
                    $"({node.Type,-15}) " +
                    $"at {node.Location,-15} | {zoneInfo}");
            }

            // Élek
            Console.WriteLine();
            Console.WriteLine("EDGES:");
            Console.WriteLine(new string('-', 60));

            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        Console.WriteLine(
                            $"  {_nodes[i].Name,-20} <--> " +
                            $"{_nodes[j].Name,-20} | " +
                            $"{edge.CurrentTimeMinutes,2} min " +
                            $"(ideal: {edge.IdealTimeMinutes,2}, " +
                            $"traffic: {edge.TrafficMultiplier:F2}x)");
                    }
                }
            }

            Console.WriteLine(new string('=', 60) + "\n");
        }

        /// <summary>
        /// Útvonal kiírása részletesen.
        /// </summary>
        public void PrintPath(List<int> path, int totalTime)
        {
            if (path == null || path.Count == 0)
            {
                Console.WriteLine("❌ No path found!");
                return;
            }

            Console.WriteLine($"\n🗺️  PATH ({path.Count} nodes, {totalTime} min total):");
            Console.WriteLine(new string('-', 50));

            for (int i = 0; i < path.Count; i++)
            {
                var node = GetNode(path[i]);
                Console.Write($"  [{node.Id}] {node.Name}");

                if (i < path.Count - 1)
                {
                    var edge = GetEdge(path[i], path[i + 1]);
                    Console.WriteLine($" → ({edge.CurrentTimeMinutes} min)");
                }
                else
                {
                    Console.WriteLine(" ✓");
                }
            }

            Console.WriteLine(new string('-', 50));
        }
    }
}
