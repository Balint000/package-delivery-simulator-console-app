namespace package_delivery_simulator_console_app.Infrastructure.Graph
{
    using package_delivery_simulator.Domain.Entities;
    using package_delivery_simulator.Domain.ValueObjects;

    /// <summary>
    /// CityGraph alapvető funkciók: konstruktor, csúcs/él kezelés.
    /// PARTIAL CLASS - több fájlra bontva az átláthatóság érdekében.
    /// </summary>
    public partial class CityGraph : ICityGraph
    {
        // ====== PRIVATE MEZŐK ======

        /// <summary>
        /// Az összes csúcs listája. Index = csúcs ID-ja!
        /// </summary>
        private readonly List<GraphNode> _nodes;

        /// <summary>
        /// A CSÚCSMÁTRIX - 2D tömb az élek tárolására.
        /// _adjacencyMatrix[i, j] = Él súlya az i. és j. csúcs között.
        /// Null = nincs közvetlen út.
        /// IRÁNYÍTATLAN: [i,j] == [j,i] (ugyanaz az objektum!)
        /// </summary>
        private readonly EdgeWeight[,] _adjacencyMatrix;

        /// <summary>
        /// Maximális csúcsszám (fix mátrix méret).
        /// </summary>
        private readonly int _nodeCount;

        /// <summary>
        /// Véletlenszám generátor forgalom szimulációhoz.
        /// </summary>
        private readonly Random _random;

        // ====== PUBLIC PROPERTY ======

        /// <summary>
        /// Csak olvasható hozzáférés a csúcsokhoz.
        /// </summary>
        public IReadOnlyList<GraphNode> Nodes => _nodes.AsReadOnly();

        /// <summary>
        /// Csúcsok száma jelenleg a gráfban.
        /// </summary>
        public int NodeCount => _nodes.Count;

        /// <summary>
        /// Élek száma a gráfban.
        /// </summary>
        public int EdgeCount
        {
            get
            {
                int count = 0;
                // Csak felső háromszög (irányítatlan)
                for (int i = 0; i < _nodes.Count; i++)
                {
                    for (int j = i + 1; j < _nodes.Count; j++)
                    {
                        if (_adjacencyMatrix[i, j] != null)
                            count++;
                    }
                }
                return count;
            }
        }

        // ====== KONSTRUKTOR ======

        /// <summary>
        /// Új város gráf létrehozása.
        /// </summary>
        /// <param name="maxNodeCount">Maximum csúcsszám (alapértelmezett: 100)</param>
        public CityGraph(int maxNodeCount = 100)
        {
            _nodeCount = maxNodeCount;
            _nodes = new List<GraphNode>(maxNodeCount);
            _adjacencyMatrix = new EdgeWeight[maxNodeCount, maxNodeCount];
            _random = new Random();

            // Mátrix inicializálás - minden null
            for (int i = 0; i < maxNodeCount; i++)
            {
                for (int j = 0; j < maxNodeCount; j++)
                {
                    _adjacencyMatrix[i, j] = null;
                }
            }
        }

        // ====== CSÚCS KEZELÉS ======

        /// <summary>
        /// Csúcs hozzáadása a gráfhoz.
        /// A csúcs ID-jának egyeznie KELL a lista indexével!
        /// </summary>
        public void AddNode(GraphNode node)
        {
            if (_nodes.Count >= _nodeCount)
                throw new InvalidOperationException($"Maximum node count ({_nodeCount}) reached!");

            if (node.Id != _nodes.Count)
                throw new ArgumentException($"Node ID must be {_nodes.Count}, but got {node.Id}");

            _nodes.Add(node);
        }

        /// <summary>
        /// Csúcs lekérdezése ID alapján.
        /// </summary>
        public GraphNode? GetNode(int nodeId)
        {
            if (nodeId < 0 || nodeId >= _nodes.Count)
                return null;

            return _nodes[nodeId];
        }

        /// <summary>
        /// Csúcs keresése név alapján (case-insensitive).
        /// </summary>
        public GraphNode? FindNodeByName(string name)
        {
            return _nodes.FirstOrDefault(n =>
                n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        // ====== ÉL KEZELÉS ======

        /// <summary>
        /// Él hozzáadása két csúcs között (IRÁNYÍTATLAN!).
        /// Mindkét irányba UGYANAZ az EdgeWeight objektum kerül.
        /// </summary>
        public void AddEdge(int nodeId1, int nodeId2, int idealTimeMinutes)
        {
            if (nodeId1 < 0 || nodeId1 >= _nodes.Count ||
                nodeId2 < 0 || nodeId2 >= _nodes.Count)
            {
                throw new ArgumentOutOfRangeException(
                    $"Invalid node IDs: {nodeId1}, {nodeId2}");
            }

            if (nodeId1 == nodeId2)
                throw new ArgumentException("Self-loops not allowed!");

            // Új él súly
            var edgeWeight = new EdgeWeight(idealTimeMinutes);

            // IRÁNYÍTATLAN: mindkét irányba ugyanaz
            _adjacencyMatrix[nodeId1, nodeId2] = edgeWeight;
            _adjacencyMatrix[nodeId2, nodeId1] = edgeWeight;
        }

        /// <summary>
        /// Él lekérdezése két csúcs között.
        /// </summary>
        public EdgeWeight? GetEdge(int nodeId1, int nodeId2)
        {
            if (nodeId1 < 0 || nodeId1 >= _nodes.Count ||
                nodeId2 < 0 || nodeId2 >= _nodes.Count)
                return null;

            return _adjacencyMatrix[nodeId1, nodeId2];
        }

        /// <summary>
        /// Van-e közvetlen él két csúcs között?
        /// </summary>
        public bool HasEdge(int nodeId1, int nodeId2)
        {
            return GetEdge(nodeId1, nodeId2) != null;
        }

        /// <summary>
        /// Szomszédos csúcsok ID listája.
        /// </summary>
        public List<int> GetNeighbors(int nodeId)
        {
            var neighbors = new List<int>();

            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_adjacencyMatrix[nodeId, i] != null)
                {
                    neighbors.Add(i);
                }
            }

            return neighbors;
        }
    }
}
