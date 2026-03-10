namespace package_delivery_simulator_console_app.Presentation;

using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Graph;

/// <summary>
/// A SetupPresenter által betöltött adatok csomagolója.
///
/// MIÉRT RECORD?
///   Az adatbetöltés eredménye nem változik — immutable.
///   A record szintaxis tömörebb, mint egy class pozicionális konstruktorral.
///
/// TARTALOM:
///   CityGraph        — a városgráf (Dijkstra + forgalom)
///   WarehouseService — már inicializált raktárszolgáltatás
///   Couriers         — betöltött futárok listája
///   Orders           — betöltött rendelések listája
/// </summary>
public record SetupResult(
    ICityGraph CityGraph,
    IWarehouseService WarehouseService,
    List<Courier> Couriers,
    List<DeliveryOrder> Orders);
