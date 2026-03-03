namespace package_delivery_simulator_console_app.Data.Dto;

using System.Collections.Generic;

// Gyökér objektum a city-graph.json-hoz
public sealed class CityGraphDto
{
    // Város neve (debug/kiíráshoz)
    public string CityName { get; set; } = string.Empty;

    // Szöveges leírás (debug)
    public string Description { get; set; } = string.Empty;

    // Csúcsok listája
    public List<CityGraphNodeJson> Nodes { get; set; } = new();

    // Élek listája
    public List<CityGraphEdgeJson> Edges { get; set; } = new();
}

// Egy csúcs a JSON-ben
public sealed class CityGraphNodeJson
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // "Warehouse", "DeliveryPoint", "Intersection" – ezt NodeType enumra kell map-pelni
    public string Type { get; set; } = string.Empty;

    public CityGraphLocationJson Location { get; set; } = new();

    public int? ZoneId { get; set; }
}

// Koordináták a JSON-ben
public sealed class CityGraphLocationJson
{
    public double X { get; set; }

    public double Y { get; set; }
}

// Egy él a JSON-ben
public sealed class CityGraphEdgeJson
{
    public int From { get; set; }

    public int To { get; set; }

    public int IdealTimeMinutes { get; set; }
}
