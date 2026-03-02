using Microsoft.EntityFrameworkCore;

namespace package_delivery_simulator.Infrastructure.Database;

/// <summary>
/// Adatbázis inicializáló osztály.
/// Felelősség: DB létrehozás, migráció alkalmazás, seed adatok.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Adatbázis inicializálása.
    ///
    /// Lépések:
    /// 1. Ellenőrzi, hogy létezik-e az adatbázis
    /// 2. Ha nem, létrehozza (CreateDatabase)
    /// 3. Migráció alkalmazása (ha vannak)
    /// </summary>
    public static void Initialize(DeliveryDbContext context)
    {
        try
        {
            // Adatbázis létrehozása, ha nem létezik
            // EF Core automatikusan létrehozza a táblákat a DbSet-ek alapján
            context.Database.EnsureCreated();

            Console.WriteLine("✅ SQLite adatbázis inicializálva!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hiba az adatbázis inicializáláskor: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Adatbázis törlése és újra létrehozása.
    /// VIGYÁZAT: Ez törli az összes adatot!
    /// </summary>
    public static void RecreateDatabase(DeliveryDbContext context)
    {
        Console.WriteLine("⚠️  Adatbázis törlése és újra létrehozása...");

        // Törlés
        context.Database.EnsureDeleted();

        // Újra létrehozás
        context.Database.EnsureCreated();

        Console.WriteLine("✅ Adatbázis újra létrehozva!");
    }

    /// <summary>
    /// SEED ADATOK - tesztadatok generálása.
    /// Ezt használd fejlesztés közben!
    /// </summary>
    public static void SeedData(DeliveryDbContext context)
    {
        // Ellenőrizzük, van-e már adat
        if (context.Couriers.Any() || context.DeliveryOrders.Any())
        {
            Console.WriteLine("ℹ️  Adatbázis már tartalmaz adatokat, seed kihagyva.");
            return;
        }

        Console.WriteLine("🌱 Seed adatok generálása...");

        // TODO: Seed adatok hozzáadása
        // Példa:
        // context.Couriers.Add(new Courier { ... });
        // context.SaveChanges();

        Console.WriteLine("✅ Seed adatok hozzáadva!");
    }
}
