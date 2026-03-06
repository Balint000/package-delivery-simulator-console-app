# 🔄 Presentation Layer + DI + IHost Refactoring

> **Dátum:** 2026. március 3.
> **Cél:** A monolitikus `Program.cs` átalakítása tiszta architektúrára (.NET best practices szerint)

---

## 📋 Tartalom

1. [Miért volt szükség a refaktorálásra?](#miért-volt-szükség-a-refaktorálásra)
2. [Mit csináltunk eddig?](#mit-csináltunk-eddig)
3. [Fájl-struktúra változások](#fájl-struktúra-változások)
4. [Részletes változások](#részletes-változások)
5. [Következő lépések](#következő-lépések)

---

## Miért volt szükség a refaktorálásra?

### 🔴 Problémák a jelenlegi `Program.cs`-ben:

```csharp
// RÉGI Program.cs (rossz)
static void Main()
{
    // 1. Gráf betöltés közvetlenül
    var cityGraph = CityGraphLoader.LoadFromJson("Data/city-graph.json");
    
    // 2. Objektumok direkt létrehozása
    var courier = new Courier { ... };
    var order = new DeliveryOrder { ... };
    
    // 3. Konzol kiírások közvetlenül
    Console.WriteLine("📦 Order: ...");
    
    // 4. Üzleti logika a Main-ben
    var (path, time) = cityGraph.FindShortestPath(0, 1);
    for (int i = 0; i < path.Count; i++) {
        Thread.Sleep(100); // Szimuláció
        Console.WriteLine("...");
    }
}

Miért rossz?

- Nincs Dependency Injection → Nem tesztelhető
- Nincs Separation of Concerns → Minden egy helyen van
- Nincs ILogger → Console.WriteLine helyett strukturált naplózás kellene
- Nem skálázható → Több futár párhuzamos szimulációja lehetetlennek tűnik
- Nem .NET konform → Nem használ Generic Host-ot, IOptions-t, stb.



1. Infrastructure réteg interfészelése

Probléma: A CityGraph konkrét osztály volt, nem lehetett DI-ban használni.

Megoldás: ICityGraph interfész létrehozása
Fájlok:

    Új: Infrastructure/Graph/ICityGraph.cs

    Módosított: Infrastructure/Graph/CityGraph.cs (implements ICityGraph)


2. Domain entitások kibővítése

Probléma: A Courier és DeliveryOrder entitások nem tartalmazták az összes szükséges információt a dinamikus rendelés kezeléshez.
Ezek a fájlok bővítésre is kerültek.

3. WarehouseService létrehozása

Probléma: Nincs központi hely, ahol a warehouse-ok kezelése történik.

Megoldás: WarehouseService létrehozása interface-szel.
Fájlok:

    Új: Infrastructure/Interfaces/IWarehouseService.cs

    Új: Infrastructure/Services/WarehouseService.cs


MINDEN TÁVOLSÁG SZÁMÍTÁS GRÁF ALAPÚ (Dijkstra)!
Koordinátákat CSAK node mapping-hez használjuk!
A gráf éleinek súlyai = utazási idő percben
A forgalom változtatja az élek súlyait!

Újítások a struktúrában:
package-delivery-simulator-console-app/
├── Program.cs                         (később lesz refaktorálva IHost-tal)
├── Domain/
│   ├── Entities/
│   │   ├── Courier.cs                 ✨ (kibővítve: warehouse tracking, performance)
│   │   └── DeliveryOrder.cs           ✨ (kibővítve: delay tracking, warehouse)
│   └── ...
├── Infrastructure/
│   ├── Graph/
│   │   ├── ICityGraph.cs              🆕 (interfész DI-hoz)
│   │   └── CityGraph.cs               ✨ (implementálja ICityGraph-ot)
│   ├── Interfaces/
│   │   └── IWarehouseService.cs       🆕
│   └── Services/
│       └── WarehouseService.cs        🆕 (warehouse management)
└── Services/
    ├── Interfaces/                     🆕 (később: IDeliverySimulationService)
    └── Simulation/
        └── DeliverySimulationService.cs  (később lesz implementálva)


Ami még hátra van:

1. OrderQueueService
- Dinamikus rendelések kezelése (queue)
- Rendelések kiosztása futárokhoz (dispatcher)

2. RouteOptimizationService
- Több rendelés optimális sorrendbe rendezése
- Nearest Neighbor TSP közelítés

3. DeliverySimulationService
- Futár workflow: IDLE → warehouse → pickup → deliver → IDLE
- Dinamikus rendelések kezelése futás közben

4. Presentation Layer
- ILiveConsoleRenderer interfész
- LiveConsoleRenderer implementáció (színes, élő konzol)

5. Program.cs refaktorálás
- Generic Host (IHost)
- Dependency Injection container
- ILogger használata

6. TPL (Task Parallel Library)
- Több futár párhuzamos szimulációja
- Thread-safe adatstruktúrák
- CancellationToken kezelés
