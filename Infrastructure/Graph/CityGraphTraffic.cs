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
        ///
        /// JAVÍTÁS — MIÉRT VOLT SOK A KÉSÉS?
        ///
        /// RÉGI (hibás) logika:
        ///   change = _random.NextDouble() * 0.15   ← mindig 0 és +0.15 között! (átlag: +0.075)
        ///   5% eséllyel: change += 0.5             ← további növekedés
        ///   10% eséllyel: change = -0.1            ← FELÜLÍRJA, nem adódik hozzá
        ///
        ///   Várható érték hívásonként: ~+0.08 → spirál a 2.5x maximumig!
        ///   + TraversePath minden él átlépésénél meghívja → hosszabb útnál
        ///     garantáltan maxon van a forgalom → minden kézbesítés késik.
        ///
        /// ÚJ (kiegyensúlyozott) logika:
        ///   Alap: szimmetrikus véletlen változás (-0.05 → +0.05), átlag: 0
        ///   5% esély: "baleset" → +0.3 → +0.5 (rövid ideig magas forgalom)
        ///   15% esély: forgalom javulás → -0.1 → -0.2
        ///   Mean reversion: ha TrafficMultiplier > 1.5, extra -0.05 nyomás lefelé
        ///
        ///   Várható érték hívásonként: ~0 → stabil, reális forgalom.
        /// </summary>
        public void UpdateTrafficConditions()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var edge = _adjacencyMatrix[i, j];
                    if (edge == null) continue;

                    double change;

                    // 5% esély: "baleset" — rövid idejű, de jelentős forgalomnövekedés
                    if (_random.NextDouble() < 0.05)
                    {
                        change = 0.3 + _random.NextDouble() * 0.2; // +0.3 → +0.5
                    }
                    // 15% esély: forgalom enyhül (pl. zöld hullám, kevesebb autó)
                    else if (_random.NextDouble() < 0.15)
                    {
                        change = -(0.1 + _random.NextDouble() * 0.1); // -0.1 → -0.2
                    }
                    // 80%: kis, SZIMMETRIKUS változás — átlaga 0
                    else
                    {
                        change = (_random.NextDouble() - 0.5) * 0.1; // -0.05 → +0.05
                    }

                    // Mean reversion: ha már magas a forgalom, extra lefelé nyomás
                    // Ez megakadályozza, hogy tartósan 2.5x közelében ragadjon
                    if (edge.TrafficMultiplier > 1.5)
                        change -= 0.05;

                    double nextValue = Math.Max(0.8, edge.TrafficMultiplier + change);
                    edge.UpdateTraffic(nextValue);
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
                // TODO: ha implementálni szeretnénk:
                // edge.UpdateTraffic(edge.TrafficMultiplier + 0.05);
                // TPL esetén lock szükséges!
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
