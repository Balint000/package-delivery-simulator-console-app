namespace package_delivery_simulator.Infrastructure
{
    using package_delivery_simulator.Domain.Entities;
    using package_delivery_simulator.Domain.Enums;
    using package_delivery_simulator.Domain.ValueObjects;
    using package_delivery_simulator.Infrastructure.Graph;


    /// <summary>
    /// Segédosztály város gráfok építéséhez.
    /// Itt hozhatunk létre különböző példa városokat.
    /// </summary>
    public static class CityGraphBuilder
    {
        /// <summary>
        /// Példa város létrehozása 8 csúccsal.
        ///
        /// STRUKTÚRA:
        ///
        ///        North(1)
        ///           |
        ///     West(4)-Suburb(6)-Downtown(5)-East(2)
        ///           |              |
        ///       Warehouse(0)   Industrial(7)
        ///                          |
        ///                       South(3)
        ///
        /// CSÚCSOK:
        /// [0] Central Warehouse - Kiindulási pont
        /// [1] North District    - Kézbesítési cím
        /// [2] East Side         - Kézbesítési cím
        /// [3] South Park        - Kézbesítési cím
        /// [4] West End          - Kézbesítési cím
        /// [5] Downtown          - Kereszteződés
        /// [6] Suburb            - Kereszteződés
        /// [7] Industrial        - Kereszteződés
        /// </summary>
        public static CityGraph BuildExampleCity()
        {
            Console.WriteLine("🏗️  Building Example City...\n");

            // ===== 1. GRÁF LÉTREHOZÁSA (8 csúcs) =====
            var graph = new CityGraph(8);

            // ===== 2. CSÚCSOK HOZZÁADÁSA =====

            // [0] Raktár - középen
            graph.AddNode(new GraphNode(
                id: 0,
                name: "Central Warehouse",
                type: NodeType.Warehouse,
                location: new Location(0, 0),
                zoneId: 1
            ));

            // [1] Északi район - kézbesítési pont
            graph.AddNode(new GraphNode(
                id: 1,
                name: "North District",
                type: NodeType.DeliveryPoint,
                location: new Location(0, 5),
                zoneId: 1
            ));

            // [2] Keleti oldal - kézbesítési pont
            graph.AddNode(new GraphNode(
                id: 2,
                name: "East Side",
                type: NodeType.DeliveryPoint,
                location: new Location(5, 0),
                zoneId: 2
            ));

            // [3] Déli park - kézbesítési pont
            graph.AddNode(new GraphNode(
                id: 3,
                name: "South Park",
                type: NodeType.DeliveryPoint,
                location: new Location(0, -5),
                zoneId: 3
            ));

            // [4] Nyugati vég - kézbesítési pont
            graph.AddNode(new GraphNode(
                id: 4,
                name: "West End",
                type: NodeType.DeliveryPoint,
                location: new Location(-5, 0),
                zoneId: 4
            ));

            // [5] Belváros - kereszteződés
            graph.AddNode(new GraphNode(
                id: 5,
                name: "Downtown",
                type: NodeType.Intersection,
                location: new Location(2, 2),
                zoneId: 1
            ));

            // [6] Külváros - kereszteződés
            graph.AddNode(new GraphNode(
                id: 6,
                name: "Suburb",
                type: NodeType.Intersection,
                location: new Location(-3, 3),
                zoneId: 1
            ));

            // [7] Ipari negyed - kereszteződés
            graph.AddNode(new GraphNode(
                id: 7,
                name: "Industrial",
                type: NodeType.Intersection,
                location: new Location(3, -2),
                zoneId: 3
            ));

            Console.WriteLine(); // Üres sor az élek előtt

            // ===== 3. ÉLEK HOZZÁADÁSA =====
            // Formátum: AddEdge(csúcs1, csúcs2, ideális_idő_percben)

            // Raktárból induló utak
            graph.AddEdge(0, 5, idealTimeMinutes: 5);   // Warehouse -> Downtown (gyors)
            graph.AddEdge(0, 7, idealTimeMinutes: 6);   // Warehouse -> Industrial
            graph.AddEdge(0, 6, idealTimeMinutes: 7);   // Warehouse -> Suburb

            // Downtown kapcsolatai
            graph.AddEdge(5, 1, idealTimeMinutes: 8);   // Downtown -> North
            graph.AddEdge(5, 2, idealTimeMinutes: 7);   // Downtown -> East

            // Industrial kapcsolatai
            graph.AddEdge(7, 2, idealTimeMinutes: 4);   // Industrial -> East (nagyon gyors)
            graph.AddEdge(7, 3, idealTimeMinutes: 5);   // Industrial -> South

            // Suburb kapcsolatai
            graph.AddEdge(6, 4, idealTimeMinutes: 6);   // Suburb -> West
            graph.AddEdge(6, 1, idealTimeMinutes: 5);   // Suburb -> North

            // Kerülő út
            graph.AddEdge(4, 3, idealTimeMinutes: 9);   // West -> South (lassú, kerülő)

            Console.WriteLine("\n✅ Example City built successfully!");

            return graph;
        }

        /// <summary>
        /// Kisebb példa város teszteléshez (4 csúcs).
        /// </summary>
        public static CityGraph BuildSmallCity()
        {
            var graph = new CityGraph(4);

            graph.AddNode(new GraphNode(0, "Warehouse", NodeType.Warehouse, new Location(0, 0)));
            graph.AddNode(new GraphNode(1, "House A", NodeType.DeliveryPoint, new Location(3, 0)));
            graph.AddNode(new GraphNode(2, "House B", NodeType.DeliveryPoint, new Location(0, 3)));
            graph.AddNode(new GraphNode(3, "Junction", NodeType.Intersection, new Location(2, 2)));

            graph.AddEdge(0, 3, 5);
            graph.AddEdge(3, 1, 3);
            graph.AddEdge(3, 2, 3);
            graph.AddEdge(1, 2, 6);

            return graph;
        }
    }
}
