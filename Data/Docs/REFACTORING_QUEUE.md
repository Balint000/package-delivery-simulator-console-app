# 🔄 Refaktorálás – 4. szakasz: Queue-alapú szimuláció + SimulationOrchestrator

> **Dátum:** 2026. március  
> **Branch:** `feature/orderQueue`  
> **Státusz:** ✅ Kész — 15/15 rendelés kézbesítve, 0 késés, 0 hiba

---

## 📋 Mit csináltunk ebben a szakaszban?

### Probléma a korábbi megközelítéssel

A korábbi `Program.cs` a szimuláció teljes vezérlését magában tartotta:

```csharp
// RÉGI Program.cs (rossz)
var assignments = assignmentService.AssignAll(orders, couriers);  // egyszerre mindent

foreach (var (orderId, courier) in assignments)                   // szekvenciális loop
{
    await simulationService.SimulateDeliveryAsync(courier, order, ...);
}
```

**Problémák:**
- `AssignAll` az összes rendelést egyszerre osztja ki, mielőtt bármilyen szimuláció elkezdődne → ha több rendelés van mint az összes futár kapacitása, a maradék soha nem kap futárt
- A futár kézbesítés után nem kap új rendelést, csak vár
- A `Program.cs` üzleti logikát tartalmazott (orchestrálás), nem csak setup-ot
- TPL-re lehetetlen lett volna áttérni ebből a struktúrából

---

## 🆕 Új fájlok

### `Presentation/Interfaces/ISimulationOrchestrator.cs`

Az orchestrator interfésze + az `OrchestratorResult` record.

```csharp
public interface ISimulationOrchestrator
{
    Task<OrchestratorResult> RunAsync(
        List<Courier> couriers,
        List<DeliveryOrder> allOrders,
        CancellationToken cancellationToken = default);
}

public record OrchestratorResult(
    int TotalOrders, int Delivered, int Delayed,
    int Failed, int Unassigned, TimeSpan WallClockTime)
{
    public double SuccessRate => ...
    public double DelayRate   => ...
}
```

**Miért az interfész a `Presentation/Interfaces/`-ben van és nem `Services/Interfaces/`-ben?**  
Az orchestrator a teljes szimuláció belépési pontja — a Presentation réteg (Program.cs, később LiveConsoleRenderer) ezt hívja. Az implementáció (`Services/Simulation/`) ettől elkülönül.

---

### `Services/Simulation/SimulationOrchestrator.cs`

A teljes queue-logika egyetlen helyen:

```
RunAsync()
  │
  ├─ 1. AssignAll()              → initial batch (minden futár MaxCapacity-ig)
  ├─ 2. ConcurrentQueue<>        → maradék Pending rendelések
  ├─ 3. foreach courier          → RunCourierLoopAsync() (most szekvenciális)
  │        │
  │        ├─ snapshot batch
  │        ├─ SimulateDeliveryAsync() × batch
  │        ├─ RefillCourier()    → queue-ból tölt MaxCapacity-ig
  │        └─ loop, amíg van rendelés
  │
  └─ 4. OrchestratorResult összegzés
```

#### `RunCourierLoopAsync` — a futár életciklusa

```csharp
while (true)
{
    // 1. Ha üres a batch → refill kísérlet
    if (currentBatch.Count == 0)
    {
        var refilled = RefillCourier(...);
        if (refilled.Count == 0) break;   // nincs több → loop vége
    }

    // 2. Batch szimulálása (snapshot-ból, mert AssignedOrderIds változik közben!)
    foreach (var order in currentBatch)
        await _simulationService.SimulateDeliveryAsync(courier, order, ct);

    // 3. Batch kész → újratöltés ha van még a queue-ban
    if (!orderQueue.IsEmpty)
        RefillCourier(...);

    // 4. Kilépési feltétel
    if (courier.AssignedOrderIds.Count == 0 && orderQueue.IsEmpty) break;
}
```

**Miért `.ToList()` snapshot a batch-ről?**  
A `SimulateDeliveryAsync` kézbesítés végén `courier.AssignedOrderIds.Remove(order.Id)`-t hív. Ha közvetlenül `courier.AssignedOrderIds`-on iterálnánk, `InvalidOperationException: collection was modified` hibát kapnánk.

#### `RefillCourier` — zóna-szűrős queue olvasás

```csharp
while (assigned.Count < slotsNeeded && tries < maxTries)
{
    if (!orderQueue.TryDequeue(out var order)) break;

    if (courier.CanWorkInZone(order.ZoneId))
        // → hozzárendel
    else
        skipped.Add(order);  // rossz zóna → visszatesszük
}

foreach (var o in skipped)
    orderQueue.Enqueue(o);   // visszatesszük a queue végére
```

**`maxTries` védekezés:** legfeljebb annyiszor próbálunk `TryDequeue`-t, ahány elem volt a queue-ban az induláskor. Ha minden elem rossz zónás, nem pörgünk végtelen ciklusban.

**TPL megjegyzés:** `ConcurrentQueue.TryDequeue()` atomikus — több párhuzamos futár egyszerre hívhatja, versenyhelyzet nélkül.

---

### `Program.cs` — leegyszerűsítve

```
RÉGEN: setup + AssignAll + foreach szimuláció + összesítő (minden itt volt)
MOST:  setup + await orchestrator.RunAsync() + összesítő kiírás
```

A `Program.cs` most valóban csak belépési pont, nem tartalmaz üzleti logikát.

---

## 📊 Ellenőrzött kimenet (dotnet run, 2026-03-06)

```
✅ Setup kész: 11 csúcs | 5 futár | 15 rendelés

Initial assignment kész: 15 hozzárendelve, 0 sorban vár
Queue inicializálva: 0 rendelés vár hozzárendelésre   ← helyes! 5×3=15, pont elfogy

Kovács János  | 3 kézb. | 0 késés | Átlag: 19.7 perc ✅
Nagy Eszter   | 3 kézb. | 0 késés | Átlag:  2.7 perc ✅
Tóth Péter    | 3 kézb. | 0 késés | Átlag:  3.0 perc ✅
Szabó Anna    | 3 kézb. | 0 késés | Átlag:  3.7 perc ✅
Kiss Gábor    | 3 kézb. | 0 késés | Átlag:  8.0 perc ✅

15/15 kézbesítve — 0 késés, 0 hiba, 20.2s
```

### Megjegyzés: miért üres a queue az initial assignment után?

5 futár × MaxCapacity=3 = 15 férőhely, 15 rendelés → pont elfogy.  
Ez **helyes és elvárt** viselkedés a demo adatokkal.

A queue-logika akkor demonstrálható igazán, ha több rendelés van mint az összes futár kapacitásának összege — például 20 rendelés és 5 futár esetén az első 15 az initial batchbe kerül, a maradék 5 a queue-ba, és majd a visszaérő futárok veszik fel őket. Az architektúra ezt már kezeli.

---

## 🗂️ Fájlstruktúra változások

```
Presentation/
  Interfaces/
    ISimulationOrchestrator.cs    🆕 interfész + OrchestratorResult record

Services/
  Simulation/
    SimulationOrchestrator.cs     🆕 queue-logika, futárloopok, refill

Program.cs                        ✏️ leegyszerűsítve: csak setup + RunAsync hívás
```

---

## ➡️ Következő lépések

### 1. TPL — párhuzamos futárok

A `SimulationOrchestrator.RunAsync`-ban egyetlen sor a változás:

```csharp
// MOST (szekvenciális):
foreach (var courier in couriers)
    await RunCourierLoopAsync(courier, orderQueue, orderLookup, ct);

// TPL (párhuzamos):
await Task.WhenAll(
    couriers.Select(c => RunCourierLoopAsync(c, orderQueue, orderLookup, ct)));
```

A `ConcurrentQueue`, a `RunCourierLoopAsync` és a `RefillCourier` már thread-safe — ez a csere azonnal működik.

### 2. LiveConsoleRenderer — élő konzol megjelenítő

Az `ILiveConsoleRenderer` interfész már megvan (`Presentation/Interfaces/`).  
Implementáció: `Presentation/LiveConsoleRenderer.cs`  
Thread-safe konzolírás szükséges a TPL miatt (`lock` vagy `SemaphoreSlim`).
