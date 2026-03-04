// ============================================================
// OrderLoader.cs
// ============================================================
// Felelőssége: Rendelések (DeliveryOrder) betöltése JSON fájlból.
//
// Ugyanolyan logika mint a CourierLoader,
// csak DeliveryOrder típussal dolgozik.
// ============================================================

namespace package_delivery_simulator_console_app.Infrastructure.Loaders;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Rendelések betöltése JSON fájlból.
/// </summary>
public class OrderLoader
{
    // ====== FÜGGŐSÉGEK ======

    /// <summary>
    /// Logger a naplózáshoz.
    /// </summary>
    private readonly ILogger<OrderLoader> _logger;

    /// <summary>
    /// A betöltendő JSON fájl alapértelmezett elérési útja.
    /// </summary>
    private const string DefaultPath = "Data/Order.json";

    // ====== KONSTRUKTOR ======

    /// <summary>
    /// OrderLoader létrehozása.
    /// </summary>
    /// <param name="logger">Logger (DI-ból jön)</param>
    public OrderLoader(ILogger<OrderLoader> logger)
    {
        _logger = logger;
    }

    // ====== BETÖLTÉS ======

    /// <summary>
    /// Rendelések betöltése az alapértelmezett JSON fájlból.
    /// </summary>
    /// <param name="cancellationToken">Megszakítási jel</param>
    /// <returns>Rendelések listája</returns>
    public Task<List<DeliveryOrder>> LoadAsync(
        CancellationToken cancellationToken = default)
        => LoadFromFileAsync(DefaultPath, cancellationToken);

    /// <summary>
    /// Rendelések betöltése egy megadott JSON fájlból.
    /// </summary>
    /// <param name="jsonFilePath">JSON fájl elérési útja</param>
    /// <param name="cancellationToken">Megszakítási jel</param>
    /// <returns>Rendelések listája</returns>
    public async Task<List<DeliveryOrder>> LoadFromFileAsync(
        string jsonFilePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rendelések betöltése: {Path}", jsonFilePath);

        if (!File.Exists(jsonFilePath))
        {
            _logger.LogError(
                "Nem található a rendelés JSON fájl: {Path}", jsonFilePath);
            throw new FileNotFoundException(
                $"A rendelés adatfájl nem található: {jsonFilePath}");
        }

        string jsonContent = await File.ReadAllTextAsync(
            jsonFilePath, cancellationToken);

        var orders = JsonSerializer.Deserialize<List<DeliveryOrder>>(
            jsonContent,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // Ugyanaz mint a CourierLoader-ben:
                // a JSON-ban "Pending" szöveg van, nem 0-s szám.
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });

        orders ??= new List<DeliveryOrder>();

        _logger.LogInformation(
            "{Count} rendelés betöltve: {Path}", orders.Count, jsonFilePath);

        return orders;
    }
}
