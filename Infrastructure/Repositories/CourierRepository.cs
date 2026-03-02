using Microsoft.EntityFrameworkCore;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Infrastructure.Database;

namespace package_delivery_simulator.Infrastructure.Repositories;

/// <summary>
/// Courier Repository - futárok adatbázis műveletei.
/// Repository Pattern: elkülöníti az adatbázis logikát az üzleti logikától.
/// </summary>
public class CourierRepository
{
    private readonly DeliveryDbContext _context;

    public CourierRepository(DeliveryDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Összes futár lekérdezése.
    /// </summary>
    public async Task<List<Courier>> GetAllAsync()
    {
        return await _context.Couriers.ToListAsync();
    }

    /// <summary>
    /// Futár lekérdezése ID alapján.
    /// </summary>
    public async Task<Courier?> GetByIdAsync(int id)
    {
        return await _context.Couriers.FindAsync(id);
    }

    /// <summary>
    /// Elérhető futárok lekérdezése (Available státusz).
    /// </summary>
    public async Task<List<Courier>> GetAvailableCouriersAsync()
    {
        return await _context.Couriers
            .Where(c => c.Status == CourierStatus.Available)
            .ToListAsync();
    }

    /// <summary>
    /// Futárok lekérdezése zóna alapján.
    /// </summary>
    public async Task<List<Courier>> GetCouriersByZoneAsync(int zoneId)
    {
        return await _context.Couriers
            .Where(c => c.AssignedZoneIds.Contains(zoneId))
            .ToListAsync();
    }

    /// <summary>
    /// Új futár hozzáadása.
    /// </summary>
    public async Task<Courier> AddAsync(Courier courier)
    {
        _context.Couriers.Add(courier);
        await _context.SaveChangesAsync();
        return courier;
    }

    /// <summary>
    /// Futár frissítése.
    /// </summary>
    public async Task UpdateAsync(Courier courier)
    {
        _context.Couriers.Update(courier);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Futár törlése.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var courier = await GetByIdAsync(id);
        if (courier != null)
        {
            _context.Couriers.Remove(courier);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Futár státusz frissítése.
    /// </summary>
    public async Task UpdateStatusAsync(int courierId, CourierStatus newStatus)
    {
        var courier = await GetByIdAsync(courierId);
        if (courier != null)
        {
            courier.Status = newStatus;
            await _context.SaveChangesAsync();
        }
    }
}
