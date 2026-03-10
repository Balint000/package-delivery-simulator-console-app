namespace package_delivery_simulator_console_app.Infrastructure.Graph
{
    /// <summary>
    /// CityGraph útvonalkeresési funkciók (Dijkstra).
    /// PARTIAL CLASS folytatása.
    /// </summary>
    public partial class CityGraph : ICityGraph
    {
        /// <summary>
        /// Legrövidebb út keresése Dijkstra algoritmussal.
        ///
        /// DIJKSTRA LÉPÉSEK:
        /// 1. Start csúcs távolsága = 0, többi = végtelen
        /// 2. Mindig a legközelebbi látogatatlan csúcsot választjuk
        /// 3. Frissítjük szomszédok távolságát ha jobb utat találunk
        /// 4. Ismételjük amíg el nem érjük a célt vagy kifogy a csúcs
        ///
        /// THREAD-SAFETY:
        /// Csak olvas az _adjacencyMatrix-ból (CurrentTimeMinutes),
        /// semmit nem ír — párhuzamos futárok egyszerre hívhatják.
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

                // 3. Szomszédok frissítése — CurrentTimeMinutes-t OLVASSUK (nem írjuk)
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
        /// Ideális kézbesítési idő számítása forgalom NÉLKÜL.
        ///
        /// RÉGI MEGKÖZELÍTÉS (thread-unsafe volt!):
        ///   Átmenetileg minden élt 1.0x-ra állított → Dijkstra → visszaállított.
        ///   Ha két futár egyszerre hívta, az egyik futár "ideális" körülmények
        ///   közt mért, miközben a másik épp visszaállított — hibás eredmény.
        ///
        /// ÚJ MEGKÖZELÍTÉS (thread-safe):
        ///   Saját, önálló Dijkstrát futtat, ami közvetlenül az IdealTimeMinutes-t
        ///   olvassa — az _adjacencyMatrix-ot egyáltalán NEM MÓDOSÍTJA.
        ///   Párhuzamos futárok egyszerre hívhatják, nem zavarják egymást.
        ///
        /// MIÉRT BIZTONSÁGOS?
        ///   Az IdealTimeMinutes a gráf betöltése után soha nem változik (readonly jellegű).
        ///   Csak olvasunk — és a lokális distances[], visited[] tömbök minden
        ///   hívásban újak, nem osztottak meg a szálak között.
        /// </summary>
        public int CalculateIdealTime(int startNodeId, int endNodeId)
        {
            // Validáció
            if (startNodeId < 0 || startNodeId >= _nodes.Count ||
                endNodeId < 0 || endNodeId >= _nodes.Count)
            {
                return int.MaxValue;
            }

            // Lokális Dijkstra — ugyanaz a logika, de IdealTimeMinutes-szal
            // Ezek a tömbök csak ehhez a híváshoz tartoznak (stack/heap lokális),
            // tehát más szálak hívásaival NEM keverednek össze.
            var distances = new int[_nodes.Count];
            var visited = new bool[_nodes.Count];

            for (int i = 0; i < _nodes.Count; i++)
            {
                distances[i] = int.MaxValue;
                visited[i] = false;
            }
            distances[startNodeId] = 0;

            for (int count = 0; count < _nodes.Count - 1; count++)
            {
                // Legközelebbi nem látogatott csúcs
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

                if (minIndex == -1) break;
                visited[minIndex] = true;

                // Szomszédok frissítése — IDEÁLIS idővel (IdealTimeMinutes)
                // Ez az egyetlen különbség a FindShortestPath-hez képest!
                for (int neighbor = 0; neighbor < _nodes.Count; neighbor++)
                {
                    var edge = _adjacencyMatrix[minIndex, neighbor];

                    if (edge != null && !visited[neighbor])
                    {
                        // ↓ IdealTimeMinutes — nem CurrentTimeMinutes!
                        int newDistance = distances[minIndex] + edge.IdealTimeMinutes;

                        if (newDistance < distances[neighbor])
                        {
                            distances[neighbor] = newDistance;
                        }
                    }
                }
            }

            return distances[endNodeId];
        }
    }
}
