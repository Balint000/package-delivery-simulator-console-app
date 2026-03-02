namespace package_delivery_simulator.Presentation.Console;

/// <summary>
/// Statisztikai adatokDTO (Data Transfer Object).
/// Egyszerű adathordozó osztály a UI számára.
/// </summary>
public class SimulationStats
{
    /// <summary>
    /// Összes kézbesített csomag.
    /// </summary>
    public int TotalDeliveries { get; set; }

    /// <summary>
    /// Késett kézbesítések száma.
    /// </summary>
    public int TotalDelays { get; set; }

    /// <summary>
    /// Átlagos késés százalékban.
    /// </summary>
    public double DelayPercentage
    {
        get
        {
            if (TotalDeliveries == 0)
                return 0;

            return (double)TotalDelays / TotalDeliveries * 100;
        }
    }
}
