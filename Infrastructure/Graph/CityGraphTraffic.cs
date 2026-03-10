namespace package_delivery_simulator_console_app.Infrastructure.Graph
{
    /// <summary>
    /// CityGraph forgalom szimulációs funkciók.
    /// PARTIAL CLASS folytatása.
    /// </summary>
    public partial class CityGraph : ICityGraph
    {
        /// <summary>
        /// Véletlenszerű forgalom frissítése az összes élen.
        /// Szimuláció során rendszeresen hívandó!
        /// </summary>
        public void UpdateTrafficConditions()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        // Alapvetően pozitív irányba tolva (0 és 15% közötti növekedés)
                        double change = _random.NextDouble() * 0.15;

                        // Néha (5% esély) történjen egy "baleset", ami megduplázza a menetidőt
                        if (_random.NextDouble() < 0.05) // 0.05
                        {
                            change += 0.5;
                        }

                        // Ritkán (10% esély) javuljon a forgalom
                        if (_random.NextDouble() < 0.10)
                        {
                            change = -0.1;
                        }

                        double nextValue = Math.Max(0.8, edge.TrafficMultiplier + change);
                        edge.UpdateTraffic(nextValue);
                    }
                }
            }
        }

        /// <summary>
        /// Futár mozgás regisztrálása egy él mentén.
        /// Növeli a forgalmat az adott úton.
        /// </summary>
        public void RegisterCourierMovement(int fromNodeId, int toNodeId)
        {
            var edge = GetEdge(fromNodeId, toNodeId);
            if (edge != null)
            {
            }
        }

        /// <summary>
        /// Forgalom visszaállítása ideális állapotra minden élen.
        /// </summary>
        public void ResetAllTraffic()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    edge?.ResetToIdeal();
                }
            }
        }
    }
}
