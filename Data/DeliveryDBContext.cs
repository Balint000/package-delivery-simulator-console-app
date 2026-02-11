using Microsoft.EntityFrameworkCore;
using PackageDelivery.Models;

namespace PackageDelivery.Data;

/// <summary>
/// Az adatbázis kontextus osztály, amely a csomagkézbesítési rendszer összes entitását kezeli.
/// Entity Framework Core segítségével kapcsolódik SQLite adatbázishoz.
/// </summary>
public class DeliveryDBContext : DbContext
{
    // DbSet-ek: ezek reprezentálják az adatbázis tábláit
    public DbSet<DeliveryOrder> DeliveryOrders { get; set; } = null!;
    public DbSet<Courier> Couriers { get; set; } = null!;
    public DbSet<Zone> Zones { get; set; } = null!;
    public DbSet<RoutePlan> RoutePlans { get; set; } = null!;
    public DbSet<StatusHistory> StatusHistories { get; set; } = null!;

    /// <summary>
    /// Paramétermentes konstruktor - EF Tools design-time használatra.
    /// </summary>
    public DeliveryDBContext()
    {
    }

    /// <summary>
    /// Konstruktor, amely paraméterként kapja az adatbázis beállításokat.
    /// </summary>
    public DeliveryDBContext(DbContextOptions<DeliveryDBContext> options) : base(options)
    {
    }

    /// <summary>
    /// Ha nincs konstruktorból kapott konfiguráció, akkor SQLite-ot használ alapértelmezetten.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // SQLite adatbázis fájl neve: delivery.db
            optionsBuilder.UseSqlite("Data Source=delivery.db");
        }
    }

    /// <summary>
    /// Fluent API használata a modellek közötti kapcsolatok, indexek és megszorítások definiálásához.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DeliveryOrder konfiguráció
        modelBuilder.Entity<DeliveryOrder>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne<Zone>()
                .WithMany()
                .HasForeignKey(e => e.ZoneId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Courier>()
                .WithMany()
                .HasForeignKey(e => e.AssignedCourierId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Deadline);
        });

        // Courier konfiguráció
        modelBuilder.Entity<Courier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IsAvailable);
        });

        // Zone konfiguráció
        modelBuilder.Entity<Zone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
        });

        // RoutePlan konfiguráció
        modelBuilder.Entity<RoutePlan>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne<Courier>()
                .WithMany()
                .HasForeignKey(e => e.CourierId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // StatusHistory konfiguráció
        modelBuilder.Entity<StatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne<DeliveryOrder>()
                .WithMany()
                .HasForeignKey(e => e.DeliveryOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Timestamp);
        });
    }
}
