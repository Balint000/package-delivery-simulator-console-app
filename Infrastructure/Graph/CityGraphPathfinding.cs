namespace package_delivery_simulator.Infrastructure.Graph
{
    /// <summary>
    /// CityGraph útvonalkeresési funkciók (Dijkstra).
    /// PARTIAL CLASS folytatása.
    /// </summary>
    public partial class CityGraph
    {
        /// <summary>
        /// Legrövidebb út keresése Dijkstra algoritmussal.
        ///
        /// DIJKSTRA LÉPÉSEK:
        /// 1. Start csúcs távolsága = 0, többi = végtelen
        /// 2. Mindig a legközelebbi látogatatlan csúcsot választjuk
        /// 3. Frissítjük szomszédok távolságát ha jobb utat találunk
        /// 4. Ismételjük amíg el nem érjük a célt vagy kifogy a csúcs
        /// </summary>
        public (List<int> Path, int TotalTime) FindShortestPath(
            int startNodeId,
            int endNodeId)
        {
            // Validáció
            if (startNodeId < 0 || startNodeId >= _nodes.Count ||
                endNodeId < 0 || endNodeId >= _nodes.Count)
            {
                return (new List<int>(), int.MaxValue);
            }

            // Dijkstra adatstruktúrák
            var distances = new int[_nodes.Count];      // Legrövidebb távolságok
            var previous = new int?[_nodes.Count];      // Előző csúcs az útvonalon
            var visited = new bool[_nodes.Count];       // Látogatott csúcsok

            // Inicializálás
            for (int i = 0; i < _nodes.Count; i++)
            {
                distances[i] = int.MaxValue;  // Végtelen
                previous[i] = null;
                visited[i] = false;
            }
            distances[startNodeId] = 0;  // Start távolsága 0

            // Fő ciklus
            for (int count = 0; count < _nodes.Count - 1; count++)
            {
                // 1. Legközelebbi nem látogatott csúcs keresése
                int minDistance = int.MaxValue;
                int minIndex = -1;

                for (int i = 0; i < _nodes.Count; i++)
                {
                    if (!visited[i] && distances[i] < minDistance)
                    {
                        minDistance = distances[i];
                        minIndex = i;
                    }
                }

                // Nincs több elérhető csúcs
                if (minIndex == -1) break;

                // 2. Megjelöljük látogatottnak
                visited[minIndex] = true;

                // 3. Szomszédok frissítése
                for (int neighbor = 0; neighbor < _nodes.Count; neighbor++)
                {
                    var edge = _adjacencyMatrix[minIndex, neighbor];

                    if (edge != null && !visited[neighbor])
                    {
                        int newDistance = distances[minIndex] + edge.CurrentTimeMinutes;

                        if (newDistance < distances[neighbor])
                        {
                            distances[neighbor] = newDistance;
                            previous[neighbor] = minIndex;
                        }
                    }
                }
            }

            // Útvonal rekonstrukció visszafelé
            var path = new List<int>();
            int? current = endNodeId;

            while (current.HasValue)
            {
                path.Insert(0, current.Value);
                current = previous[current.Value];
            }

            // Validáció: van-e érvényes út?
            if (path.Count == 0 || path[0] != startNodeId)
            {
                return (new List<int>(), int.MaxValue);
            }

            return (path, distances[endNodeId]);
        }

        /// <summary>
        /// Ideális kézbesítési idő számítása (forgalom nélkül).
        /// Átmenetileg minden élt ideális állapotra állít.
        /// </summary>
        public int CalculateIdealTime(int startNodeId, int endNodeId)
        {
            // Eredeti forgalom mentése
            var originalMultipliers = new double[_nodes.Count, _nodes.Count];

            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = 0; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        originalMultipliers[i, j] = edge.TrafficMultiplier;
                        edge.UpdateTraffic(1.0);  // Ideális
                    }
                }
            }

            // Útvonal számítás ideális körülmények között
            var (_, idealTime) = FindShortestPath(startNodeId, endNodeId);

            // Eredeti forgalom visszaállítása
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = 0; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        edge.UpdateTraffic(originalMultipliers[i, j]);
                    }
                }
            }

            return idealTime;
        }
    }
}
