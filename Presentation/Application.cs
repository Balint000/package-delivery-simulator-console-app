namespace package_delivery_simulator.Presentation;

using Microsoft.Extensions.Logging;
using package_delivery_simulator.Domain.Interfaces;
using package_delivery_simulator_console_app.Domain.Interfaces;
using package_delivery_simulator.Presentation.Console.ViewsInterfaces;
using package_delivery_simulator.Domain.Enums;


using package_delivery_simulator.Domain.Entities;

// ═══════════════════════════════════════════════════════════════════════
// APPLICATION.CS - FŐ ALKALMAZÁS KOORDINÁTOR
// ═══════════════════════════════════════════════════════════════════════
//
// Ez az osztály a "Composition Root" - az alkalmazás fő irányítója.
//
// FŐ FELELŐSSÉGEK:
// 1. Főmenü megjelenítése és választás kezelése
// 2. Adatok betöltése (futárok, rendelések)
// 3. Szimuláció indítása és monitorozása
// 4. UI frissítés koordinálása
// 5. Végső riport megjelenítése
//
// AMIT NEM CSINÁL:
// - Console.WriteLine (azt a View-k végzik)
// - Üzleti logika (azt a Services végzi)
// - Adatbázis hozzáférés (azt az Infrastructure végzi)
//
// Ez az osztály csak KOORDINÁL és HÍVOGAT más osztályokat.
// ═══════════════════════════════════════════════════════════════════════

public class Application
{
    // ───────────────────────────────────────────────────────────────────
    // DEPENDENCY INJECTION - PRIVÁT MEZŐK
    // ───────────────────────────────────────────────────────────────────
    // Ezek a függőségek a konstruktorban jönnek be automatikusan.
    // A DI konténer (Program.cs-ben konfigurálva) tölti be őket.

    // Üzleti logika szolgáltatás (futárok, rendelések, szimuláció)
    private readonly IDeliveryService _deliveryService;

    // Főmenü nézet (1. Indítás, 2. Kilépés)
    private readonly IMainMenuView _mainMenu;

    // Szimuláció nézet (élő állapot kiírása)
    private readonly ISimulationView _simulationView;

    // Riport nézet (végső statisztikák)
    private readonly IReportView _reportView;

    // Logger (strukturált naplózás)
    private readonly ILogger<Application> _logger;

    private readonly ICityGraphLoader _cityGraphLoader;
    private readonly ICourierLoader _courierLoader;
    private readonly IOrderLoader _orderLoader;

    // ───────────────────────────────────────────────────────────────────
    // KONSTRUKTOR - DEPENDENCY INJECTION
    // ───────────────────────────────────────────────────────────────────
    // A DI konténer automatikusan meghívja ezt a konstruktort és
    // beinjektálja az összes paramétert.
    //
    // PÉLDA:
    // Program.cs-ben regisztráltuk:
    //   services.AddSingleton<IDeliveryService, DeliveryService>();
    //   services.AddSingleton<IMainMenuView, MainMenuView>();
    //
    // Amikor az Application-t lekérjük:
    //   var app = scope.ServiceProvider.GetRequiredService<Application>();
    //
    // A DI automatikusan:
    // 1. Létrehozza a DeliveryService példányt
    // 2. Létrehozza a MainMenuView példányt
    // 3. Létrehozza a SimulationView példányt
    // 4. Létrehozza a ReportView példányt
    // 5. Létrehozza a Logger példányt
    // 6. Meghívja ezt a konstruktort ezekkel a példányokkal
    //
    // NINCS SZÜKSÉG `new`-ra! A DI mindent megold!
    public Application(
        ICityGraphLoader cityGraphLoader,
        ICourierLoader courierLoader,
        IOrderLoader orderLoader,
        IDeliveryService deliveryService,
        IMainMenuView mainMenu,
        ISimulationView simulationView,
        IReportView reportView,
        ILogger<Application> logger)
    {
        // Null ellenőrzés - biztonsági intézkedés
        // Ha valami nincs regisztrálva a DI-ben, itt elkapjuk
        _cityGraphLoader = cityGraphLoader;
        _courierLoader = courierLoader;
        _orderLoader = orderLoader;
        _deliveryService = deliveryService ?? throw new ArgumentNullException(nameof(deliveryService));
        _mainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));
        _simulationView = simulationView ?? throw new ArgumentNullException(nameof(simulationView));
        _reportView = reportView ?? throw new ArgumentNullException(nameof(reportView));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FŐ ALKALMAZÁS LOGIKA - VÉGREHAJTÁSI FOLYAMAT
    // ═══════════════════════════════════════════════════════════════════
    // Ez a metódus a Program.cs-ből hívódik meg:
    //   await app.RunAsync(cancellationToken);
    //
    // VÉGREHAJTÁSI LÉPÉSEK:
    // 1. Logolás: Alkalmazás indítása
    // 2. Főmenü megjelenítése → Felhasználó választ
    // 3. Ha kilépés → befejezés
    // 4. Ha indítás → adatok betöltése
    // 5. Szimuláció + UI frissítés párhuzamosan
    // 6. Végső riport megjelenítése
    //
    // ASYNC/AWAIT:
    // - async = ez a metódus aszinkron (nem blokkolja a szálat)
    // - await = várunk egy Task befejezésére (más Task-ok közben futhatnak)
    // - Task = aszinkron művelet (pl. késleltetés, I/O, stb.)
    //
    // CANCELLATION TOKEN:
    // - Ha a felhasználó CTRL+C-t nyom, a token jelzi a leállítást
    // - Minden async metódusnak továbbítjuk, hogy le tudjon állni
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // ───────────────────────────────────────────────────────────────
        // LÉPÉS 1: ALKALMAZÁS INDÍTÁS LOGOLÁSA
        // ───────────────────────────────────────────────────────────────
        // ILogger használat (strukturált naplózás)
        // LogLevel: Information (fontos esemény)
        _logger.LogInformation("Csomagkézbesítés Szimuláció indítása");

        // ───────────────────────────────────────────────────────────────
        // LÉPÉS 2: FŐMENÜ MEGJELENÍTÉSE
        // ───────────────────────────────────────────────────────────────
        // A MainMenuView.ShowMenu() metódust hívjuk.
        //
        // MIT CSINÁL A ShowMenu()?
        // 1. Console.Clear() - törli a képernyőt
        // 2. Kiírja a menüt:
        //    ╔═══════════════════════════════╗
        //    ║ Csomagkézbesítés Szimuláció   ║
        //    ╚═══════════════════════════════╝
        //    1. Szimuláció indítása
        //    2. Kilépés
        // 3. Console.ReadLine() - bekéri a választást
        // 4. Visszaadja MenuChoice.StartSimulation vagy MenuChoice.Exit
        var choice = _mainMenu.ShowMenu();

        // ───────────────────────────────────────────────────────────────
        // LÉPÉS 3: KILÉPÉS KEZELÉSE
        // ───────────────────────────────────────────────────────────────
        // Ha a felhasználó "2"-t választott (Kilépés), azonnal befejezzük.
        if (choice == MenuChoice.Exit)
        {
            _logger.LogInformation("Felhasználó kilépett");
            return; // Metódus befejezése, vissza a Program.cs-be
        }

        // ───────────────────────────────────────────────────────────────
        // LÉPÉS 4: ADATOK BETÖLTÉSE
        // ───────────────────────────────────────────────────────────────
        // Futárok és rendelések betöltése.
        // JELENLEGI ÁLLAPOT: Hardcoded adatok
        // KÉSŐBB: JSON fájlból vagy adatbázisból
        await LoadInitialDataAsync(cancellationToken);

        // ───────────────────────────────────────────────────────────────
        // LÉPÉS 5: SZIMULÁCIÓ ÉS UI FRISSÍTÉS PÁRHUZAMOSAN
        // ───────────────────────────────────────────────────────────────
        // Két Task párhuzamosan fut:
        // 1. Szimuláció Task (futárok dolgoznak, rendelések kézbesítése)
        // 2. UI frissítő Task (500ms-enként frissíti a konzolt)
        //
        // PÁRHUZAMOSSÁG:
        // - Task 1: DeliveryService párhuzamosan futtatja a futárokat (TPL)
        // - Task 2: UpdateUILoopAsync 500ms-enként frissít
        // - Mindkettő egyszerre fut!
        //
        // LEÁLLÍTÁS:
        // - Ha CTRL+C, a cancellationToken jelzi
        // - Mindkét Task megkapja a jelet
        // - Graceful shutdown (biztonságos leállás)
        await RunSimulationWithUIAsync(cancellationToken);

        // ───────────────────────────────────────────────────────────────
        // LÉPÉS 6: VÉGSŐ RIPORT MEGJELENÍTÉSE
        // ───────────────────────────────────────────────────────────────
        // A szimuláció befejeződött (vagy CTRL+C volt).
        // Most megjelenítjük a végső statisztikákat:
        // - Összes kézbesítés
        // - Késések száma
        // - Futárok teljesítménye
        _reportView.ShowFinalReport(
            _deliveryService.GetCouriers(),      // Futárok listája
            _deliveryService.GetOrders(),        // Rendelések listája
            _deliveryService.GetStatistics()     // (TotalDeliveries, TotalDelays)
        );

        _logger.LogInformation("Alkalmazás befejezve");
    }

    // ═══════════════════════════════════════════════════════════════════
    // ADATOK BETÖLTÉSE (HARDCODED - KÉSŐBB JSON/DB)
    // ═══════════════════════════════════════════════════════════════════
    // JELENLEGI ÁLLAPOT:
    // - 3 futár hardcoded adatokkal
    // - 10 rendelés random adatokkal
    //
    // KÉSŐBB (TODO):
    // - Futárok: couriers.json fájlból
    // - Rendelések: orders.json fájlból
    // - Zónák: zones.json fájlból
    // - Gráf: city-graph.json fájlból
    private async Task LoadInitialDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initial data loading started...");

        // 1. Város gráf betöltése
        var cityGraph = await _cityGraphLoader.LoadAsync(cancellationToken);
        _deliveryService.SetCityGraph(cityGraph); // vagy konstruktorban kapja, attól függ, nálatok hogy van

        // 2. Futárok betöltése JSON-ből
        var couriers = await _courierLoader.LoadAsync(cancellationToken);
        foreach (var courier in couriers)
        {
            _deliveryService.AddCourier(courier);
        }

        // 3. Rendelések betöltése JSON-ből
        var orders = await _orderLoader.LoadAsync(cancellationToken);
        foreach (var order in orders)
        {
            // Csak Pending rendeléseket addunk hozzá a szimulációhoz
            if (order.Status == OrderStatus.Pending)
            {
                _deliveryService.AddOrder(order);
            }
        }

        _logger.LogInformation(
            "Initial data loaded. Couriers: {CourierCount}, Orders: {OrderCount}",
            couriers.Count,
            orders.Count);
    }



        // TODO: JSON betöltés implementálás!
        // Példa:
        // var couriers = JsonSerializer.Deserialize<List<Courier>>(
        //     File.ReadAllText("Data/couriers.json")
        // );
        //
        // foreach (var courier in couriers)
        // {
        //     _deliveryService.AddCourier(courier);
        // }

        // ÁTMENETI MEGOLDÁS: Hardcoded adatok
        // (A SimulationRunner.cs-ből átemelve)
    }

    // ═══════════════════════════════════════════════════════════════════
    // SZIMULÁCIÓ ÉS UI FRISSÍTÉS PÁRHUZAMOS FUTTATÁSA
    // ═══════════════════════════════════════════════════════════════════
    // Ez a metódus koordinálja a két párhuzamos Task-ot.
    //
    // MŰKÖDÉS:
    // 1. Létrehoz 2 Task-ot:
    //    - simulationTask: DeliveryService.RunSimulationAsync()
    //    - uiTask: UpdateUILoopAsync()
    // 2. Task.WhenAll() várakozik MINDKÉT Task befejezésére
    // 3. Ha CTRL+C, OperationCanceledException kivétel
    // 4. Catch ágban kezelés és logolás
    private async Task RunSimulationWithUIAsync(CancellationToken cancellationToken)
    {
        // ───────────────────────────────────────────────────────────────
        // SZIMULÁCIÓ TASK INDÍTÁSA
        // ───────────────────────────────────────────────────────────────
        // A DeliveryService.RunSimulationAsync() metódus:
        // 1. Létrehoz Task-okat minden futárnak (Task.Run)
        // 2. Minden futár párhuzamosan fut (thread pool)
        // 3. Greedy algoritmussal választanak rendelést
        // 4. "Utaznak" és kézbesítenek
        // 5. Késés esetén NotificationService értesítést küld
        //
        // Ez a Task NEM várja meg az UI frissítést, PÁRHUZAMOSAN fut!
        var simulationTask = _deliveryService.RunSimulationAsync(cancellationToken);

        // ───────────────────────────────────────────────────────────────
        // UI FRISSÍTŐ TASK INDÍTÁSA
        // ───────────────────────────────────────────────────────────────
        // Az UpdateUILoopAsync() metódus:
        // 1. Végtelen ciklusban fut (while(!cancelled))
        // 2. 500ms-enként frissíti a konzolt
        // 3. Lekéri a futárok és rendelések állapotát
        // 4. Meghívja a SimulationView.UpdateDisplay()-t
        //
        // Ez a Task is PÁRHUZAMOSAN fut a szimulációval!
        var uiTask = UpdateUILoopAsync(cancellationToken);

        // ───────────────────────────────────────────────────────────────
        // VÁRAKOZÁS MINDKÉT TASK BEFEJEZÉSÉRE
        // ───────────────────────────────────────────────────────────────
        // Task.WhenAll() várja meg, amíg MINDKÉT Task befejeződik.
        //
        // NORMÁLIS BEFEJEZÉS:
        // - Szimuláció befejeződik (pl. minden rendelés kézbesítve)
        // - UI frissítő loop is leáll
        // - Task.WhenAll() visszatér
        //
        // CTRL+C ESETÉN:
        // - cancellationToken.IsCancellationRequested = true
        // - Mindkét Task OperationCanceledException-t dob
        // - Catch ágba ugrunk
        try
        {
            await Task.WhenAll(simulationTask, uiTask);
        }
        catch (OperationCanceledException)
        {
            // CTRL+C volt - ez normális leállítás, nem hiba!
            _logger.LogInformation("Szimuláció leállítva (felhasználó által)");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // UI FRISSÍTŐ LOOP (500MS-ENKÉNT)
    // ═══════════════════════════════════════════════════════════════════
    // Ez a metódus egy végtelen ciklusban fut, és frissíti a konzolt.
    //
    // VÉGREHAJTÁSI FOLYAMAT:
    // 1. while (!cancellationToken.IsCancellationRequested) - végtelen loop
    // 2. Adatok lekérése a DeliveryService-ből
    // 3. SimulationView.UpdateDisplay() meghívása (Console kiírás)
    // 4. await Task.Delay(500, cancellationToken) - 500ms várakozás
    // 5. Ismétlés (2. lépéstől)
    //
    // LEÁLLÍTÁS:
    // - CTRL+C esetén a cancellationToken jelzi
    // - Task.Delay OperationCanceledException-t dob
    // - Catch ágba ugrunk, break → kilépés a loop-ból
    private async Task UpdateUILoopAsync(CancellationToken cancellationToken)
    {
        // Végtelen loop (amíg nincs leállítási kérés)
        while (!cancellationToken.IsCancellationRequested)
        {
            // ───────────────────────────────────────────────────────────
            // ADATOK LEKÉRÉSE A DELIVERY SERVICE-BŐL
            // ───────────────────────────────────────────────────────────
            // GetCouriers() - futárok aktuális állapota
            // GetOrders() - rendelések aktuális állapota
            // GetStatistics() - (TotalDeliveries, TotalDelays) tuple
            //
            // THREAD-SAFETY:
            // Ezek a metódusok ConcurrentBag-ből olvasnak, ami thread-safe.
            // Több Task hozzáférhet egyszerre, nincs race condition veszély.
            var couriers = _deliveryService.GetCouriers();
            var orders = _deliveryService.GetOrders();
            var stats = _deliveryService.GetStatistics();

            // ───────────────────────────────────────────────────────────
            // UI FRISSÍTÉS - SIMULATIONVIEW MEGHÍVÁSA
            // ───────────────────────────────────────────────────────────
            // A SimulationView.UpdateDisplay() metódus:
            // 1. Console.Clear() - törli a képernyőt
            // 2. Kiírja a statisztikákat (kézbesítések, késések)
            // 3. Kiírja a futárok állapotát:
            //    - Név, státusz (🟢 Elérhető / 🚚 Szállít)
            //    - Kézbesítések száma
            // 4. Kiírja az aktív rendeléseket (első 5 db)
            //
            // CSAK MEGJELENÍTÉS! Nincs logika, csak Console.WriteLine-ok.
            _simulationView.UpdateDisplay(couriers, orders, stats);

            // ───────────────────────────────────────────────────────────
            // VÁRAKOZÁS 500 MILLISZEKUNDUMIG
            // ───────────────────────────────────────────────────────────
            // await Task.Delay(500, cancellationToken):
            // - 500ms várakozás (fél másodperc)
            // - Aszinkron (nem blokkolja a szálat, más Task-ok futhatnak)
            // - cancellationToken: ha CTRL+C, azonnal leáll
            //
            // MIÉRT NE Thread.Sleep(500)?
            // - Thread.Sleep BLOKKOLJA a szálat (pazarlás)
            // - await Task.Delay NEM blokkolja (hatékony)
            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // CTRL+C történt a várakozás közben
                // Kilépünk a loop-ból (break)
                _logger.LogDebug("UI frissítő loop leállítva");
                break;
            }
        }

        // Loop vége - Task befejeződik
    }
}

// ═══════════════════════════════════════════════════════════════════════
// MENÜ VÁLASZTÁS ENUM
// ═══════════════════════════════════════════════════════════════════════
// A MainMenuView.ShowMenu() metódus ezt az enum-ot adja vissza.
//
// ÉRTÉKEK:
// - StartSimulation: Felhasználó "1"-et választott
// - Exit: Felhasználó "2"-t választott

// ═══════════════════════════════════════════════════════════════════════
// VÉGREHAJTÁSI FOLYAMAT ÖSSZEFOGLALÁS (APPLICATION)
// ═══════════════════════════════════════════════════════════════════════
//
// 1. Program.cs meghívja: await app.RunAsync(cancellationToken)
// 2. Application.RunAsync() elindul
// 3. _mainMenu.ShowMenu() → Felhasználó választ (1 vagy 2)
// 4. Ha Exit → return (befejezés)
// 5. Ha Start → LoadInitialData() (futárok, rendelések betöltése)
// 6. RunSimulationWithUIAsync() → 2 Task párhuzamosan:
//    ├─ simulationTask: _deliveryService.RunSimulationAsync()
//    │   ├─ Task.Run() minden futárnak
//    │   ├─ SimulateCourierAsync(courier) - végtelen loop
//    │   ├─ FindNearestOrder() - Greedy algoritmus
//    │   ├─ AssignOrderToCourier() - Thread-safe hozzárendelés
//    │   ├─ "Kézbesítés" (Task.Delay szimuláció)
//    │   └─ CompleteDelivery() - Késés ellenőrzés + értesítés
//    │
//    └─ uiTask: UpdateUILoopAsync()
//        ├─ while (!cancelled) - Végtelen loop
//        ├─ Adatok lekérése (GetCouriers, GetOrders, GetStatistics)
//        ├─ _simulationView.UpdateDisplay() - Console frissítés
//        └─ await Task.Delay(500) - 500ms várakozás
//
// 7. Task.WhenAll(simulationTask, uiTask) - Várakozás mindkettőre
// 8. CTRL+C esetén: OperationCanceledException → catch → logolás
// 9. _reportView.ShowFinalReport() - Végső statisztikák kiírása
// 10. return - Vissza a Program.cs-be
//
// ═══════════════════════════════════════════════════════════════════════
