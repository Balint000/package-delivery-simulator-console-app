namespace package_delivery_simulator_console_app.Infrastructure.Configuration;

public sealed class DataOptions
{
    public const string SectionName = "Data";

    // Alap mappa, ahol a JSON-ok vannak (pl. "Data")
    public string BasePath { get; set; } = "Data";

    // Város gráf fájl neve
    public string CityGraphFileName { get; set; } = "city-graph.json";

    // Később ide jöhet Courier.json, Order.json stb.
    public string CourierFileName { get; set; } = "Courier.json";

    public string OrderFileName { get; set; } = "Order.json";
}
