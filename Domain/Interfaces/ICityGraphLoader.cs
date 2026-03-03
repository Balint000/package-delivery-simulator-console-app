namespace package_delivery_simulator_console_app.Domain.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using package_delivery_simulator.Infrastructure.Graph;
using package_delivery_simulator.Domain.Entities;

/// <summary>
/// Absztrakció a város gráf betöltésére (pl. JSON-ből).
/// Így könnyen cserélhető, és jól tesztelhető.
/// </summary>
public interface ICityGraphLoader
{
    /// <summary>
    /// Város gráf betöltése aszinkron módon.
    /// </summary>
    /// <param name="cancellationToken">Leállítás támogatása.</param>
    /// <returns>A felépített CityGraph példány.</returns>
    Task<CityGraph> LoadAsync(CancellationToken cancellationToken);
}
