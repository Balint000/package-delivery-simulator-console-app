// ============================================================
// Program.cs
// ============================================================
// A program belépési pontja — itt indul minden.
//
// ARCHITEKTÚRA (manuális bekötés, DI nélkül):
//
//   Program.cs
//     │
//     ├── CityGraphLoader      → betölti a városgráfot (JSON)
//     ├── WarehouseService     → megkeresi a raktárakat a gráfban
//     ├── CourierLoader        → betölti a futárokat (JSON)
//     ├── OrderLoader          → betölti a rendeléseket (JSON)
//     ├── GreedyAssignmentService → hozzárendeli a rendeléseket futárokhoz
//     └── DeliverySimulationService → szimulálja a kézbesítéseket
//
// MIÉRT MANUÁLIS BEKÖTÉS (nem IHost + DI)?
//   - Jobban látható, mi jön létre és milyen sorrendben
//   - Kezdőknek érthetőbb mint a "varázslatos" DI container
//   - Később könnyű átírni IHost-ra (csak a new-okat kell kicserélni)
//
// SZIMULÁCIÓ MENETE:
//   1. Városgráf betöltése
//   2. Futárok és rendelések betöltése
//   3. Minden rendeléshez: legközelebbi futár megkeresése (greedy)
//   4. Minden hozzárendelt (futár, rendelés) pár szimulálása egymás után
//   5. Összesítő kiírása a végén
// ============================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using package_delivery_simulator.Domain.Enums;
using package_delivery_simulator_console_app.Infrastructure.Graph;
using package_delivery_simulator_console_app.Infrastructure.Interfaces;
using package_delivery_simulator_console_app.Infrastructure.Loaders;
using package_delivery_simulator_console_app.Infrastructure.Services;
using package_delivery_simulator_console_app.Services.Assignment;
using package_delivery_simulator_console_app.Services.Simulation;

// ============================================================
// Program.cs — módosított részlet
// ============================================================
// CSAK A MEGVÁLTOZOTT SOROK vannak itt.
// A többi rész (városgráf, warehouse, greedy, szimuláció) változatlan.
//
// VÁLTOZÁSOK:
//   1. DatabaseInitializer létrehozása és meghívása (0. lépés)
//   2. CourierLoader és OrderLoader konstruktorba dbInitializer kell
// ============================================================

// === HOZZÁADANDÓ using a fájl tetejére ===
using package_delivery_simulator_console_app.Infrastructure.Database;

// ============================================================
// LOGGER GYÁR LÉTREHOZÁSA
// ============================================================
// A ILogger<T> interfészt nem lehet "sima" new-val létrehozni.
// Szükségünk van egy LoggerFactory-ra, ami előállítja őket.
//
// AddSimpleConsole: minden log sor egy sorban jelenik meg,
// időbélyeggel és szinttel (Info, Warning, Error stb.)
// ============================================================

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        // Egy soros formátum: [12:34:56 INF] Üzenet szövege
        options.SingleLine = true;

        // Időbélyeg formátuma (óra:perc:másodperc)
        options.TimestampFormat = "[HH:mm:ss] ";
    });

    // Csak Information szintű és annál fontosabb logokat mutatunk
    // (Debug szintű logok nem jelennek meg — azok túl részletesek)
    builder.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// LÉPÉS 0 (ÚJ): ADATBÁZIS INICIALIZÁLÁSA
// ============================================================
// Ha a simulator.db fájl nem létezik:
//   → létrehozza a táblákat és beolvassa a JSON fájlokat (seed)
// Ha már létezik:
//   → nem csinál semmit, csak kapcsolatot biztosít a loadereknek
// ============================================================

Console.WriteLine("━━━ 0. ADATBÁZIS INICIALIZÁLÁSA ━━━━━━━━━━━━━━━━━━━━━━");

var dbInitializer = new DatabaseInitializer(
    loggerFactory.CreateLogger<DatabaseInitializer>()
    // Opcionális 2. paraméter: "Data/simulator.db" az alapértelmezett
);

await dbInitializer.InitializeAsync();
Console.WriteLine();


// ============================================================
// FEJLÉC KIÍRÁSA
// ============================================================

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║       🚚 CSOMAG KÉZBESÍTÉS SZIMULÁCIÓ               ║");
Console.WriteLine("║          Demo City — Teljes szimuláció               ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

// ============================================================
// LÉPÉS 1: VÁROSGRÁF BETÖLTÉSE
// ============================================================
// A CityGraphLoader beolvassa a city-graph.json fájlt,
// és létrehoz egy ICityGraph objektumot (csúcsok + élek + Dijkstra).
// ============================================================

Console.WriteLine("━━━ 1. VÁROSGRÁF BETÖLTÉSE ━━━━━━━━━━━━━━━━━━━━━━━━━━━");

ICityGraph cityGraph;

try
{
    cityGraph = CityGraphLoader.LoadFromJson("Data/city-graph.json");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ Hiba a városgráf betöltésekor: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine("\nNyomj meg egy billentyűt a kilépéshez...");
    Console.ReadKey();
    return; // Leállítjuk a programot, ha nincs gráf
}

Console.WriteLine($"✅ Gráf betöltve: {cityGraph.Nodes.Count} csúcs\n");

// ============================================================
// LÉPÉS 2: WAREHOUSE SERVICE INICIALIZÁLÁSA
// ============================================================
// A WarehouseService megkeresi a gráfban a Warehouse típusú
// csúcsokat, és ezeket cache-eli a gyors eléréshez.
// Muszáj az Initialize() metódust meghívni egyszer!
// ============================================================

Console.WriteLine("━━━ 2. WAREHOUSE SERVICE ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

IWarehouseService warehouseService = new WarehouseService(
    cityGraph,
    loggerFactory.CreateLogger<WarehouseService>()
);

// Initialize() megkeresi az összes Warehouse típusú node-ot
warehouseService.Initialize();

var warehouses = warehouseService.GetAllWarehouses();
Console.WriteLine($"✅ {warehouses.Count} raktár megtalálva:");
foreach (var wh in warehouses)
{
    Console.WriteLine($"   📦 [{wh.Id}] {wh.Name} (Zóna {wh.ZoneId})");
}
Console.WriteLine();

// ============================================================
// LÉPÉS 3: FUTÁROK BETÖLTÉSE  (módosítva)
// ============================================================

Console.WriteLine("━━━ 3. FUTÁROK BETÖLTÉSE ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

var courierLoader = new CourierLoader(
    loggerFactory.CreateLogger<CourierLoader>(),
    dbInitializer    // <-- ÚJ: dbInitializer az adatbázis kapcsolathoz
);

var couriers = await courierLoader.LoadAsync();

// LoadAsync() aszinkron — a "await" megvárja az eredményt.
// A ".GetAwaiter().GetResult()" szinkron hívás: mivel a Main
// metódus nem async, így kell meghívni az async metódust.
// (Később, IHost-tal, ez elegánsabban megoldható.)

Console.WriteLine($"✅ {couriers.Count} futár betöltve:");
foreach (var c in couriers)
{
    Console.WriteLine(
        $"   👤 [{c.Id}] {c.Name} — Zónák: [{string.Join(", ", c.AssignedZoneIds)}]");
}
Console.WriteLine();

// ============================================================
// LÉPÉS 4: RENDELÉSEK BETÖLTÉSE  (módosítva)
// ============================================================

Console.WriteLine("━━━ 4. RENDELÉSEK BETÖLTÉSE ━━━━━━━━━━━━━━━━━━━━━━━━━━");

var orderLoader = new OrderLoader(
    loggerFactory.CreateLogger<OrderLoader>(),
    dbInitializer    // <-- ÚJ: dbInitializer az adatbázis kapcsolathoz
);

var orders = await orderLoader.LoadAsync();


Console.WriteLine($"✅ {orders.Count} rendelés betöltve:");
foreach (var o in orders)
{
    Console.WriteLine(
        $"   📦 {o.OrderNumber} — {o.CustomerName} (Zóna {o.ZoneId})");
}
Console.WriteLine();

// ============================================================
// LÉPÉS 5: RENDELÉSEK HOZZÁRENDELÉSE FUTÁROKHOZ (GREEDY)
// ============================================================
// A GreedyAssignmentService minden rendeléshez megkeresi
// a legközelebbi szabad futárt, és hozzárendeli.
//
// FONTOS: Ha kifogy a szabad futár, a maradék rendelések
// hozzárendelés nélkül maradnak (null lesz a futárjuk).
// ============================================================

Console.WriteLine("━━━ 5. GREEDY HOZZÁRENDELÉS ━━━━━━━━━━━━━━━━━━━━━━━━━━");

var assignmentService = new GreedyAssignmentService(
    cityGraph,
    loggerFactory.CreateLogger<GreedyAssignmentService>()
);

// AssignAll() visszaad egy Dictionary<int, Courier?>-t:
//   kulcs   = rendelés ID
//   érték   = hozzárendelt futár (vagy null ha nem sikerült)
var assignments = assignmentService.AssignAll(orders, couriers);

// Összesítő kiírása
int assignedCount = assignments.Values.Count(c => c != null);
int unassignedCount = assignments.Values.Count(c => c == null);

Console.WriteLine($"\n✅ Hozzárendelés kész: {assignedCount} sikeres, {unassignedCount} sikertelen");

foreach (var (orderId, courier) in assignments)
{
    var order = orders.First(o => o.Id == orderId);
    if (courier != null)
    {
        Console.WriteLine(
            $"   ✔ {order.OrderNumber} → {courier.Name}");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(
            $"   ✘ {order.OrderNumber} → nincs szabad futár!");
        Console.ResetColor();
    }
}

Console.WriteLine();

// ============================================================
// LÉPÉS 6: SZIMULÁCIÓ FUTTATÁSA
// ============================================================
// Minden hozzárendelt (futár + rendelés) párt szimulálunk
// EGYMÁS UTÁN (szekvenciálisan).
//
// MIÉRT NEM PÁRHUZAMOSAN?
// A TPL (Task Parallel Library) a következő nagy lépés.
// Most előbb értjük meg az egy futáros szimulációt,
// majd kiterjesztjük több párhuzamos futárra.
// ============================================================

Console.WriteLine("━━━ 6. SZIMULÁCIÓ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("(Nyomj meg egy billentyűt az indításhoz...)");
Console.ReadKey();
Console.WriteLine();

// Program.cs — csak a megváltozott sor (6. lépés eleje)
// A dbInitializer-t 4. paraméterként kell átadni:

var simulationService = new DeliverySimulationService(
    cityGraph,
    warehouseService,
    loggerFactory.CreateLogger<DeliverySimulationService>(),
    dbInitializer    // <-- ÚJ 4. paraméter
);

// Megszakítás kezelése: ha Ctrl+C-t nyom a felhasználó,
// a CancellationToken jelzi az async metódusoknak, hogy álljanak le.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Megakadályozzuk az azonnali leállást
    cts.Cancel();    // Jelzünk a service-eknek: álljatok le szépen
    Console.WriteLine("\n⚠️  Megszakítás kérve... leállítás folyamatban.");
};

// Szimuláció eredmények gyűjtése
int totalSuccess = 0;
int totalDelayed = 0;
int totalFailed = 0;

// Végigmegyünk az összes hozzárendelésen
foreach (var (orderId, assignedCourier) in assignments)
{
    // Ha nem sikerült futárt rendelni ehhez a rendeléshez, kihagyjuk
    if (assignedCourier == null) continue;

    var order = orders.First(o => o.Id == orderId);

    Console.WriteLine(new string('─', 54));
    Console.WriteLine(
        $"🚚 Szimuláció: {assignedCourier.Name} → {order.OrderNumber}");
    Console.WriteLine(
        $"   Ügyfél: {order.CustomerName} | Cím: {order.AddressText}");
    Console.WriteLine();

    try
    {
        // SimulateDeliveryAsync() elvégzi a teljes kézbesítési folyamatot:
        // futár → raktár → csomag felvétel → kézbesítési cím → kész
        var result = simulationService
            .SimulateDeliveryAsync(assignedCourier, order, cts.Token)
            .GetAwaiter()
            .GetResult();

        // Eredmény kiírása
        if (result.Success)
        {
            totalSuccess++;

            if (result.WasDelayed)
            {
                totalDelayed++;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    $"   ⚠️  KÉSVE kézbesítve | " +
                    $"Tényleges: {result.ActualTimeMinutes} perc | " +
                    $"Ideális: {result.IdealTimeMinutes} perc | " +
                    $"Késés: +{result.DelayMinutes} perc");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    $"   ✅ Időben kézbesítve | " +
                    $"Tényleges: {result.ActualTimeMinutes} perc | " +
                    $"Ideális: {result.IdealTimeMinutes} perc");
            }

            Console.ResetColor();
        }
        else
        {
            totalFailed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("   ❌ Szimuláció sikertelen!");
            Console.ResetColor();
        }
    }
    catch (OperationCanceledException)
    {
        // Ctrl+C esetén ide kerülünk — leállítjuk a szimulációt
        Console.WriteLine("\n⚠️  Szimuláció megszakítva.");
        break;
    }

    Console.WriteLine();
}

// ============================================================
// LÉPÉS 7: VÉGSŐ ÖSSZESÍTŐ
// ============================================================

Console.WriteLine("━━━ ÖSSZESÍTŐ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"   📦 Összes rendelés:      {orders.Count}");
Console.WriteLine($"   ✔  Hozzárendelve:        {assignedCount}");
Console.WriteLine($"   ✅ Sikeresen kézbesítve: {totalSuccess}");

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"   ⚠️  Késve kézbesítve:    {totalDelayed}");
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"   ❌ Sikertelen:           {totalFailed}");
Console.ResetColor();

Console.WriteLine();

// Futár teljesítmény összesítő
Console.WriteLine("━━━ FUTÁR TELJESÍTMÉNY ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
foreach (var courier in couriers.Where(c => c.TotalDeliveriesCompleted > 0))
{
    Console.WriteLine(
        $"   👤 {courier.Name,-20} | " +
        $"Kézbesítések: {courier.TotalDeliveriesCompleted,2} | " +
        $"Késések: {courier.TotalDelayedDeliveries,2} | " +
        $"Átlag: {courier.AverageDeliveryTime:F1} perc");
}

Console.WriteLine();
Console.WriteLine("Nyomj meg egy billentyűt a kilépéshez...");
Console.ReadKey();
