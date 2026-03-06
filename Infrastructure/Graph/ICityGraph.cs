namespace package_delivery_simulator_console_app.Infrastructure.Graph;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.ValueObjects;

/// <summary>
/// Város gráf interfész - DI container számára.
///
/// MIÉRT KELL EZ?
/// - Dependency Inversion: A service-ek nem konkrét CityGraph-ra,
///   hanem erre az interface-re támaszkodnak
/// - Tesztelhetőség: Könnyen mock-olható
/// - Cserélhetőség: Később más implementációt is használhatunk
///
/// CSAK A LEGFONTOSABB MŰVELETEK vannak itt!
/// Nem kell minden privát metódus, csak amit kívülről használni fogunk.
/// </summary>
public interface ICityGraph
{
    // ====== QUERY MŰVELETEK (OLVASÁS) ======

    /// <summary>
    /// Összes csúcs csak olvasható listája.
    /// </summary>
    IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>
    /// Csúcs lekérdezése ID alapján.
    /// </summary>
    GraphNode? GetNode(int nodeId);

    /// <summary>
    /// Csúcs keresése név alapján.
    /// </summary>
    GraphNode? FindNodeByName(string name);

    /// <summary>
    /// Él lekérdezése két csúcs között.
    /// </summary>
    EdgeWeight? GetEdge(int nodeId1, int nodeId2);

    /// <summary>
    /// Szomszédos csúcsok lekérdezése.
    /// </summary>
    List<int> GetNeighbors(int nodeId);

    // ====== PATHFINDING MŰVELETEK ======

    /// <summary>
    /// Legrövidebb út keresése Dijkstra algoritmussal (aktuális forgalommal).
    /// </summary>
    /// <returns>Tuple: (Útvonal csúcs ID-k, Teljes idő percben)</returns>
    (List<int> Path, int TotalTime) FindShortestPath(int startNodeId, int endNodeId);

    /// <summary>
    /// Ideális kézbesítési idő számítása FORGALOM NÉLKÜL.
    /// Használjuk ezt a késések detektálására!
    /// </summary>
    int CalculateIdealTime(int startNodeId, int endNodeId);

    // ====== TRAFFIC MŰVELETEK ======

    /// <summary>
    /// Forgalom véletlenszerű frissítése az összes élen.
    /// Hívjuk meg rendszeresen a szimuláció során!
    /// </summary>
    void UpdateTrafficConditions();

    /// <summary>
    /// Futár mozgás regisztrálása (növeli az él forgalmát).
    /// </summary>
    void RegisterCourierMovement(int fromNodeId, int toNodeId);

    // ====== DEBUG ======

    /// <summary>
    /// Teljes gráf kiírása konzolra (debug célra).
    /// NEM HASZNÁLJUK az élő UI-ban, csak kezdeti ellenőrzéshez!
    /// </summary>
    void PrintGraph();

    /// <summary>
    /// Útvonal kiírása konzolra (debug célra).
    /// </summary>
    void PrintPath(List<int> path, int totalTime);

}
