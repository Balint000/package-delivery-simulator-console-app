namespace package_delivery_simulator.Services.Simulation;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Entities;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator.Domain.ValueObjects;
using package_delivery_simulator.Presentation.Console;
using package_delivery_simulator.Domain.Interfaces;

/// <summary>
/// Szimuláció-futtató szolgáltatás.
/// Ez az osztály koordinálja a teljes szimulációt:
/// - Futárok és rendelések betöltése/generálása
/// - UI inicializálás
/// - Szimuláció és UI frissítés párhuzamos indítása
/// - Leállítás kezelése
///
/// Felelősség: A Program.cs logikáját veszi át, hogy az csak egy vékony entry point legyen.
/// </summary>
public class SimulationRunner
{
    private readonly IDeliveryService _deliveryService;
    private readonly ILiveConsoleUI _liveUI;
    private readonly ILogger<SimulationRunner> _logger;

    /// <summary>
    /// Konstruktor - DI-ből kapja a szolgáltatásokat.
    /// </summary>
    public SimulationRunner(
        IDeliveryService deliveryService,
        ILiveConsoleUI liveUI,
        ILogger<SimulationRunner> logger)
    {
        _deliveryService = deliveryService;
        _liveUI = liveUI;
        _logger = logger;
    }

    /// <summary>
    /// Szimuláció főmetódusa - ezt hívjuk a Program.cs-ből.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // UTF-8 encoding a magyar karakterekhez (ékezetek)
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;

        _logger.LogInformation("Csomagkézbesítés Szimuláció - TPL");
        _logger.LogInformation("============================================");

        // ===== 1. VÁROS/GRÁF BETÖLTÉSE =====
        // TODO: A te gráf betöltő kódod ide!
        // Pl: var cityGraph = CityGraphLoader.LoadFromJson("Data/city-graph.json");
        // ÁTMENETI: null objektum (később cseréld ki!)
        _logger.LogInformation("Város gráf betöltve!");

        // ===== 2. FUTÁROK HOZZÁADÁSA =====
        LoadCouriers();
        _logger.LogInformation("Futárok betöltve!");

        // ===== 3. RENDELÉSEK GENERÁLÁSA =====
        GenerateOrders(orderCount: 10);
        _logger.LogInformation("Rendelések generálva!");

        // ===== 4. INDÍTÁS (felhasználói input) =====
        System.Console.WriteLine("\nNyomj ENTER-t a szimuláció indításához...");
        System.Console.ReadLine();

        // ===== 5. UI INICIALIZÁLÁS =====
        _liveUI.Initialize();

        // ===== 6. SZIMULÁCIÓ ÉS UI FRISSÍTÉS PÁRHUZAMOSAN =====

        // Szimuláció Task (párhuzamos futárok)
        var simulationTask = _deliveryService.RunSimulationAsync(cancellationToken);

        // UI frissítő Task (500ms-enként frissít)
        var uiTask = RunUIUpdateLoopAsync(cancellationToken);

        // ===== 7. VÁRUNK A BEFEJEZÉSRE =====
        try
        {
            // Mindkét Task-ra várunk (szimuláció ÉS UI)
            await Task.WhenAll(simulationTask, uiTask);
        }
        catch (OperationCanceledException)
        {
            // Normális leállítás (CTRL+C)
            _logger.LogInformation("⏹️  Szimuláció leállítva");
        }

        // ===== 8. CLEANUP =====
        _liveUI.Cleanup();

        // ===== 9. VÉGSŐ STATISZTIKA =====
        DisplayFinalStatistics();
    }

    /// <summary>
    /// Futárok betöltése (jelenleg hardcoded, később JSON-ből vagy DB-ből).
    /// </summary>
    private void LoadCouriers()
    {
        _deliveryService.AddCourier(new Courier
        {
            Id = 1,
            Name = "Kovács Péter",
            CurrentNodeId = 0, // Raktár
            CurrentLocation = new Location(0, 0)
        });

        _deliveryService.AddCourier(new Courier
        {
            Id = 2,
            Name = "Nagy Anna",
            CurrentNodeId = 0,
            CurrentLocation = new Location(0, 0)
        });

        _deliveryService.AddCourier(new Courier
        {
            Id = 3,
            Name = "Szabó Gábor",
            CurrentNodeId = 0,
            CurrentLocation = new Location(0, 0)
        });
    }

    /// <summary>
    /// Rendelések generálása (random adatokkal).
    /// Később ez jöhet JSON fájlból vagy adatbázisból.
    /// </summary>
    private void GenerateOrders(int orderCount)
    {
        var random = new Random();

        for (int i = 1; i <= orderCount; i++)
        {
            _deliveryService.AddOrder(new DeliveryOrder
            {
                Id = i,
                OrderNumber = $"ORD-{i:D5}",
                CustomerName = $"Ügyfél #{i}",
                AddressText = $"Cím {i}",
                AddressLocation = new Location(random.Next(1, 100), random.Next(1, 100)),
                ZoneId = random.Next(1, 4),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.Now,
                ExpectedDeliveryTime = DateTime.Now.AddMinutes(15) // 15 perc a cél
            });
        }
    }

    /// <summary>
    /// UI frissítő ciklus (párhuzamosan fut a szimulációval).
    /// 500ms-enként frissíti a konzol UI-t.
    /// </summary>
    private async Task RunUIUpdateLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Adatok lekérése a service-ből
            var couriers = _deliveryService.GetCouriers();
            var orders = _deliveryService.GetOrders();
            var (totalDeliveries, totalDelays) = _deliveryService.GetStatistics();

            // Statisztikák objektum
            var stats = new SimulationStats
            {
                TotalDeliveries = totalDeliveries,
                TotalDelays = totalDelays
            };

            // UI frissítés (500ms-enként)
            _liveUI.Update(couriers, orders, stats);

            // Várakozás
            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Leállítás közben - kilépünk
                break;
            }
        }
    }

    /// <summary>
    /// Végső statisztikák kiírása (szimuláció után).
    /// </summary>
    private void DisplayFinalStatistics()
    {
        System.Console.Clear();
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════════════╗");
        System.Console.WriteLine("║                    ✅ SZIMULÁCIÓ BEFEJEZVE                            ║");
        System.Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝\n");
        System.Console.ResetColor();

        var (finalDeliveries, finalDelays) = _deliveryService.GetStatistics();

        System.Console.WriteLine("VÉGSŐ STATISZTIKÁK:");
        System.Console.WriteLine("───────────────────────────────────────────────────────────────────────");
        System.Console.WriteLine($"Összes kézbesítés:  {finalDeliveries}");
        System.Console.WriteLine($"Késések száma:      {finalDelays}");

        if (finalDeliveries > 0)
        {
            double delayRate = (double)finalDelays / finalDeliveries * 100;
            System.Console.WriteLine($"Késési arány:       {delayRate:F1}%");
        }

        System.Console.WriteLine("\nFUTÁR TELJESÍTMÉNYEK:");
        System.Console.WriteLine("───────────────────────────────────────────────────────────────────────");

        foreach (var courier in _deliveryService.GetCouriers().OrderByDescending(c => c.TotalDeliveries))
        {
            System.Console.WriteLine($"{courier.Name,-20}: {courier.TotalDeliveries,3} kézbesítés");
        }

        System.Console.WriteLine("\nNyomj meg egy billentyűt a kilépéshez...");
        System.Console.ReadKey();
    }
}
