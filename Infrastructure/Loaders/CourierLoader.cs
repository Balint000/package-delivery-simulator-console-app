// ============================================================
// CourierLoader.cs — SQLite verzió
// ============================================================
// Felelőssége: Futárok (Courier) betöltése SQLite adatbázisból.
//
// VÁLTOZÁSOK a JSON verzióhoz képest:
//   - A File.ReadAllTextAsync + JsonSerializer helyett
//     SqliteConnection + SqliteDataReader használatos.
//   - A zóna-kapcsolatokat (CourierZones tábla) egy második
//     lekérdezéssel töltjük be (N:M → két SELECT).
//   - A külső interfész (LoadAsync / LoadFromFileAsync szignatúrája)
//     VÁLTOZATLAN → Program.cs nem igényel módosítást!
//
// MIÉRT KÉT LEKÉRDEZÉS A ZÓNÁKHOZ?
//   Az SQL JOIN helyett szándékosan külön SELECT-et használunk,
//   mert így a kód jobban olvasható és egyértelműbb hibakereséskor.
//   (Teljesítményre ilyen kis adatnál nincs hatása.)
// ============================================================

namespace package_delivery_simulator_console_app.Infrastructure.Loaders;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator_console_app.Infrastructure.Database;

/// <summary>
/// Futárok betöltése SQLite adatbázisból.
/// </summary>
public class CourierLoader
{
    // ====================================================
    // FÜGGŐSÉGEK
    // ====================================================

    private readonly ILogger<CourierLoader> _logger;
    private readonly DatabaseInitializer _dbInitializer;

    // ====================================================
    // KONSTRUKTOR
    // ====================================================

    /// <summary>
    /// CourierLoader létrehozása.
    /// </summary>
    /// <param name="logger">Logger (DI-ból)</param>
    /// <param name="dbInitializer">
    ///     Az adatbázis inicializáló — innen kapjuk a connection string-et
    ///     és az OpenConnection() metódust.
    /// </param>
    public CourierLoader(
        ILogger<CourierLoader> logger,
        DatabaseInitializer dbInitializer)
    {
        _logger = logger;
        _dbInitializer = dbInitializer;
    }

    // ====================================================
    // BETÖLTÉS
    // ====================================================

    /// <summary>
    /// Futárok betöltése az SQLite adatbázisból.
    /// A régi LoadFromFileAsync névhez képest itt az útvonal
    /// paramétert nem használjuk (az adatbázis útját a
    /// DatabaseInitializer kezeli), de a szignatúra kompatibilis marad.
    /// </summary>
    /// <param name="cancellationToken">Megszakítási jel</param>
    /// <returns>Futárok listája</returns>
    public Task<List<Courier>> LoadAsync(
        CancellationToken cancellationToken = default)
        => LoadFromDatabaseAsync(cancellationToken);

    /// <summary>
    /// Futárok betöltése SQLite-ból.
    ///
    /// FOLYAMAT:
    ///   1. Couriers tábla beolvasása → Courier objektumok listája
    ///   2. CourierZones tábla beolvasása → AssignedZoneIds feltöltése
    /// </summary>
    public async Task<List<Courier>> LoadFromDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Futárok betöltése SQLite adatbázisból...");

        await using var connection = _dbInitializer.OpenConnection();

        // ---- 1. LÉPÉS: Futár alaptulajdonságok lekérdezése ----
        var couriers = new List<Courier>();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Id, Name, CurrentLocationX, CurrentLocationY,
                       Status, MaxCapacity
                FROM   Couriers
                ORDER  BY Id;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                // Status szövegből enum konverzió
                // ("Available" → CourierStatus.Available)
                var statusText = reader.GetString(4);
                var status = Enum.Parse<CourierStatus>(statusText, ignoreCase: true);

                couriers.Add(new Courier
                {
                    Id              = reader.GetInt32(0),
                    Name            = reader.GetString(1),
                    CurrentLocation = new Location(
                                          reader.GetDouble(2),
                                          reader.GetDouble(3)),
                    Status          = status,
                    MaxCapacity     = reader.GetInt32(5),

                    // Listákat itt inicializáljuk, a következő lépésben töltjük fel
                    AssignedZoneIds  = new List<int>(),
                    AssignedOrderIds = new List<int>()
                });
            }
        }

        // ---- 2. LÉPÉS: Zóna-hozzárendelések betöltése ----
        // CourierZones kapcsolótáblából töltjük a AssignedZoneIds listákat.
        // Egy futárhoz több zóna is tartozhat (pl. [1, 2, 3]).
        if (couriers.Count > 0)
        {
            // Egy lekérdezéssel hozzuk le az összes sort,
            // majd CourierId szerint szétválogatjuk
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT CourierId, ZoneId
                FROM   CourierZones
                ORDER  BY CourierId, ZoneId;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            // Lookup: courierId → Courier objektum (gyors kereséshez)
            var courierById = couriers.ToDictionary(c => c.Id);

            while (await reader.ReadAsync(cancellationToken))
            {
                int courierId = reader.GetInt32(0);
                int zoneId    = reader.GetInt32(1);

                if (courierById.TryGetValue(courierId, out var courier))
                    courier.AssignedZoneIds.Add(zoneId);
            }
        }

        _logger.LogInformation(
            "{Count} futár betöltve az adatbázisból.", couriers.Count);

        return couriers;
    }
}