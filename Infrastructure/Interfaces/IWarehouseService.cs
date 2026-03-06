namespace package_delivery_simulator_console_app.Infrastructure.Interfaces;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.ValueObjects;

/// <summary>
/// Warehouse (raktár) kezelő service interfész.
///
/// FELELŐSSÉGEK:
/// - Warehouse node-ok azonosítása a gráfban
/// - Legközelebbi warehouse keresés GRÁF ÉLEK ALAPJÁN (Dijkstra)
/// - Futárok inicializálása warehouse-okhoz
///
/// FONTOS: Minden távolság számítás a gráf élein keresztül történik,
/// NEM Euklideszi koordinátákkal! A forgalom is befolyásolja az eredményt.
/// </summary>
public interface IWarehouseService
{
    /// <summary>
    /// Inicializálja a service-t - megkeresi az összes warehouse node-ot a gráfban.
    /// Csak egyszer kell meghívni az alkalmazás indulásakor!
    /// </summary>
    void Initialize();

    /// <summary>
    /// Összes warehouse node a gráfban.
    /// </summary>
    IReadOnlyList<GraphNode> GetAllWarehouses();

    /// <summary>
    /// Legközelebbi warehouse keresése egy koordináta alapján.
    ///
    /// MŰKÖDÉS:
    /// 1. Koordináta → legközelebbi node mapping (Euklideszi, mert nincs más opció)
    /// 2. Node → legközelebbi warehouse GRÁF ALAPJÁN (Dijkstra)
    ///
    /// HASZNÁLAT:
    /// - Futár inicializáláskor: melyik warehouse-hoz tartozik
    /// - Rendelés létrehozáskor: melyik warehouse-ból kell felvenni
    ///
    /// FIGYELEM: Az eredmény függ a JELENLEGI FORGALOMTÓL is!
    /// </summary>
    /// <param name="location">Kiindulási koordináta</param>
    /// <returns>Legközelebbi warehouse node (vagy null, ha nincs elérhető)</returns>
    GraphNode? FindNearestWarehouse(Location location);

    /// <summary>
    /// Legközelebbi warehouse keresése node ID alapján - GRÁF ÉLEK ALAPJÁN!
    ///
    /// MŰKÖDÉS:
    /// - Dijkstra algoritmussal kiszámítja a legrövidebb utat minden warehouse-hoz
    /// - A legkisebb útvonal idejű warehouse-t választja
    /// - Figyelembe veszi az aktuális forgalmat is!
    ///
    /// HASZNÁLAT:
    /// - Futár visszatéréskor: melyik warehouse a legközelebbi (útvonal időben)
    /// - Optimális warehouse választás már ismert node pozícióból
    /// </summary>
    /// <param name="nodeId">Node ID a gráfban</param>
    /// <returns>Legközelebbi warehouse node (vagy null, ha egyik sem elérhető)</returns>
    GraphNode? FindNearestWarehouseFromNode(int nodeId);

    /// <summary>
    /// Ellenőrzi, hogy egy adott node warehouse-e.
    /// </summary>
    /// <param name="nodeId">Node ID</param>
    /// <returns>True, ha warehouse</returns>
    bool IsWarehouse(int nodeId);

    /// <summary>
    /// Warehouse node ID lekérése zóna alapján.
    /// Ha egy zónában több warehouse van, a legelsőt adja vissza.
    ///
    /// HASZNÁLAT:
    /// - Futár inicializáláskor: melyik zónában melyik warehouse
    /// - Gyors lookup, ha tudjuk hogy melyik zónában vagyunk
    /// </summary>
    /// <param name="zoneId">Zóna ID</param>
    /// <returns>Warehouse node ID (vagy null, ha nincs a zónában warehouse)</returns>
    int? GetWarehouseInZone(int zoneId);
}
