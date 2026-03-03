using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Interfaces;
using package_delivery_simulator.Infrastructure.Configuration;
using package_delivery_simulator.Infrastructure.Graph;
using package_delivery_simulator.Presentation;
using package_delivery_simulator.Presentation.Console.Views;
using package_delivery_simulator.Presentation.Console.ViewsInterfaces;
using package_delivery_simulator.Services.Delivery;
using package_delivery_simulator.Services.Notification;
using package_delivery_simulator.Services.Routing;

// ═══════════════════════════════════════════════════════════════════════
// PROGRAM.CS - AZ ALKALMAZÁS BELÉPÉSI PONTJA
// ═══════════════════════════════════════════════════════════════════════
//
// Ez a fájl az alkalmazás ENTRY POINT-ja (belépési pont).
// A .NET runtime először ezt a fájlt futtatja.
//
// FŐ FELELŐSSÉGEK:
// 1. UTF-8 encoding beállítása (magyar ékezetek miatt)
// 2. Generic Host létrehozása (DI konténer)
// 3. Szolgáltatások regisztrálása (Dependency Injection)
// 4. Logging konfiguráció
// 5. CancellationToken kezelés (CTRL+C)
// 6. Application futtatása
// 7. Hibakezelés
//
// MIT NEM CSINÁL:
// - Üzleti logika (azt a Services réteg végzi)
// - UI megjelenítés (azt a Presentation réteg végzi)
// - Adatkezelés (azt az Infrastructure réteg végzi)
//
// ═══════════════════════════════════════════════════════════════════════

// ───────────────────────────────────────────────────────────────────────
// 1. UTF-8 ENCODING BEÁLLÍTÁSA
// ───────────────────────────────────────────────────────────────────────
// A konzol alapértelmezetten nem támogatja a magyar ékezeteket.
// Ez a sor beállítja, hogy a Console.WriteLine helyesen jelenítse meg
// az "á", "é", "ő", "ű" karaktereket.
System.Console.OutputEncoding = System.Text.Encoding.UTF8;

// ───────────────────────────────────────────────────────────────────────
// 2. GENERIC HOST LÉTREHOZÁSA
// ───────────────────────────────────────────────────────────────────────
// A Generic Host a modern .NET alkalmazások alapja.
//
// MIT CSINÁL A HOST?
// - Dependency Injection konténer kezelése
// - Alkalmazás életciklus kezelése (startup, shutdown)
// - Konfiguráció betöltése (appsettings.json)
// - Logging szolgáltatás biztosítása
//
// Host.CreateDefaultBuilder(args) automatikusan:
// - Betölti az appsettings.json fájlt
// - Beállítja a default logging-ot
// - Kezeli a command-line argumentumokat
var host = Host.CreateDefaultBuilder(args)

    // ───────────────────────────────────────────────────────────────────
    // 3. SZOLGÁLTATÁSOK REGISZTRÁLÁSA (DEPENDENCY INJECTION)
    // ───────────────────────────────────────────────────────────────────
    // Ez a rész a "DI Container"-t konfigurálja.
    // Minden interface-implementáció párost itt regisztrálunk.
    //
    // MIÉRT FONTOS EZ?
    // - Amikor egy osztály konstruktorában IService-t kér, a DI
    //   automatikusan beinjektálja a megfelelő implementációt.
    // - Nem kell manuálisan `new`-ozni semmit!
    // - Könnyű tesztelni (mock objektumokkal)
    .ConfigureServices((context, services) =>
    {
        // ═══════════════════════════════════════════════════════════════
        // OPTIONS PATTERN - KONFIGURÁCIÓ
        // ═══════════════════════════════════════════════════════════════
        // Az appsettings.json "AppSettings" szekciója → AppSettings osztály
        //
        // appsettings.json:
        // {
        //   "AppSettings": {
        //     "TickDurationMs": 500,
        //     "MaxCouriers": 10
        //   }
        // }
        //
        // Ezután bármelyik osztály konstruktorában:
        // public MyClass(IOptions<AppSettings> options)
        // {
        //     var tickMs = options.Value.TickDurationMs; // Típusbiztos!
        // }
        services.AddOptions<AppSettings>()
                .Bind(context.Configuration.GetSection("AppSettings"));

        // ═══════════════════════════════════════════════════════════════
        // SERVICES RÉTEG - ÜZLETI LOGIKA
        // ═══════════════════════════════════════════════════════════════
        services.AddSingleton<CityGraph>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<CityGraph>>();
            // Itt hozd létre / töltsd be a gráfot.
            // Ha már van CityGraphFactory vagy hasonló, azt használd.
            // Ideiglenesen lehet egyszerűbb valami:
            var graph = new CityGraph();
            // TODO: töltsd fel JSON-ből, ha már kész
            return graph;
        });
        // Útvonal optimalizálás (Greedy: legközelebbi futár)
        // Interface: IRouteOptimizationService
        // Implementáció: GreedyRouteOptimizationService
        // Lifetime: Singleton (1 példány az egész alkalmazásra)
        //
        // SINGLETON = Egyszer létrehozva, mindenki ugyanazt a példányt kapja
        services.AddSingleton<IRouteOptimizationService, GreedyRouteOptimizationService>();

        // Értesítési szolgáltatás (késések esetén)
        // Interface: INotificationService
        // Implementáció: ConsoleNotificationService
        // Lifetime: Singleton
        services.AddSingleton<INotificationService, ConsoleNotificationService>();

        // Kézbesítési szolgáltatás (fő szimuláció logika, TPL párhuzamosítás)
        // Interface: IDeliveryService
        // Implementáció: DeliveryService
        // Lifetime: Singleton
        //
        // Ez a szolgáltatás:
        // - Kezeli a futárokat és rendeléseket
        // - Task.Run()-nal párhuzamosan futtatja a futárokat
        // - Greedy algoritmust használ a hozzárendeléshez
        services.AddSingleton<IDeliveryService, DeliveryService>();

        // ═══════════════════════════════════════════════════════════════
        // PRESENTATION RÉTEG - USER INTERFACE
        // ═══════════════════════════════════════════════════════════════

        // Főmenü nézet (1. Szimuláció indítása, 2. Kilépés)
        // Interface: IMainMenuView
        // Implementáció: MainMenuView
        services.AddSingleton<IMainMenuView, MainMenuView>();

        // Szimuláció nézet (élő állapot kiírása: futárok, rendelések)
        // Interface: ISimulationView
        // Implementáció: SimulationView
        services.AddSingleton<ISimulationView, SimulationView>();

        // Riport nézet (végső statisztikák: kézbesítések, késések, futárok)
        // Interface: IReportView
        // Implementáció: ReportView
        services.AddSingleton<IReportView, ReportView>();

        // ═══════════════════════════════════════════════════════════════
        // APPLICATION OSZTÁLY - COMPOSITION ROOT
        // ═══════════════════════════════════════════════════════════════
        // Ez az osztály koordinálja az egész alkalmazást.
        // Ő hívja meg a View-kat, Service-ket a megfelelő sorrendben.
        //
        // FONTOS: Ez NEM interface-szel van regisztrálva, mert
        // ez a legfelső szintű osztály, amit közvetlenül használunk.
        services.AddSingleton<Application>();
    })

    // ───────────────────────────────────────────────────────────────────
    // 4. LOGGING KONFIGURÁCIÓ
    // ───────────────────────────────────────────────────────────────────
    // A .NET beépített Logging rendszerét konfiguráljuk.
    //
    // MIÉRT NE Console.WriteLine?
    // - A Logger strukturált (JSON formátum támogatás)
    // - Szinteket támogat (Debug, Info, Warning, Error, Critical)
    // - Később egyszerűen cserélhető (fájlba, adatbázisba, stb.)
    .ConfigureLogging(logging =>
    {
        // Töröljük az összes default provider-t
        // (pl. EventLog, Debug ablak, stb.)
        logging.ClearProviders();

        // Csak Console logging-ot engedélyezünk (egyszerű, debug-olható)
        logging.AddConsole();

        // Log szint beállítása: Information
        // - Trace: Nagyon részletes (minden apróság)
        // - Debug: Részletes (fejlesztéshez)
        // - Information: Fontos események (ALAPÉRTELMEZETT)
        // - Warning: Figyelmeztetések
        // - Error: Hibák
        // - Critical: Kritikus hibák
        logging.SetMinimumLevel(LogLevel.Information);
    })

    // Host objektum építése (konfigurációk élesítése)
    .Build();

// ═══════════════════════════════════════════════════════════════════════
// 5. CANCELLATION TOKEN SETUP (CTRL+C KEZELÉS)
// ═══════════════════════════════════════════════════════════════════════
// A CancellationToken lehetővé teszi a biztonságos leállítást.
//
// MŰKÖDÉS:
// 1. Felhasználó megnyomja CTRL+C-t
// 2. CancelKeyPress event lefut
// 3. cts.Cancel() meghívása
// 4. Minden async Task megkapja a cancellationToken.IsCancellationRequested = true jelet
// 5. Task-ok befejeződnek (cleanup fut)
// 6. Alkalmazás leáll
//
// Ez a "Graceful Shutdown" (kecses leállás) - nem "instant kill".
var cts = new CancellationTokenSource();

// CTRL+C esemény figyelése
System.Console.CancelKeyPress += (sender, eventArgs) =>
{
    // e.Cancel = true → NE lépjen ki azonnal az alkalmazás
    // Adjunk időt a cleanup-ra!
    eventArgs.Cancel = true;

    // Felhasználói visszajelzés
    System.Console.WriteLine("\n⏹️  Leállítás folyamatban...");

    // Jelet küldünk minden Task-nak, hogy fejezzék be a munkát
    cts.Cancel();
};

// ═══════════════════════════════════════════════════════════════════════
// 6. APPLICATION FUTTATÁSA
// ═══════════════════════════════════════════════════════════════════════
// Itt történik a fő végrehajtás.
//
// LÉPÉSEK:
// 1. Scope létrehozása (DI konténer scope-olása)
// 2. Application példány lekérése a DI-ből
// 3. Application.RunAsync() meghívása
// 4. Várakozás a befejezésre
// 5. Hibakezelés
// 6. Exit code visszaadása (0 = siker, 1 = hiba)

try
{
    // ───────────────────────────────────────────────────────────────────
    // SCOPE LÉTREHOZÁSA
    // ───────────────────────────────────────────────────────────────────
    // A Scope egy DI konténer "élettartam buborék".
    // Scoped szolgáltatások (ha lennének) itt jönnek létre és itt semmisülnek meg.
    //
    // Console app-nál nincs sok jelentősége (mert nincs kérés/response ciklus),
    // de Web API-knál fontos (egy kérés = egy scope).
    using var scope = host.Services.CreateScope();

    // ───────────────────────────────────────────────────────────────────
    // APPLICATION LEKÉRÉSE A DI KONTÉNERBŐL
    // ───────────────────────────────────────────────────────────────────
    // GetRequiredService<Application>():
    // - Megkeresi a DI konténerben az Application osztályt
    // - Létrehozza, HA még nem létezik (Singleton esetén 1x)
    // - Automatikusan beinjektálja az ÖSSZES függőségét:
    //   * IDeliveryService → DeliveryService példány
    //   * IMainMenuView → MainMenuView példány
    //   * ISimulationView → SimulationView példány
    //   * IReportView → ReportView példány
    //   * ILogger<Application> → Logger példány
    var app = scope.ServiceProvider.GetRequiredService<Application>();

    // ───────────────────────────────────────────────────────────────────
    // FŐ ALKALMAZÁS LOGIKA FUTTATÁSA
    // ───────────────────────────────────────────────────────────────────
    // Az Application.RunAsync() metódus most átveszi az irányítást.
    // Ez a metódus:
    // 1. Megjeleníti a főmenüt (MainMenuView)
    // 2. Betölti az adatokat (futárok, rendelések)
    // 3. Elindítja a szimulációt (DeliveryService)
    // 4. Frissíti a UI-t (SimulationView) 500ms-enként
    // 5. Megjeleníti a végső riportot (ReportView)
    //
    // await = várunk, amíg befejeződik (aszinkron végrehajtás)
    // cancellationToken = CTRL+C kezeléshez
    await app.RunAsync(cts.Token);

    // ───────────────────────────────────────────────────────────────────
    // SIKERES BEFEJEZÉS
    // ───────────────────────────────────────────────────────────────────
    // Ha idáig eljutottunk hiba nélkül, 0-val térünk vissza.
    // Exit code 0 = SUCCESS (minden rendben)
    return 0;
}
catch (Exception ex)
{
    // ───────────────────────────────────────────────────────────────────
    // GLOBÁLIS HIBAKEZELÉS
    // ───────────────────────────────────────────────────────────────────
    // Ha bármilyen nem kezelt exception történik, ide jutunk.
    //
    // PÉLDÁK:
    // - NullReferenceException (null objektum elérés)
    // - InvalidOperationException (érvénytelen művelet)
    // - IOException (fájl hiba)
    // - stb.

    // Piros szín a hiba jelzésére
    System.Console.ForegroundColor = ConsoleColor.Red;

    // Hibaüzenet kiírása
    System.Console.WriteLine($"\n❌ HIBA: {ex.Message}");

    // Stack Trace kiírása (fejlesztéshez hasznos, hol történt a hiba)
    System.Console.WriteLine($"Stack Trace: {ex.StackTrace}");

    // Szín visszaállítása
    System.Console.ResetColor();

    // Exit code 1 = ERROR (hiba történt)
    // Ez jelzi az operációs rendszernek, hogy valami nem sikerült.
    return 1;
}

// ═══════════════════════════════════════════════════════════════════════
// VÉGREHAJTÁSI FOLYAMAT ÖSSZEFOGLALÁS
// ═══════════════════════════════════════════════════════════════════════
//
// 1. Program.cs elindul (main entry point)
// 2. UTF-8 encoding beállítása
// 3. Generic Host létrehozása
// 4. Szolgáltatások regisztrálása DI konténerben
// 5. Logging konfiguráció
// 6. CancellationToken setup (CTRL+C)
// 7. Application lekérése DI-ből (automatikus dependency injection!)
// 8. Application.RunAsync() meghívása
//    ├─ MainMenuView.ShowMenu()           → Főmenü
//    ├─ LoadInitialData()                 → Adatok betöltése
//    ├─ DeliveryService.RunSimulationAsync() → Szimuláció (TPL)
//    ├─ SimulationView.UpdateDisplay()    → UI frissítés (500ms)
//    └─ ReportView.ShowFinalReport()      → Végső statisztika
// 9. Alkalmazás befejeződik
// 10. Exit code visszaadása (0 vagy 1)
//
// ═══════════════════════════════════════════════════════════════════════
