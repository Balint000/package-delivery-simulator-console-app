// ============================================================
// CourierLoader.cs
// ============================================================
// Felelőssége: Futárok (Courier) betöltése JSON fájlból.
//
// MIÉRT IDE KERÜL (Infrastructure/Loaders)?
// Az adatbetöltés "infrastruktúra" feladat — ugyanúgy ahogy
// a CityGraphLoader is itt van. A Service réteg nem tudja
// (és nem kell tudja), hogy az adatok JSON-ból, adatbázisból
// vagy bárhonnan máshonnan jönnek. Ez a réteg felelős érte.
// ============================================================

namespace package_delivery_simulator_console_app.Infrastructure.Loaders;

using System.Text.Json;                      // JSON olvasáshoz
using Microsoft.Extensions.Logging;         // Naplózáshoz
using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Futárok betöltése JSON fájlból.
/// </summary>
public class CourierLoader
{
    // ====== FÜGGŐSÉGEK ======

    /// <summary>
    /// Logger a naplózáshoz.
    /// </summary>
    private readonly ILogger<CourierLoader> _logger;

    /// <summary>
    /// A betöltendő JSON fájl alapértelmezett elérési útja.
    /// </summary>
    private const string DefaultPath = "Data/Courier.json";

    // ====== KONSTRUKTOR ======

    /// <summary>
    /// CourierLoader létrehozása.
    /// </summary>
    /// <param name="logger">Logger (DI-ból jön)</param>
    public CourierLoader(ILogger<CourierLoader> logger)
    {
        _logger = logger;
    }

    // ====== BETÖLTÉS ======

    /// <summary>
    /// Futárok betöltése az alapértelmezett JSON fájlból.
    /// Ez egy "kényelmi metódus" — meghívja a fő metódust az alapértelmezett úttal.
    /// </summary>
    /// <param name="cancellationToken">Megszakítási jel</param>
    /// <returns>Futárok listája</returns>
    public Task<List<Courier>> LoadAsync(
        CancellationToken cancellationToken = default)
        => LoadFromFileAsync(DefaultPath, cancellationToken);

    /// <summary>
    /// Futárok betöltése egy megadott JSON fájlból.
    ///
    /// MIÉRT async?
    /// A fájl olvasás I/O művelet — az operációs rendszer végzi,
    /// nem a CPU. Az await megvárja anélkül hogy blokkolná a szálat.
    /// </summary>
    /// <param name="jsonFilePath">JSON fájl elérési útja</param>
    /// <param name="cancellationToken">Megszakítási jel</param>
    /// <returns>Futárok listája</returns>
    public async Task<List<Courier>> LoadFromFileAsync(
        string jsonFilePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Futárok betöltése: {Path}", jsonFilePath);

        // Ellenőrzés: létezik-e a fájl?
        if (!File.Exists(jsonFilePath))
        {
            _logger.LogError(
                "Nem található a futár JSON fájl: {Path}", jsonFilePath);
            throw new FileNotFoundException(
                $"A futár adatfájl nem található: {jsonFilePath}");
        }

        // Fájl tartalmának beolvasása szövegként (aszinkron)
        string jsonContent = await File.ReadAllTextAsync(
            jsonFilePath, cancellationToken);

        // JSON szöveg → List<Courier> (deszerializálás)
        // PropertyNameCaseInsensitive: kis-nagybetű érzéketlenség
        // pl. "name" és "Name" is elfogadott a JSON-ban
        var couriers = JsonSerializer.Deserialize<List<Courier>>(
            jsonContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Ha a fájl üres vagy hibás volt, adjunk vissza üres listát
        // A "??=" operátor: "ha null, akkor legyen új lista"
        couriers ??= new List<Courier>();

        _logger.LogInformation(
            "{Count} futár betöltve: {Path}", couriers.Count, jsonFilePath);

        return couriers;
    }
}
