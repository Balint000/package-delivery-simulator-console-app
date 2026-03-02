namespace package_delivery_simulator.Data.Dto
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Data Transfer Object a város gráf JSON betöltéséhez.
    /// </summary>
    public class CityGraphDto
    {
        [JsonPropertyName("cityName")]
        public string CityName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("nodes")]
        public List<NodeDto> Nodes { get; set; }

        [JsonPropertyName("edges")]
        public List<EdgeDto> Edges { get; set; }
    }

    /// <summary>
    /// Csúcs JSON reprezentáció.
    /// </summary>
    public class NodeDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }  // "Warehouse", "DeliveryPoint", "Intersection"

        [JsonPropertyName("location")]
        public LocationDto Location { get; set; }

        [JsonPropertyName("zoneId")]
        public int? ZoneId { get; set; }
    }

    /// <summary>
    /// Él JSON reprezentáció.
    /// </summary>
    public class EdgeDto
    {
        [JsonPropertyName("from")]
        public int From { get; set; }

        [JsonPropertyName("to")]
        public int To { get; set; }

        [JsonPropertyName("idealTimeMinutes")]
        public int IdealTimeMinutes { get; set; }
    }

    /// <summary>
    /// Lokáció JSON reprezentáció.
    /// </summary>
    public class LocationDto
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }
}
