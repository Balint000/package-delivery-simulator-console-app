using Microsoft.EntityFrameworkCore;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Infrastructure.Database;

namespace package_delivery_simulator.Infrastructure.Repositories;

/// <summary>
/// Order Repository - rendelések adatbázis műveletei.
/// </summary>
public class OrderRepository
{
    private readonly DeliveryDbContext _context;

    public OrderRepository(DeliveryDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Összes rendelés lekérdezése.
    /// </summary>
    public async Task<List<DeliveryOrder>> GetAllAsync()
    {
        return await _context.DeliveryOrders.ToListAsync();
    }

    /// <summary>
    /// Rendelés lekérdezése ID alapján.
    /// </summary>
    public async Task<DeliveryOrder?> GetByIdAsync(int id)
    {
        return await _context.DeliveryOrders.FindAsync(id);
    }

    /// <summary>
    /// Függőben lévő rendelések (Pending).
    /// </summary>
    public async Task<List<DeliveryOrder>> GetPendingOrdersAsync()
    {
        return await _context.DeliveryOrders
            .Where(o => o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt) // Legrégebbi először
            .ToListAsync();
    }

    /// <summary>
    /// Rendelések zóna alapján.
    /// </summary>
    public async Task<List<DeliveryOrder>> GetOrdersByZoneAsync(int zoneId)
    {
        return await _context.DeliveryOrders
            .Where(o => o.ZoneId == zoneId)
            .ToListAsync();
    }

    /// <summary>
    /// Késett rendelések (DeliveredAt > ExpectedDeliveryTime).
    /// </summary>
    public async Task<List<DeliveryOrder>> GetDelayedOrdersAsync()
    {
        return await _context.DeliveryOrders
            .Where(o => o.Status == OrderStatus.Delivered &&
                        o.DeliveredAt != null &&
                        o.DeliveredAt > o.ExpectedDeliveryTime)
            .ToListAsync();
    }

    /// <summary>
    /// Új rendelés hozzáadása.
    /// </summary>
    public async Task<DeliveryOrder> AddAsync(DeliveryOrder order)
    {
        _context.DeliveryOrders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    /// <summary>
    /// Rendelés frissítése.
    /// </summary>
    public async Task UpdateAsync(DeliveryOrder order)
    {
        _context.DeliveryOrders.Update(order);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Rendelés törlése.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        var order = await GetByIdAsync(id);
        if (order != null)
        {
            _context.DeliveryOrders.Remove(order);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Rendelés státusz frissítése.
    /// </summary>
    public async Task UpdateStatusAsync(int orderId, OrderStatus newStatus)
    {
        var order = await GetByIdAsync(orderId);
        if (order != null)
        {
            order.Status = newStatus;

            if (newStatus == OrderStatus.Delivered)
            {
                order.DeliveredAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Futár hozzárendelése rendeléshez.
    /// </summary>
    public async Task AssignCourierAsync(int orderId, int courierId)
    {
        var order = await GetByIdAsync(orderId);
        if (order != null)
        {
            order.AssignedCourierId = courierId;
            order.Status = OrderStatus.InTransit;
            await _context.SaveChangesAsync();
        }
    }
}
