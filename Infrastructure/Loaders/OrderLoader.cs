// ============================================================
// OrderLoader.cs — SQLite verzió
// ============================================================
// Felelőssége: Rendelések (DeliveryOrder) betöltése SQLite-ból.
//
// VÁLTOZÁSOK a JSON verzióhoz képest:
//   - File.ReadAllTextAsync + JsonSerializer helyett
//     SqliteConnection + SqliteDataReader.
//   - NULL értékek kezelése: DeliveredAt és AssignedCourierId
//     az adatbázisban NULL lehet → IsDBNull ellenőrzés szükséges.
//   - A külső szignatúra (LoadAsync) VÁLTOZATLAN.
// ============================================================

namespace package_delivery_simulator_console_app.Infrastructure.Loaders;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator_console_app.Infrastructure.Database;

/// <summary>
/// Rendelések betöltése SQLite adatbázisból.
/// </summary>
public class OrderLoader
{
    // ====================================================
    // FÜGGŐSÉGEK
    // ====================================================

    private readonly ILogger<OrderLoader> _logger;
    private readonly DatabaseInitializer _dbInitializer;

    // ====================================================
    // KONSTRUKTOR
    // ====================================================

    /// <summary>
    /// OrderLoader létrehozása.
    /// </summary>
    /// <param name="logger">Logger (DI-ból)</param>
    /// <param name="dbInitializer">Adatbázis inicializáló</param>
    public OrderLoader(
        ILogger<OrderLoader> logger,
        DatabaseInitializer dbInitializer)
    {
        _logger = logger;
        _dbInitializer = dbInitializer;
    }

    // ====================================================
    // BETÖLTÉS
    // ====================================================

    /// <summary>
    /// Rendelések betöltése az SQLite adatbázisból.
    /// </summary>
    /// <param name="cancellationToken">Megszakítási jel</param>
    /// <returns>Rendelések listája</returns>
    public Task<List<DeliveryOrder>> LoadAsync(
        CancellationToken cancellationToken = default)
        => LoadFromDatabaseAsync(cancellationToken);

    /// <summary>
    /// Rendelések betöltése SQLite-ból.
    ///
    /// NULLABLE MEZŐK KEZELÉSE:
    ///   - DeliveredAt      → null amíg nincs kézbesítve
    ///   - AssignedCourierId → null amíg nincs futár hozzárendelve
    ///   Ezeket IsDBNull() ellenőrzéssel kezeljük.
    ///
    /// DÁTUM FORMÁTUM:
    ///   Az adatbázisban ISO 8601 szövegként tároljuk ("O" formátum),
    ///   amit DateTime.Parse() visszaalakít.
    /// </summary>
    public async Task<List<DeliveryOrder>> LoadFromDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rendelések betöltése SQLite adatbázisból...");

        await using var connection = _dbInitializer.OpenConnection();

        var orders = new List<DeliveryOrder>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, OrderNumber, CustomerName, AddressText,
                   AddressLocationX, AddressLocationY,
                   ZoneId, Status, CreatedAt, ExpectedDeliveryTime,
                   DeliveredAt, AssignedCourierId
            FROM   DeliveryOrders
            ORDER  BY Id;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            // Status szövegből enum konverzió
            var statusText = reader.GetString(7);
            var status = Enum.Parse<OrderStatus>(statusText, ignoreCase: true);

            // NULL-képes mezők olvasása (IsDBNull ellenőrzéssel)
            DateTime? deliveredAt = reader.IsDBNull(10)
                ? null
                : DateTime.Parse(reader.GetString(10));

            int? assignedCourierId = reader.IsDBNull(11)
                ? null
                : reader.GetInt32(11);

            orders.Add(new DeliveryOrder
            {
                Id                   = reader.GetInt32(0),
                OrderNumber          = reader.GetString(1),
                CustomerName         = reader.GetString(2),
                AddressText          = reader.GetString(3),
                AddressLocation      = new Location(
                                           reader.GetDouble(4),
                                           reader.GetDouble(5)),
                ZoneId               = reader.GetInt32(6),
                Status               = status,
                CreatedAt            = DateTime.Parse(reader.GetString(8)),
                ExpectedDeliveryTime = DateTime.Parse(reader.GetString(9)),
                DeliveredAt          = deliveredAt,
                AssignedCourierId    = assignedCourierId
            });
        }

        _logger.LogInformation(
            "{Count} rendelés betöltve az adatbázisból.", orders.Count);

        return orders;
    }
}