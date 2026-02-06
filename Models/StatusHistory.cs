namespace PackageDelivery.Models;

/// <summary>
/// Naplózza a rendelések állapotváltozásait.
/// Szükséges a "StatusHistory" DB elváráshoz.
/// </summary>
public class StatusHistory
{
    public int Id { get; set; }
    public int DeliveryOrderId { get; set; }

    // Milyen állapotba került a csomag?
    public string NewStatus { get; set; } = string.Empty;

    // Mikor történt a változás?
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Megjegyzés (pl. "Futár kijelölve", "Forgalmi dugó miatti késés")
    public string Comment { get; set; } = string.Empty;
}
