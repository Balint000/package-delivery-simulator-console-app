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
            // Csak felső háromszög (irányítatlan gráf)
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge != null)
                    {
                        // Véletlenszerű változás: ±10%
                        double change = (_random.NextDouble() - 0.5) * 0.2;
                        edge.UpdateTraffic(edge.TrafficMultiplier + change);
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
