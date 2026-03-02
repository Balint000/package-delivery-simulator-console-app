namespace package_delivery_simulator.Domain.Enums
{
    /// <summary>
    /// A gráf csúcsainak típusai.
    /// Meghatározza, hogy egy adott pont mit reprezentál a városban.
    /// </summary>
    public enum NodeType
    {
        /// <summary>
        /// Raktár - ahol a futárok indulnak és ahova visszatérnek.
        /// Általában a város központjában helyezkedik el.
        /// Példa: "Central Warehouse", "Distribution Center"
        /// </summary>
        Warehouse,

        /// <summary>
        /// Kézbesítési pont - ahol a csomagokat le kell adni.
        /// Ezek az ügyfélcímek, házak, irodák.
        /// Példa: "North District House", "East Side Office"
        /// </summary>
        DeliveryPoint,

        /// <summary>
        /// Útkereszteződés - köztes pont az útvonalakon.
        /// Nem cél, csak áthaladási pont a navigáció során.
        /// Példa: "Main Intersection", "Highway Junction"
        /// </summary>
        Intersection
    }
}
