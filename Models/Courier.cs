namespace PackageDelivery.Models;

/// <summary>
/// A futárokat reprezentáló osztály.
/// Tartalmazza a pillanatnyi pozíciót az algoritmushoz és a teljesítményadatokat a statisztikához.
/// </summary>
public class Courier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Koordináták: A "Legközelebbi futár" (Greedy) algoritmus alapja.
    public double CurrentLocationX { get; set; }
    public double CurrentLocationY { get; set; }

    // Állapotjelző: Szabad-e a futár? (A TPL párhuzamos szimulációnál kulcsfontosságú)
    public bool IsAvailable { get; set; } = true;

    // Teljesítmény adatok a végső sorrendezéshez
    public int CompletedDeliveries { get; set; } = 0; // Hány sikeres kézbesítés?
    public double TotalDistanceTraveled { get; set; } = 0; // Megtett út km-ben
    public int TotalDelayMinutes { get; set; } = 0; // Összesített késés a teljesítmény méréséhez
}
