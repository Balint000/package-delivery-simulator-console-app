using Microsoft.EntityFrameworkCore;
using package_delivery_simulator.Domain.Entities;

namespace package_delivery_simulator.Infrastructure.Database;

/// <summary>
/// Entity Framework Core DbContext osztály.
/// Ez az osztály kezeli az adatbázis kapcsolatot és a táblák definícióját.
///
/// DbSet<T> property-k = adatbázis táblák (T típusú entitások).
/// </summary>
public class DeliveryDbContext : DbContext
{
    /// <summary>
    /// Couriers tábla - futárok tárolása.
    /// </summary>
    public DbSet<Courier> Couriers { get; set; } = null!;

    /// <summary>
    /// DeliveryOrders tábla - rendelések tárolása.
    /// </summary>
    public DbSet<DeliveryOrder> DeliveryOrders { get; set; } = null!;

    // TODO: További táblák később
    // public DbSet<Zone> Zones { get; set; } = null!;
    // public DbSet<RoutePlan> RoutePlans { get; set; } = null!;
    // public DbSet<StatusHistory> StatusHistories { get; set; } = null!;

    /// <summary>
    /// Konstruktor - beállítja az adatbázis kapcsolati opciókat.
    /// </summary>
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// OnModelCreating - tábla konfigurációk, relációk definiálása.
    /// Ez fut le, amikor EF Core felépíti az adatbázis modellt.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== COURIER KONFIGURÁCIÓ =====
        modelBuilder.Entity<Courier>(entity =>
        {
            // Tábla neve
            entity.ToTable("Couriers");

            // Primary Key
            entity.HasKey(c => c.Id);

            // Auto-increment ID
            entity.Property(c => c.Id)
                .ValueGeneratedOnAdd();

            // Name kötelező, max 100 karakter
            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            // CurrentLocation - Owned Entity Pattern
            // Ez azt jelenti, hogy a Location értékobjektum
            // INLINE tárolódik a Couriers táblában (lat, lng oszlopok)
            entity.OwnsOne(c => c.CurrentLocation, location =>
            {
                location.Property(l => l.X)
                    .HasColumnName("CurrentLatitude")
                    .IsRequired();

                location.Property(l => l.Y)
                    .HasColumnName("CurrentLongitude")
                    .IsRequired();
            });

            // Status - enum -> int tárolás
            entity.Property(c => c.Status)
                .HasConversion<int>() // Enum -> int konverzió
                .IsRequired();

            // AssignedZoneIds - JSON tárolás (lista)
            entity.Property(c => c.AssignedZoneIds)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<int>()
                )
                .HasColumnType("TEXT");

            // AssignedOrderIds - JSON tárolás (lista)
            entity.Property(c => c.AssignedOrderIds)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<int>()
                )
                .HasColumnType("TEXT");

            // CurrentNodeId
            entity.Property(c => c.CurrentNodeId)
                .IsRequired()
                .HasDefaultValue(0);

            // TotalDeliveries
            entity.Property(c => c.TotalDeliveries)
                .IsRequired()
                .HasDefaultValue(0);

            // Index a gyorsabb kereséshez
            entity.HasIndex(c => c.Status);
        });

        // ===== DELIVERY ORDER KONFIGURÁCIÓ =====
        modelBuilder.Entity<DeliveryOrder>(entity =>
        {
            // Tábla neve
            entity.ToTable("DeliveryOrders");

            // Primary Key
            entity.HasKey(o => o.Id);

            // Auto-increment ID
            entity.Property(o => o.Id)
                .ValueGeneratedOnAdd();

            // OrderNumber - unique, kötelező
            entity.Property(o => o.OrderNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(o => o.OrderNumber)
                .IsUnique();

            // CustomerName
            entity.Property(o => o.CustomerName)
                .IsRequired()
                .HasMaxLength(100);

            // AddressText
            entity.Property(o => o.AddressText)
                .IsRequired()
                .HasMaxLength(500);

            // AddressLocation - Owned Entity Pattern
            entity.OwnsOne(o => o.AddressLocation, location =>
            {
                location.Property(l => l.X)
                    .HasColumnName("AddressLatitude")
                    .IsRequired();

                location.Property(l => l.Y)
                    .HasColumnName("AddressLongitude")
                    .IsRequired();
            });

            // ZoneId
            entity.Property(o => o.ZoneId)
                .IsRequired();

            // Status - enum -> int
            entity.Property(o => o.Status)
                .HasConversion<int>()
                .IsRequired();

            // Dátumok
            entity.Property(o => o.CreatedAt)
                .IsRequired();

            entity.Property(o => o.ExpectedDeliveryTime)
                .IsRequired();

            entity.Property(o => o.DeliveredAt)
                .IsRequired(false); // Nullable

            // AssignedCourierId - Foreign Key (opcionális)
            entity.Property(o => o.AssignedCourierId)
                .IsRequired(false); // Nullable

            // Indexek a gyorsabb lekérdezésekhez
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.ZoneId);
            entity.HasIndex(o => o.AssignedCourierId);
        });
    }
}
