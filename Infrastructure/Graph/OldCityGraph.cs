namespace package_delivery_simulator.Infrastructure.Graph
{
    using package_delivery_simulator.Domain.Entities;
    using package_delivery_simulator.Domain.ValueObjects;

    /// <summary>
    /// A város gráf reprezentációja CSÚCSMÁTRIX használatával.
    ///
    /// MIT CSINÁL EZ AZ OSZTÁLY?
    /// - Tárolja a város összes pontját (GraphNode lista)
    /// - Tárolja az utak közöttük (EdgeWeight 2D tömb = csúcsmátrix)
    /// - Képes legrövidebb utat számolni (Dijkstra algoritmus)
    /// - Szimulálja a forgalom változásait
    ///
    /// CSÚCSMÁTRIX ALAPFOGALOM:
    /// Ha van N csúcsunk, akkor van egy NxN méretű mátrixunk.
    /// _adjacencyMatrix[i, j] = Az él súlya az i. és j. csúcs között
    /// Ha null → nincs közvetlen út i és j között
    ///
    /// PÉLDA 3 csúccsal:
    ///       0   1   2
    ///   0 [null, 5, null]  → 0-ból 1-be 5 perc, 2-be nincs közvetlen út
    ///   1 [5, null, 3]     → 1-ből 0-ba 5 perc (irányítatlan!), 2-be 3 perc
    ///   2 [null, 3, null]  → 2-ből 1-be 3 perc
    /// </summary>
    public class OldCityGraph
    {
        // ====== PRIVATE MEZŐK ======

        /// <summary>
        /// Az összes csúcs listája.
        /// Index = csúcs ID-ja!
        /// Példa: _nodes[0] = Central Warehouse, _nodes[1] = North District
        /// </summary>
        private readonly List<GraphNode> _nodes;

        /// <summary>
        /// A CSÚCSMÁTRIX - itt tároljuk az éleket!
        /// 2D tömb: minden sor egy csúcsból induló élek, minden oszlop egy célcsúcs.
        ///
        /// _adjacencyMatrix[i, j] = Él az i. csúcsból a j. csúcsba
        /// - Ha null → nincs közvetlen út
        /// - Ha EdgeWeight objektum → van út, benne van az idő és forgalom
        ///
        /// IRÁNYÍTATLAN GRÁF: _adjacencyMatrix[i, j] == _adjacencyMatrix[j, i]
        /// (Ugyanaz az objektum mindkét irányban!)
        /// </summary>
        private readonly EdgeWeight[,] _adjacencyMatrix;

        /// <summary>
        /// Maximális csúcsszám (fix méret a mátrixnak).
        /// </summary>
        private readonly int _nodeCount;

        /// <summary>
        /// Véletlenszám generátor a forgalom szimulációhoz.
        /// </summary>
        private readonly Random _random;

        // ====== PUBLIC PROPERTY ======

        /// <summary>
        /// Csak olvasható hozzáférés a csúcsokhoz kívülről.
        /// Nem lehet módosítani a listát, csak olvasni.
        /// </summary>
        public IReadOnlyList<GraphNode> Nodes
        {
            get
            {
                return _nodes.AsReadOnly();
            }
        }

        // ====== KONSTRUKTOR ======

        /// <summary>
        /// Új város gráf létrehozása.
        /// </summary>
        /// <param name="nodeCount">Hány csúcsot szeretnénk maximum? (pl. 10, 20, 50)</param>
        public OldCityGraph(int nodeCount)
        {
            _nodeCount = nodeCount;
            _nodes = new List<GraphNode>(nodeCount); // Lista a csúcsoknak
            _adjacencyMatrix = new EdgeWeight[nodeCount, nodeCount]; // NxN mátrix
            _random = new Random();

            // INICIALIZÁLÁS: Minden mátrix elem kezdetben null (nincs él)
            for (int i = 0; i < nodeCount; i++)
            {
                for (int j = 0; j < nodeCount; j++)
                {
                    _adjacencyMatrix[i, j] = null; // Nincs él
                }
            }
        }

        // ====== CSÚCS HOZZÁADÁS ======

        /// <summary>
        /// Csúcs hozzáadása a gráfhoz.
        ///
        /// FONTOS: A csúcs ID-ja EGYEZZEN a lista indexével!
        /// Példa: Ha node.Id = 3, akkor ez lesz a _nodes[3] elem.
        /// </summary>
        /// <param name="node">A hozzáadandó csúcs</param>
        public void AddNode(GraphNode node)
        {
            // Ellenőrzés: elértük-e a limitet?
            if (_nodes.Count >= _nodeCount)
                throw new InvalidOperationException($"Cannot add more nodes! Limit is {_nodeCount}.");

            // Hozzáadjuk a listához
            _nodes.Add(node);

            Console.WriteLine($"✅ Node added: {node}");
        }

        // ====== ÉL HOZZÁADÁS (IRÁNYÍTATLAN) ======

        /// <summary>
        /// Él hozzáadása két csúcs között (IRÁNYÍTATLAN!).
        /// Mivel irányítatlan, mindkét irányba létrejön az él.
        ///
        /// PÉLDA:
        /// AddEdge(0, 1, 5) → Warehouse és North District között 5 perc
        /// Ez létrehozza:
        ///   - _adjacencyMatrix[0, 1] = EdgeWeight(5)
        ///   - _adjacencyMatrix[1, 0] = UGYANAZ az EdgeWeight objektum!
        ///
        /// Így ha az egyik él forgalma változik, a másik is automatikusan!
        /// </summary>
        /// <param name="nodeId1">Első csúcs ID-ja</param>
        /// <param name="nodeId2">Második csúcs ID-ja</param>
        /// <param name="idealTimeMinutes">Ideális utazási idő percben</param>
        public void AddEdge(int nodeId1, int nodeId2, int idealTimeMinutes)
        {
            // Validáció: léteznek-e ezek a csúcsok?
            if (nodeId1 < 0 || nodeId1 >= _nodes.Count || nodeId2 < 0 || nodeId2 >= _nodes.Count)
            {
                throw new ArgumentOutOfRangeException($"Invalid node IDs: {nodeId1}, {nodeId2}");
            }

            // Új él súly létrehozása
            var edgeWeight = new EdgeWeight(idealTimeMinutes);

            // IRÁNYÍTATLAN: mindkét irányba UGYANAZ az objektum!
            _adjacencyMatrix[nodeId1, nodeId2] = edgeWeight;
            _adjacencyMatrix[nodeId2, nodeId1] = edgeWeight; // Ugyanaz!

            Console.WriteLine($"✅ Edge added: {_nodes[nodeId1].Name} <-> {_nodes[nodeId2].Name} ({idealTimeMinutes} min)");
        }


        // ====== ÉL LEKÉRDEZÉS ======

        /// <summary>
        /// Él lekérdezése két csúcs között.
        /// Visszaadja az EdgeWeight objektumot, vagy null-t ha nincs közvetlen út.
        /// </summary>
        /// <param name="nodeId1">Első csúcs ID</param>
        /// <param name="nodeId2">Második csúcs ID</param>
        /// <returns>EdgeWeight vagy null</returns>
        public EdgeWeight GetEdge(int nodeId1, int nodeId2)
        {
            // Validáció
            if (nodeId1 < 0 || nodeId1 >= _nodes.Count || nodeId2 < 0 || nodeId2 >= _nodes.Count)
                return null;

            // Visszaadjuk a mátrix megfelelő elemét
            return _adjacencyMatrix[nodeId1, nodeId2];
        }

        /// <summary>
        /// Ellenőrzi, van-e közvetlen él két csúcs között.
        /// </summary>
        public bool HasEdge(int nodeId1, int nodeId2)
        {
            return GetEdge(nodeId1, nodeId2) != null;
        }

        /// <summary>
        /// Szomszédos csúcsok lekérdezése.
        /// Visszaadja azon csúcsok ID-it, amikhez KÖZVETLEN él vezet innen.
        ///
        /// PÉLDA: Ha 0. csúcsból vannak élek 1, 3, 5 csúcsokba
        ///        → GetNeighbors(0) = [1, 3, 5]
        /// </summary>
        /// <param name="nodeId">Kiindulási csúcs ID</param>
        /// <returns>Szomszédos csúcsok ID listája</returns>
        public List<int> GetNeighbors(int nodeId)
        {
            var neighbors = new List<int>();

            // Végignézzük a mátrix nodeId. sorát
            for (int i = 0; i < _nodes.Count; i++)
            {
                // Ha nem null → van él
                if (_adjacencyMatrix[nodeId, i] != null)
                {
                    neighbors.Add(i);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Csúcs lekérdezése ID alapján.
        /// </summary>
        public GraphNode GetNode(int nodeId)
        {
            if (nodeId < 0 || nodeId >= _nodes.Count)
                return null;

            return _nodes[nodeId];
        }

        /// <summary>
        /// Csúcs keresése név alapján.
        /// Hasznos, ha stringgel hivatkozunk egy helyre.
        /// </summary>
        public GraphNode FindNodeByName(string name)
        {
            return _nodes.FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }



        // ====== LEGRÖVIDEBB ÚT KERESÉS (DIJKSTRA) ======

        /// <summary>
        /// Legrövidebb út keresése két csúcs között Dijkstra algoritmussal.
        ///
        /// DIJKSTRA ALGORITMUS EGYSZERŰEN:
        /// 1. Induló csúcsból indul, távolsága 0
        /// 2. Mindig a legközelebbi, még nem látogatott csúcsot választja
        /// 3. Frissíti a szomszédok távolságát, ha jobb utat talál
        /// 4. Így garantáltan megtalálja a LEGRÖVIDEBB utat!
        ///
        /// PÉLDA:
        ///    A --5-- B
        ///    |       |
        ///    3       2
        ///    |       |
        ///    C --1-- D
        ///
        /// A-ból D-be:
        /// - A->B->D = 5+2 = 7
        /// - A->C->D = 3+1 = 4  ← EZ A RÖVIDEBB! ✅
        /// </summary>
        /// <param name="startNodeId">Kiindulási csúcs</param>
        /// <param name="endNodeId">Cél csúcs</param>
        /// <returns>
        /// Tuple:
        ///   - Path: Csúcsok ID listája az útvonalon
        ///   - TotalTime: Összes utazási idő percben
        /// </returns>
        public (List<int> Path, int TotalTime) FindShortestPath(int startNodeId, int endNodeId)
        {
            // Validáció
            if (startNodeId < 0 || startNodeId >= _nodes.Count ||
                endNodeId < 0 || endNodeId >= _nodes.Count)
            {
                return (new List<int>(), int.MaxValue); // Nincs út
            }

            // ===== DIJKSTRA ADATSTRUKTÚRÁK =====

            // distances[i] = Legrövidebb ismert távolság a start-ból az i. csúcsig
            var distances = new int[_nodes.Count];

            // previous[i] = Melyik csúcsból érkeztünk az i. csúcsba a legrövidebb úton?
            // Ezzel rekonstruáljuk később az útvonalat!
            var previous = new int?[_nodes.Count];

            // visited[i] = Járt már itt az algoritmus?
            var visited = new bool[_nodes.Count];

            // ===== INICIALIZÁLÁS =====

            for (int i = 0; i < _nodes.Count; i++)
            {
                distances[i] = int.MaxValue; // Kezdetben végtelen távol van minden
                previous[i] = null;          // Nincs előző csúcs
                visited[i] = false;          // Sehol nem jártunk még
            }

            // A kiinduló csúcs távolsága 0 (önmagától 0 távolságra van)
            distances[startNodeId] = 0;

            // ===== DIJKSTRA FŐ CIKLUS =====

            // Maximum N-1 iteráció (N = csúcsok száma)
            for (int count = 0; count < _nodes.Count - 1; count++)
            {
                // 1. LÉPÉS: Keressük meg a legközelebbi, még nem látogatott csúcsot

                int minDistance = int.MaxValue;
                int minIndex = -1;

                for (int i = 0; i < _nodes.Count; i++)
                {
                    // Ha még nem jártunk itt ÉS közelebb van mint a jelenlegi minimum
                    if (!visited[i] && distances[i] < minDistance)
                    {
                        minDistance = distances[i];
                        minIndex = i;
                    }
                }

                // Ha nincs több elérhető csúcs → kész
                if (minIndex == -1) break;

                // 2. LÉPÉS: Megjelöljük ezt a csúcsot látogatottnak
                visited[minIndex] = true;

                // 3. LÉPÉS: Frissítjük a szomszédok távolságát

                // Végigmegyünk minden szomszédon
                for (int neighbor = 0; neighbor < _nodes.Count; neighbor++)
                {
                    // Van-e él a minIndex és neighbor között?
                    var edge = _adjacencyMatrix[minIndex, neighbor];

                    if (edge != null && !visited[neighbor])
                    {
                        // Számoljuk ki az új távolságot ezen az úton
                        // Új távolság = (jelenlegi csúcsig vezető távolság) + (él súlya)
                        int newDistance = distances[minIndex] + edge.CurrentTimeMinutes;

                        // Ha ez jobb mint a jelenlegi ismert távolság
                        if (newDistance < distances[neighbor])
                        {
                            // Frissítjük!
                            distances[neighbor] = newDistance;
                            previous[neighbor] = minIndex; // Ide a minIndex-ből érkeztünk
                        }
                    }
                }
            }

            // ===== ÚTVONAL REKONSTRUKCIÓ =====

            // Az útvonalat visszafelé építjük fel a previous tömb alapján
            var path = new List<int>();
            int? current = endNodeId;

            // Amíg van előző csúcs
            while (current.HasValue)
            {
                path.Insert(0, current.Value); // Beszúrjuk az elejére
                current = previous[current.Value]; // Lépünk az előző csúcsra
            }

            // Ha nincs útvonal (nem érhető el a cél)
            if (path.Count == 0 || path[0] != startNodeId)
            {
                return (new List<int>(), int.MaxValue);
            }

            // Visszaadjuk az útvonalat és a teljes időt
            return (path, distances[endNodeId]);
        }




        // ====== IDEÁLIS IDŐ SZÁMÍTÁS ======

        /// <summary>
        /// Ideális kézbesítési idő számítása FORGALOM NÉLKÜL.
        ///
        /// MIÉRT FONTOS EZ?
        /// - Megmondjuk az ügyfélnek: "Normál körülmények között 15 perc"
        /// - Ha a valós idő 25 perc → 10 perc késés!
        /// - Így detektálhatjuk a késéseket és értesíthetjük az ügyfelet
        ///
        /// HOGYAN MŰKÖDIK?
        /// 1. Átmenetileg minden él forgalmát 1.0-ra állítjuk (ideális)
        /// 2. Kiszámoljuk a legrövidebb utat
        /// 3. Visszaállítjuk az eredeti forgalmat
        /// </summary>
        /// <param name="startNodeId">Kiindulási csúcs</param>
        /// <param name="endNodeId">Cél csúcs</param>
        /// <returns>Ideális utazási idő percben</returns>
        public int CalculateIdealTime(int startNodeId, int endNodeId)
        {
            // ===== 1. MENTÉS: Eredeti forgalom elmentése =====

            var originalMultipliers = new double[_nodes.Count, _nodes.Count];

            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = 0; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        // Elmentjük az eredeti szorzót
                        originalMultipliers[i, j] = edge.TrafficMultiplier;

                        // Ideális állapotra állítjuk (1.0 = nincs forgalom)
                        edge.UpdateTraffic(1.0);
                    }
                }
            }

            // ===== 2. ÚTVONAL SZÁMÍTÁS ideális körülmények között =====

            var (_, idealTime) = FindShortestPath(startNodeId, endNodeId);

            // ===== 3. VISSZAÁLLÍTÁS: Eredeti forgalom visszarakása =====

            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = 0; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        // Visszaállítjuk az eredeti forgalmat
                        edge.UpdateTraffic(originalMultipliers[i, j]);
                    }
                }
            }

            return idealTime;
        }

        // ====== FORGALOM FRISSÍTÉS ======

        /// <summary>
        /// Véletlenszerű forgalom frissítése az ÖSSZES élen.
        ///
        /// SZIMULÁCIÓ SORÁN hívjuk meg rendszeresen!
        /// - Forgalom növekedhet (dugó)
        /// - Forgalom csökkenhet (üresebb utak)
        ///
        /// FONTOS: Csak a felső háromszögön megyünk végig!
        /// Mivel irányítatlan gráf, _adjacencyMatrix[i,j] == _adjacencyMatrix[j,i]
        /// → Elég csak az egyik irányba nézni (i < j)
        /// </summary>
        public void UpdateTrafficConditions()
        {
            // Csak a felső háromszög (i < j)
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++) // j = i+1 → felső háromszög
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        // Véletlenszerű változás: -10% ... +10%
                        // _random.NextDouble() → 0.0 és 1.0 között
                        // (0.0 - 0.5) * 0.2 = -0.1 ... +0.1
                        double change = (_random.NextDouble() - 0.5) * 0.2;

                        // Új forgalom szorzó
                        double newMultiplier = edge.TrafficMultiplier + change;

                        // Frissítjük az élt
                        edge.UpdateTraffic(newMultiplier);
                    }
                }
            }
        }

        // ====== FUTÁR MOZGÁS REGISZTRÁLÁS ======

        /// <summary>
        /// Futár mozgás regisztrálása egy él mentén.
        /// Amikor egy futár végigmegy egy úton, az növeli a forgalmat!
        ///
        /// LOGIKA: Több jármű az úton → nagyobb forgalom
        /// </summary>
        /// <param name="fromNodeId">Honnan indult</param>
        /// <param name="toNodeId">Hová ért</param>
        public void RegisterCourierMovement(int fromNodeId, int toNodeId)
        {
            var edge = GetEdge(fromNodeId, toNodeId);
            if (edge != null)
            {

                Console.WriteLine($"📍 Courier moved: {_nodes[fromNodeId].Name} -> {_nodes[toNodeId].Name} " +
                                  $"(traffic now: {edge.TrafficMultiplier:F2}x)");
            }
        }

        // ====== DEBUG KIÍRÁS ======

        /// <summary>
        /// Teljes gráf kiírása a konzolra debug célokra.
        /// Megmutatja az összes csúcsot és élt.
        /// </summary>
        public void PrintGraph()
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("           CITY GRAPH");
            Console.WriteLine(new string('=', 50));

            Console.WriteLine($"📍 Total Nodes: {_nodes.Count}");
            Console.WriteLine();

            // Csúcsok listázása
            Console.WriteLine("NODES:");
            foreach (var node in _nodes)
            {
                string zoneInfo = node.ZoneId.HasValue ? $"Zone {node.ZoneId.Value}" : "No Zone";
                Console.WriteLine($"  [{node.Id}] {node.Name,-20} ({node.Type,-15}) at {node.Location,-15} | {zoneInfo}");
            }

            // Élek listázása (csak felső háromszög, irányítatlan miatt)
            Console.WriteLine();
            Console.WriteLine("EDGES:");

            int edgeCount = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        edgeCount++;
                        Console.WriteLine($"  {_nodes[i].Name,-20} <--> {_nodes[j].Name,-20} | {edge}");
                    }
                }
            }

            Console.WriteLine($"\n📊 Total Edges: {edgeCount}");
            Console.WriteLine(new string('=', 50) + "\n");
        }
    }
}
