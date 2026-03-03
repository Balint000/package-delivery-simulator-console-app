using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Presentation.Console;
using package_delivery_simulator.Services.Delivery;
using package_delivery_simulator.Services.Interfaces;
using package_delivery_simulator.Services.Notification;
using package_delivery_simulator.Services.Routing;
using package_delivery_simulator.Services.Simulation;

// UTF-8 encoding a magyar karakterekhez (ékezetek)
System.Console.OutputEncoding = System.Text.Encoding.UTF8;

// ╔════════════════════════════════════════════════════════════════════════╗
// ║                      .NET GENERIC HOST SETUP                            ║
// ║                                                                          ║
// ║  Ez a modern .NET alkalmazások életciklus-kezelése.                     ║
// ║  Funkcióik:                                                              ║
// ║  - Dependency Injection (szolgáltatások regisztrálása)                  ║
// ║  - Structured Logging (ILogger)                                          ║
// ║  - Configuration (appsettings.json)                                      ║
// ║  - Graceful Shutdown (CTRL+C kezelés beépítve)                          ║
// ║                                                                          ║
// ║  A Program.cs mostantól csak egy VÉKONY ENTRY POINT!                    ║
// ║  Az üzleti logika a SimulationRunner szolgáltatásban van.               ║
// ╚════════════════════════════════════════════════════════════════════════╝


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // ===== SZOLGÁLTATÁSOK REGISZTRÁLÁSA (DEPENDENCY INJECTION) =====

        // Singletonok: egész alkalmazás életciklusa alatt egy példány
        // - CityGraph: városi gráf modell (később töltsd be JSON-ből!)
        services.AddSingleton<object>(sp => new object()); // PLACEHOLDER - cseréld ki CityGraph-ra!

        // Scoped szolgáltatások: kérésenkénti/futásonként új példány
        // (Console appnál gyakorlatilag Singleton viselkedésű lesz)

        // Routing szolgáltatás: GREEDY (nearest neighbor) algoritmus
        services.AddScoped<IRouteOptimizationService, GreedyRouteOptimizationService>();

        // Notification szolgáltatás: késés értesítés
        services.AddScoped<INotificationService, ConsoleNotificationService>();

        // Delivery szolgáltatás: fő szimuláció logika (TPL)
        services.AddScoped<IDeliveryService, DeliveryService>();

        // Live Console UI: élő státusz megjelenítés
        services.AddScoped<ILiveConsoleUI, LiveConsoleUI>();

        // Szimuláció futtató: koordinálja az egész folyamatot
        services.AddScoped<SimulationRunner>();

        // TODO KÉSŐBB: EF Core DbContext regisztrálása
        // services.AddDbContext<DeliveryDbContext>(options =>
        //     options.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection")));
    })
    .ConfigureLogging(logging =>
    {
        // ===== LOGGING KONFIGURÁCIÓ =====

        // Alapértelmezett provider-ek törlése (FileLogger, EventLog, stb.)
        logging.ClearProviders();

        // Csak Console logging (egyszerű, debug-olható)
        logging.AddConsole();

        // Log szint: Information (Debug, Information, Warning, Error, Critical)
        // Debug részletesebb, Information: csak fontos dolgok
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

// ╔════════════════════════════════════════════════════════════════════════╗
// ║                      CANCELLATION TOKEN SETUP                            ║
// ║                                                                          ║
// ║  CTRL+C kezelés: ha a felhasználó megnyomja, a CancellationToken        ║
// ║  jelzést küld az összes futó Task-nak, hogy fejezzék be a munkát.       ║
// ║                                                                          ║
// ║  Ez a .NET ajánlott módja a graceful shutdown-ra.                        ║
// ╚════════════════════════════════════════════════════════════════════════╝

var cancellationTokenSource = new CancellationTokenSource();

// CTRL+C esemény figyelése
System.Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true; // Nem lép ki azonnal, hanem várunk a cleanup-ra
    System.Console.WriteLine("\n\n⏹️  Leállítás folyamatban...");
    cancellationTokenSource.Cancel();
};

// ╔════════════════════════════════════════════════════════════════════════╗
// ║                      SZIMULÁCIÓ INDÍTÁSA                                 ║
// ║                                                                          ║
// ║  A SimulationRunner szolgáltatást a DI container-ből kérjük ki.         ║
// ║  Ez automatikusan beinjektálja az összes függőségét!                    ║
// ╚════════════════════════════════════════════════════════════════════════╝

try
{
    // Scope létrehozása (scoped szolgáltatásokhoz)
    using var scope = host.Services.CreateScope();

    // SimulationRunner lekérése DI-ből (ez fog mindent koordinálni)
    var simulationRunner = scope.ServiceProvider.GetRequiredService<SimulationRunner>();

    // Szimuláció futtatása (main logika)
    await simulationRunner.RunAsync(cancellationTokenSource.Token);
}
catch (Exception ex)
{
    // Globális exception handling
    System.Console.ForegroundColor = ConsoleColor.Red;
    System.Console.WriteLine($"\n❌ HIBA: {ex.Message}");
    System.Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    System.Console.ResetColor();
    return 1; // Exit code 1 = hiba
}

// ╔════════════════════════════════════════════════════════════════════════╗
// ║                      SIKERES BEFEJEZÉS                                   ║
// ╚════════════════════════════════════════════════════════════════════════╝

return 0; // Exit code 0 = siker
