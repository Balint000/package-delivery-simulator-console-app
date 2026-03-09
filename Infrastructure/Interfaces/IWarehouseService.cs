namespace package_delivery_simulator_console_app.Infrastructure.Interfaces;

using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Warehouse (raktár) kezelő service interfésze.
///
/// FELELŐSSÉGEK:
///   - Warehouse node-ok azonosítása a gráfban (Initialize)
///   - Legközelebbi warehouse keresés DIJKSTRA alapján
///   - Futárhoz legjobb warehouse meghatározása (zóna + távolság)
///   - Zóna alapján warehouse lekérdezése
///
/// KOORDINÁTA-MENTES: Minden metódus node ID-val dolgozik.
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
    /// </summary>
    /// <param name="nodeId">Kiindulási node ID</param>
    /// <returns>Legközelebbi warehouse node, vagy null ha egyik sem elérhető</returns>
    GraphNode? FindNearestWarehouseFromNode(int nodeId);

    /// <summary>
    /// A futárhoz legjobb warehouse meghatározása.
    ///
    /// LOGIKA (prioritás sorrendben):
    ///   1. A futár saját zónáiban lévő warehouse-ok közül a Dijkstra szerinti legközelebbi
    ///   2. Fallback: ha a futár zónáiban nincs warehouse → abszolút legközelebbi (bármely zónából)
    ///
    /// MIÉRT KERÜL IDE ÉS NEM A SZIMULÁCIÓBA?
    ///   A "melyik warehouse-ból induljon a futár" döntés warehouse-kezelési logika,
    ///   nem szimulációs logika. A DeliverySimulationService csak meghívja ezt,
    ///   és a visszakapott warehouse node-ból indul — nem tudja és nem kell tudja a részleteket.
    /// </summary>
    /// <param name="courier">A futár (zónái + jelenlegi pozíciója alapján dönt)</param>
    /// <returns>Legjobb warehouse node, vagy null ha semmi sem elérhető</returns>
    GraphNode? FindBestWarehouseForCourier(Courier courier);

    /// <summary>
    /// Megadja, hogy egy node warehouse-e.
    /// </summary>
    bool IsWarehouse(int nodeId);

    /// <summary>
    /// Egy adott zónában lévő warehouse node ID-ja.
    /// Ha a zónában nincs warehouse, null-t ad vissza.
    /// </summary>
    int? GetWarehouseInZone(int zoneId);
}
