namespace package_delivery_simulator_console_app.Infrastructure.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Warehouse (raktár) kezelő service interfész.
///
/// EGYSZERŰSÍTÉS: Koordinátás metódus eltávolítva.
/// A rendszerben MINDENHOL node ID-val dolgozunk — nincs szükség
/// koordináta → node közelítésre. A futárok és rendelések már
/// eleve node ID-val tárolják a pozíciójukat.
///
/// FELELŐSSÉGEK:
/// - Warehouse node-ok azonosítása a gráfban (Initialize)
/// - Legközelebbi warehouse keresés DIJKSTRA alapján (node ID → node ID)
/// - Zóna alapján warehouse lekérdezése
/// </summary>
public interface IWarehouseService
{
    /// <summary>
    /// Megkeresi az összes Warehouse típusú node-ot a gráfban és cache-eli.
    /// Az alkalmazás indulásakor EGYSZER kell meghívni!
    /// </summary>
    void Initialize();

    /// <summary>
    /// Az összes raktár node listája (cache-ből, gyors).
    /// </summary>
    IReadOnlyList<GraphNode> GetAllWarehouses();

    /// <summary>
    /// Legközelebbi warehouse keresése egy adott node-tól — DIJKSTRA alapján.
    ///
    /// Ha a megadott node maga is warehouse, azonnal visszaadja.
    /// Egyébként minden warehouse-hoz Dijkstrát futtat és a legkisebb
    /// útvonal-időjűt adja vissza.
    ///
    /// NINCS koordináta-közelítés — tisztán gráf-alapú!
    /// </summary>
    /// <param name="nodeId">Kiindulási node ID (pl. courier.CurrentNodeId)</param>
    /// <returns>Legközelebbi warehouse node, vagy null ha egyik sem elérhető</returns>
    GraphNode? FindNearestWarehouseFromNode(int nodeId);

    /// <summary>
    /// Megadja, hogy egy node warehouse-e.
    /// </summary>
    bool IsWarehouse(int nodeId);

    /// <summary>
    /// Egy adott zónában lévő warehouse node ID-ja.
    /// Ha a zónában nincs warehouse, null-t ad vissza.
    /// Több warehouse esetén az elsőt adja vissza.
    /// </summary>
    int? GetWarehouseInZone(int zoneId);
}
