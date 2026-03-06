namespace package_delivery_simulator.Domain.Entities
{
    using package_delivery_simulator.Domain.Enums;
    using package_delivery_simulator.Domain.ValueObjects;

    /// <summary>
    /// Egy csúcs a város gráfjában.
    /// Minden GraphNode egy konkrét helyet reprezentál (raktár, cím, vagy kereszteződés).
    /// </summary>
    public class GraphNode
    {
        /// <summary>
        /// Egyedi azonosító a csúcsnak.
        /// Ez lesz az index a csúcsmátrixban! (0-tól indul)
        /// Példa: 0, 1, 2, 3...
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// A hely neve, emberek számára olvasható formában.
        /// Példa: "Central Warehouse", "North District", "Main Intersection"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Mi ez a csúcs? (Raktár, Kézbesítési pont, vagy Kereszteződés)
        /// </summary>
        public NodeType Type { get; set; }

        /// <summary>
        /// A csúcs földrajzi helyzete (x, y koordináták).
        /// Újrahasznosítjuk a meglévő Location osztályt a projektből!
        /// </summary>
        public Location Location { get; set; }

        /// <summary>
        /// Melyik zónához tartozik ez a csúcs? (Opcionális)
        /// Null lehet, ha nincs zónához rendelve.
        /// Példa: ZoneId = 1 → "North Zone"
        /// </summary>
        public int? ZoneId { get; set; }

        /// <summary>
        /// Konstruktor - új csúcs létrehozása.
        /// </summary>
        /// <param name="id">Csúcs azonosító (mátrix index)</param>
        /// <param name="name">Hely neve</param>
        /// <param name="type">Csúcs típusa</param>
        /// <param name="location">Földrajzi koordináták</param>
        /// <param name="zoneId">Zóna azonosító (opcionális)</param>
        public GraphNode(int id, string name, NodeType type, Location location, int? zoneId = null)
        {
            Id = id;
            Name = name;
            Type = type;
            Location = location;
            ZoneId = zoneId;
        }

        /// <summary>
        /// Szöveges reprezentáció debug célokra.
        /// </summary>
        public override string ToString()
        {
            return $"[{Id}] {Name} ({Type}) at {Location}";
        }
    }
}
