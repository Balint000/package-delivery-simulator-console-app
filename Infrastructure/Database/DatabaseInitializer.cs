// ============================================================
// DatabaseInitializer.cs
// ============================================================
// Felelőssége: SQLite adatbázis létrehozása és első feltöltése.
//
// LOGIKA:
//   1. Ha az adatbázis fájl nem létezik → létrehozza a táblákat
//      és beolvassa a meglévő JSON fájlokból az adatokat (seed).
//   2. Ha már létezik → nem csinál semmit (idempotens).
//
// TÁBLÁK:
//   Couriers         — futárok alaptulajdonságai
//   CourierZones     — futár ↔ zóna kapcsolótábla (N:M)
//   DeliveryOrders   — rendelések
//
// MIÉRT NEM ORM (Entity Framework)?
//   A projekt szándékosan kerüli a "varázslatos" keretrendszereket.
//   Az alacsony szintű ADO.NET stílusú hozzáférés jobban illeszkedik
//   az oktatási jellegű architektúrához, és nem hoz be felesleges
//   függőséget.
// ============================================================

namespace package_delivery_simulator_console_app.Infrastructure.Database;

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;

/// <summary>
/// SQLite adatbázis inicializáló és seed-elő osztály.
/// Csak egyszer kell meghívni a program indulásakor.
/// </summary>
public class DatabaseInitializer
{
    // ====================================================
    // KONFIGURÁCIÓ
    // ====================================================

    /// <summary>
    /// Az SQLite adatbázis fájl elérési útja.
    /// Ha más helyre szeretnéd, itt változtasd meg.
    /// </summary>
    public const string DefaultDatabasePath = "Data/simulator.db";

    private readonly string _databasePath;
    private readonly ILogger<DatabaseInitializer> _logger;

    // ====================================================
    // KONSTRUKTOR
    // ====================================================

    public DatabaseInitializer(
        ILogger<DatabaseInitializer> logger,
        string databasePath = DefaultDatabasePath)
    {
        _logger = logger;
        _databasePath = databasePath;
    }

    // ====================================================
    // FŐ METÓDUS
    // ====================================================

    /// <summary>
    /// Adatbázis inicializálása.
    /// Ha már létezik az adatbázis fájl, kihagyja a seed-elést.
    /// </summary>
    public async Task InitializeAsync(
        string courierJsonPath = "Data/Courier.json",
        string orderJsonPath   = "Data/Order.json",
        CancellationToken cancellationToken = default)
    {
        bool isNewDatabase = !File.Exists(_databasePath);

        _logger.LogInformation(
            isNewDatabase
                ? "Új SQLite adatbázis létrehozása: {Path}"
                : "Meglévő SQLite adatbázis használata: {Path}",
            _databasePath);

        // Könyvtár létrehozása ha szükséges (pl. Data/ mappa)
        var dir = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var connection = OpenConnection();

        // Táblák létrehozása (IF NOT EXISTS → biztonságos ismételt futtatáshoz)
        await CreateTablesAsync(connection, cancellationToken);

        if (isNewDatabase)
        {
            _logger.LogInformation("Adatok betöltése JSON fájlokból...");
            await SeedCouriersAsync(connection, courierJsonPath, cancellationToken);
            await SeedOrdersAsync(connection, orderJsonPath, cancellationToken);
            _logger.LogInformation("✅ Seed kész, adatbázis készen áll.");
        }
        else
        {
            _logger.LogInformation("✅ Adatbázis már létezik, seed kihagyva.");
        }
    }

    // ====================================================
    // SCHEMA LÉTREHOZÁS
    // ====================================================

    private static async Task CreateTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        // ---- Couriers tábla ----
        // A many-to-many kapcsolatokat (zónák, rendelések) külön
        // kapcsolótáblákban tároljuk — ez a relációs adatbázis normál formája.
        await ExecuteNonQueryAsync(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS Couriers (
                Id                   INTEGER PRIMARY KEY,
                Name                 TEXT    NOT NULL,
                CurrentLocationX     REAL    NOT NULL DEFAULT 0.0,
                CurrentLocationY     REAL    NOT NULL DEFAULT 0.0,
                Status               TEXT    NOT NULL DEFAULT 'Available',
                MaxCapacity          INTEGER NOT NULL DEFAULT 3
            );
            """);

        // ---- CourierZones kapcsolótábla ----
        // Egy futárhoz több zóna tartozhat (AssignedZoneIds).
        await ExecuteNonQueryAsync(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS CourierZones (
                CourierId  INTEGER NOT NULL REFERENCES Couriers(Id),
                ZoneId     INTEGER NOT NULL,
                PRIMARY KEY (CourierId, ZoneId)
            );
            """);

        // ---- DeliveryOrders tábla ----
        await ExecuteNonQueryAsync(connection, cancellationToken, """
            CREATE TABLE IF NOT EXISTS DeliveryOrders (
                Id                      INTEGER PRIMARY KEY,
                OrderNumber             TEXT    NOT NULL,
                CustomerName            TEXT    NOT NULL,
                AddressText             TEXT    NOT NULL,
                AddressLocationX        REAL    NOT NULL DEFAULT 0.0,
                AddressLocationY        REAL    NOT NULL DEFAULT 0.0,
                ZoneId                  INTEGER NOT NULL,
                Status                  TEXT    NOT NULL DEFAULT 'Pending',
                CreatedAt               TEXT    NOT NULL,
                ExpectedDeliveryTime    TEXT    NOT NULL,
                DeliveredAt             TEXT,
                AssignedCourierId       INTEGER
            );
            """);
    }

    // ====================================================
    // SEED — FUTÁROK
    // ====================================================

    private async Task SeedCouriersAsync(
        SqliteConnection connection,
        string jsonPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(jsonPath))
        {
            _logger.LogWarning("Futár JSON nem található: {Path}", jsonPath);
            return;
        }

        string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);

        // Ugyanolyan deszerializálás mint a régi CourierLoader-ben
        var couriers = JsonSerializer.Deserialize<List<Courier>>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            }) ?? new List<Courier>();

        _logger.LogInformation(
            "{Count} futár beillesztése az adatbázisba...", couriers.Count);

        foreach (var courier in couriers)
        {
            // Futár alaptulajdonságai
            await ExecuteNonQueryAsync(connection, cancellationToken, """
                INSERT OR REPLACE INTO Couriers
                    (Id, Name, CurrentLocationX, CurrentLocationY, Status, MaxCapacity)
                VALUES
                    (@Id, @Name, @X, @Y, @Status, @MaxCapacity);
                """,
                ("@Id",          courier.Id),
                ("@Name",        courier.Name),
                ("@X",           courier.CurrentLocation.X),
                ("@Y",           courier.CurrentLocation.Y),
                ("@Status",      courier.Status.ToString()),
                ("@MaxCapacity", courier.MaxCapacity));

            // Zóna kapcsolatok (N:M)
            foreach (var zoneId in courier.AssignedZoneIds)
            {
                await ExecuteNonQueryAsync(connection, cancellationToken, """
                    INSERT OR IGNORE INTO CourierZones (CourierId, ZoneId)
                    VALUES (@CourierId, @ZoneId);
                    """,
                    ("@CourierId", courier.Id),
                    ("@ZoneId",    zoneId));
            }
        }

        _logger.LogInformation("✅ {Count} futár sikeresen beillesztve.", couriers.Count);
    }

    // ====================================================
    // SEED — RENDELÉSEK
    // ====================================================

    private async Task SeedOrdersAsync(
        SqliteConnection connection,
        string jsonPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(jsonPath))
        {
            _logger.LogWarning("Rendelés JSON nem található: {Path}", jsonPath);
            return;
        }

        string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);

        var orders = JsonSerializer.Deserialize<List<DeliveryOrder>>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            }) ?? new List<DeliveryOrder>();

        _logger.LogInformation(
            "{Count} rendelés beillesztése az adatbázisba...", orders.Count);

        foreach (var order in orders)
        {
            await ExecuteNonQueryAsync(connection, cancellationToken, """
                INSERT OR REPLACE INTO DeliveryOrders
                    (Id, OrderNumber, CustomerName, AddressText,
                     AddressLocationX, AddressLocationY,
                     ZoneId, Status, CreatedAt, ExpectedDeliveryTime,
                     DeliveredAt, AssignedCourierId)
                VALUES
                    (@Id, @OrderNumber, @CustomerName, @AddressText,
                     @X, @Y,
                     @ZoneId, @Status, @CreatedAt, @ExpectedDeliveryTime,
                     @DeliveredAt, @AssignedCourierId);
                """,
                ("@Id",                   order.Id),
                ("@OrderNumber",          order.OrderNumber),
                ("@CustomerName",         order.CustomerName),
                ("@AddressText",          order.AddressText),
                ("@X",                    order.AddressLocation.X),
                ("@Y",                    order.AddressLocation.Y),
                ("@ZoneId",               order.ZoneId),
                ("@Status",               order.Status.ToString()),
                ("@CreatedAt",            order.CreatedAt.ToString("O")),
                ("@ExpectedDeliveryTime", order.ExpectedDeliveryTime.ToString("O")),
                ("@DeliveredAt",          (object?)order.DeliveredAt?.ToString("O") ?? DBNull.Value),
                ("@AssignedCourierId",    (object?)order.AssignedCourierId ?? DBNull.Value));
        }

        _logger.LogInformation("✅ {Count} rendelés sikeresen beillesztve.", orders.Count);
    }

    // ====================================================
    // HELPER METÓDUSOK
    // ====================================================

    /// <summary>
    /// Visszaadja a connection string-et az adatbázis elérési útja alapján.
    /// </summary>
    public string GetConnectionString() =>
        new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode       = SqliteOpenMode.ReadWriteCreate
        }.ToString();

    /// <summary>
    /// Megnyit egy új SQLite kapcsolatot.
    /// </summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(GetConnectionString());
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Paraméteres SQL utasítás futtatása visszatérési érték nélkül.
    /// Params tömb: (paramNév, érték) párok.
    /// </summary>
    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}