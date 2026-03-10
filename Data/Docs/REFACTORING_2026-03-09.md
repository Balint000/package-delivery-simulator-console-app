# 🔄 Refaktorálás – 2026-03-09: Egyszerűsítés + OOP belépési pont

> **Dátum:** 2026. március 9.
> **Branch:** `feature/simplify-and-nearest-neighbor`
> **Státusz:** ✅ Lefordul és fut — 40/40 rendelés kézbesítve

---

## 📋 Tartalom

1. [Hogyan működik a program jelenleg?](#hogyan-működik-a-program-jelenleg)
2. [Mi változott ezen a napon?](#mi-változott-ezen-a-napon)
3. [Törölt fájlok](#törölt-fájlok)
4. [Módosított fájlok](#módosított-fájlok)
5. [Új fájlok](#új-fájlok)

---

## Hogyan működik a program jelenleg?

### Indítás és setup

A program belépési pontja a `Program.cs` — egy `internal static class Program` a hagyományos `static async Task Main()` metódussal. A setup lépései:

```
Program.Main()
  │
  ├─ CityGraphLoader.LoadFromJson()     → 22 csúcs, 28 él betöltése (city-graph.json)
  ├─ WarehouseService.Initialize()      → 4 warehouse node azonosítása és cache-elése
  ├─ CourierLoader.LoadAsync()          → 5 futár betöltése (Courier.json)
  ├─ OrderLoader.LoadAsync()            → 40 rendelés betöltése (Order.json)
  └─ SimulationOrchestrator.RunAsync()  → szimuláció indítása
```

### A városgráf

22 csúcsból és 28 élből álló súlyozott, irányítatlan gráf. Minden csúcs egy helyet reprezentál:

| Típus | Példa | Szerepe |
|-------|-------|---------|
| `Warehouse` | Zóna 1 Raktár (Node 0) | Futárok innen indulnak, ide térnek vissza |
| `DeliveryPoint` | Északi pályaudvar (Node 1) | Kézbesítési cím |
| `Intersection` | Centrum (Node 19) | Áthaladási pont, nem cél |

Minden zónának (1–4) saját warehouse-a van. Az élek súlya: utazási idő percben.
A forgalom (`TrafficMultiplier`) véletlenszerűen változik szimuláció közben.

### A szimuláció menete

```
SimulationOrchestrator.RunAsync()
  │
  ├─ 1. GreedyAssignmentService.AssignAll()
  │      Minden futárhoz zóna-alapú + kapacitás-szűréssel rendel rendelést (max 3/futár).
  │      Távolságmérés: Dijkstra — courier.CurrentNodeId → order.AddressNodeId
  │
  ├─ 2. ConcurrentQueue feltöltése
  │      A még Pending rendelések queue-ba kerülnek (TPL-re kész, thread-safe).
  │
  └─ 3. foreach courier → RunCourierLoopAsync()
           │
           ├─ Snapshot a jelenlegi batch-ről (.ToList() — módosítás elleni védelem)
           ├─ foreach order → DeliverySimulationService.SimulateDeliveryAsync()
           │      │
           │      ├─ WarehouseService.FindBestWarehouseForCourier()
           │      │    Saját zóna warehouse-ok közül Dijkstra legközelebbi,
           │      │    fallback: abszolút legközelebbi.
           │      │
           │      ├─ Futár bemegy a raktárba (ha nem ott van) → TraversePath()
           │      ├─ Csomag felvétel → InTransit státusz
           │      ├─ Ideális idő kiszámítása (forgalom nélkül, teljes út)
           │      ├─ Kézbesítés → TraversePath() → Delivered státusz
           │      ├─ Késés detektálás (tényleges > ideális × 1.05)
           │      └─ Ha késett → INotificationService.NotifyDelay()
           │
           └─ RefillCourier() — queue-ból tölt MaxCapacity-ig (zóna-szűréssel)
```

### Koordináta-mentes navigáció

**Minden pozíció és navigáció node ID-val történik — nincs koordináta-közelítés.**

```csharp
// Futár pozíciója:
courier.CurrentNodeId  // int — melyik node-on áll

// Rendelés célpontja:
order.AddressNodeId    // int — melyik node-ra kell kézbesíteni

// Távolságmérés:
_cityGraph.FindShortestPath(courier.CurrentNodeId, order.AddressNodeId)
// → Dijkstra, az élek CurrentTimeMinutes súlyával (forgalommal)
```

### Adatstruktúrák

| Entitás | Forrás | Kulcs mezők |
|---------|--------|-------------|
| `Courier` | `Courier.json` | `CurrentNodeId`, `AssignedZoneIds`, `MaxCapacity` |
| `DeliveryOrder` | `Order.json` | `AddressNodeId`, `ZoneId`, `Status` |
| `GraphNode` | `city-graph.json` | `Id`, `Type`, `ZoneId` |
| `EdgeWeight` | `city-graph.json` | `IdealTimeMinutes`, `CurrentTimeMinutes`, `TrafficMultiplier` |

---

## Mi változott ezen a napon?

### 1. Koordináta-mentes `WarehouseService`

**Előtte:** A `WarehouseService` tartalmazott egy `FindNearestWarehouse(Location)` metódust, ami Euklideszi távolsággal közelítette a legközelebbi warehouse-t. Ez pontatlan volt — különösen, ha két node közel volt egymáshoz a koordináta-térben, de messze a gráfban.

**Utána:** Csak `FindNearestWarehouseFromNode(int nodeId)` és `FindBestWarehouseForCourier(Courier)` létezik — mindkettő Dijkstra-alapú. Koordináta nem szerepel a döntésben.

**Új metódus — `FindBestWarehouseForCourier(Courier courier)`:**
```
1. Szűrés: futár saját zónáiban lévő warehouse-ok
2. Dijkstra: ezek közül a legközelebbi
3. Fallback: ha nincs zónás warehouse → abszolút legközelebbi
```

Ez a logika korábban a `DeliverySimulationService`-ben volt szétszórva (~20 sor). Most egy metódushívás.

**Új privát helper — `FindClosestWarehouseFromList()`:**
A `FindNearestWarehouseFromNode` és `FindBestWarehouseForCourier` is ezt hívja belül — a Dijkstra-loop egyszer van megírva (DRY elv).

---

### 2. `INotificationService` + `ConsoleNotificationService`

**Előtte:** A késési értesítés logika a `DeliverySimulationService.SimulateDeliveryAsync()` végén élt, hardkódolva:
```csharp
if (!order.CustomerNotifiedOfDelay)
{
    order.CustomerNotifiedOfDelay = true;
    _logger.LogInformation("📧 Ügyfélértesítés: ...");
}
```

**Utána:** Külön interfész és implementáció:
```csharp
// INotificationService.cs
void NotifyDelay(DeliveryOrder order, int delayMinutes);

// ConsoleNotificationService.cs — idempotens implementáció
// Ha már értesítve volt → kihagyja (CustomerNotifiedOfDelay flag alapján)
```

A `DeliverySimulationService`-ben most csak egy sor:
```csharp
_notificationService.NotifyDelay(order, delayMinutes);
```

**Miért jobb?** Az értesítés módja cserélhető az implementáció lecserélésével — az interfész és a szimuláció nem változik. E-mail, SMS, webhook — mind beköthetők.

---

### 3. `Program.cs` → `internal static class Program`

**Előtte:** Top-level statements — nincs explicit osztály, a kód "lebeg".

**Utána:** Hagyományos `internal static class Program` + `private static async Task Main()`. A logika privát statikus metódusokban él:

```
Program (static class)
  ├─ Main()                  — belépési pont, koordinál
  ├─ BuildLoggerFactory()    — logger gyár létrehozása
  ├─ LoadCityGraph()         — városgráf betöltése
  ├─ BuildWarehouseService() — WarehouseService init
  ├─ LoadCouriersAsync()     — futárok betöltése
  ├─ LoadOrdersAsync()       — rendelések betöltése
  ├─ BuildOrchestrator()     — service-ek összerakása
  ├─ PrintHeader()           — fejléc kiírása
  └─ PrintSummary()          — összesítő kiírása
```

**Miért jobb?**
- OOP elvnek megfelel — az alkalmazás is egy objektum
- Minden metódusnak egy felelőssége van
- Tesztelhető — az egyes metódusok izoláltan vizsgálhatók
- Átlátható — a `Main()` olvasásából azonnal látszik a teljes folyamat

---

### 4. `SimulationOrchestrator.cs` — build fix

**Hiányzó `using` hozzáadva:**
```csharp
using package_delivery_simulator_console_app.Presentation.Interfaces;
```
Az `OrchestratorResult` record az `ISimulationOrchestrator.cs`-ben van definiálva, ami a `Presentation.Interfaces` névtérben él. Ez okozta a `CS0246` build hibát.

---

## Törölt fájlok

| Fájl | Ok |
|------|----|
| `Domain/Entities/RoutePlan.cs` | Üres — DB-s koncepció, nem kell |
| `Domain/Entities/StatusHistory.cs` | Üres — DB-s koncepció, nem kell |
| `Domain/Entities/Zone.cs` | Üres — zóna már csak `int ZoneId` a node-ban |
| `Domain/ValueObjects/Coordinate.cs` | Duplikátum — `Location.cs` ugyanezt csinálja |
| `Infrastructure/Repositories/*` (mind a 4) | Üres — nincs DB, nincs szükség |
| `Infrastructure/Time/SimulationClock.cs` | Üres — az orchestrator `Stopwatch`-t használ |
| `Infrastructure/CityGraphBuilder.cs` | Hardkódolt 8-csúcsos város, `CityGraphLoader` helyettesíti |
| `Presentation/Models/CourierStatus.cs` | Üres — duplikátum (`Domain/Enums/CourierStatus.cs`) |
| `Utils/DistanceCalculator.cs` | Üres — koordinátás logika, nem kell |
| `Utils/RandomDataGenerator.cs` | Üres — nincs terv rá |

---

## Módosított fájlok

| Fájl | Változás |
|------|---------|
| `Infrastructure/Interfaces/IWarehouseService.cs` | `+ FindBestWarehouseForCourier(Courier)` |
| `Infrastructure/Services/WarehouseService.cs` | `+ FindBestWarehouseForCourier()` + `FindClosestWarehouseFromList()` privát helper |
| `Services/Simulation/DeliverySimulationService.cs` | Warehouse-blokk és értesítés-blokk kiszervezve — `_warehouseService` és `_notificationService` hívások |
| `Services/Simulation/SimulationOrchestrator.cs` | Hiányzó `using Presentation.Interfaces` hozzáadva |
| `Program.cs` | Top-level → `internal static class Program` + privát metódusok |

---

## Új fájlok

| Fájl | Leírás |
|------|--------|
| `Services/Interfaces/INotificationService.cs` | Értesítési service interfésze |
| `Services/Notification/ConsoleNotificationService.cs` | Konzol-alapú implementáció, idempotens |

---

## ➡️ Következő lépés

**Step 2 — `NearestNeighborRouteService`**

Ha egy futárnak egyszerre 3 rendelése van, jelenleg a sorrendjük véletlenszerű (hozzárendelési sorrend). A Nearest Neighbor algoritmus optimálisabb sorrendet határoz meg:

```
Kiindulás: warehouse
→ Melyik rendelés a legközelebb? → azt kézbesíti először
→ Onnan melyik a legközelebb? → azt másodiknak
→ ... amíg van rendelés
```

Ez a `Services/Routing/NearestNeighborRouteService.cs` fájlba kerül, ami jelenleg üres.
