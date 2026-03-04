# 🔄 Refaktorálás – 3. szakasz: Több warehouse + zóna-alapú logika

> **Dátum:** 2026. március  
> **Státusz:** ✅ Kész — lefordul és fut, 15/15 rendelés kézbesítve

---

## 📋 Mit csináltunk ebben a szakaszban?

### 1. `city-graph.json` — 3 új Warehouse node

A gráf 8 csúcsról 11 csúcsra bővült. Minden zónának most saját warehouse-a van:

| Node ID | Név | Típus | Zóna | Koordináta |
|---------|-----|-------|------|------------|
| 0 | Zone 1 Warehouse | Warehouse | 1 | (0, 0) — megvolt |
| 8 | Zone 2 Warehouse | Warehouse | 2 | (5, 3) — ÚJ |
| 9 | Zone 3 Warehouse | Warehouse | 3 | (2, -5) — ÚJ |
| 10 | Zone 4 Warehouse | Warehouse | 4 | (-5, -3) — ÚJ |

Új élek (mind 3-4 perces gyors összeköttetések a szomszédos kereszteződésekhez):
```
Zone 2 Warehouse (8) ↔ Downtown (5)     4 perc
Zone 2 Warehouse (8) ↔ East Side (2)    3 perc
Zone 3 Warehouse (9) ↔ Industrial (7)   3 perc
Zone 3 Warehouse (9) ↔ South Park (3)   4 perc
Zone 4 Warehouse (10) ↔ Suburb (6)      4 perc
Zone 4 Warehouse (10) ↔ West End (4)    3 perc
```

### 2. `Courier.cs` — kapacitás kezelés hozzáadva

Új property-k:

```csharp
public int MaxCapacity { get; set; } = 3;   // max egyszerre vihető rendelés
public bool HasCapacity => AssignedOrderIds.Count < MaxCapacity;
public int RemainingCapacity => MaxCapacity - AssignedOrderIds.Count;
```

`MaxCapacity` JSON-ból felülírható (ha nincs megadva, 3 az alapértelmezett).

### 3. `GreedyAssignmentService` — zóna-szűrés + kapacitás

**`AssignToNearest` — két új szűrési feltétel:**

```
Korábban: csak Status == Available
Most:     Status != OffDuty
          ÉS HasCapacity (AssignedOrderIds.Count < MaxCapacity)
          ÉS CanWorkInZone(order.ZoneId)
```

Ez azt jelenti:
- Egy Busy futár is kaphat rendelést, ha még nincs tele (pl. 2/3)
- Egy futár csak a saját zónájába eső rendelést kaphat
- Ha ugyanabban a zónában több futár is van → Dijkstra dönt (aki közelebb van)

**`AssignNextBatch` (ÚJ metódus):**
Akkor hívandó, amikor egy futár visszaér a warehouse-ba és új rendeléseket vesz fel.
Feltölti a szabad helyeket (`RemainingCapacity`) a saját zónájában várakozó
Pending rendelésekkel. A szimulációs logikában lesz majd bekötve.

### 4. `DeliverySimulationService` — warehouse-keresés javítva

**A bug:** A régi kód `FindNearestWarehouse(order.AddressLocation)` hívással
kereste a warehouse-t — koordináta-alapon, ami mindig Node 8-at (Zone 2 Warehouse)
adta vissza, mert az összes rendelés koordinátája véletlenül közel esett hozzá.

**A javítás:** Most a futár saját zónáit nézzük:

```
1. Lekérjük az összes warehouse-t
2. Szűrjük azokat, amelyek a FUTÁR zónáiban vannak
   (courier.AssignedZoneIds.Contains(warehouse.ZoneId))
3. Ezek közül Dijkstrával a futárhoz legközelebbit választjuk
4. Fallback: ha a futár zónájában nincs warehouse → legközelebbi bármely zónából
```

---

## 🐛 Megoldott hibák

| Hiba | Ok | Megoldás |
|------|----|----------|
| Minden csomag Node 8-ból (Zone 2 WH) jött fel | `FindNearestWarehouse` koordináta-alapon keresett, és az összes koordináta Node 8 közelébe esett | Warehouse-keresés átírva: futár zónája alapján szűr, majd Dijkstra választ |

---

## ⚠️ Ismert, még nem javított jelenség

**"0 perc" minden kézbesítésnél** — ez NEM hiba a logikában, hanem adatprobléma:

A `Courier.json`-ban lévő koordináták (`47.5, 19.05` stb.) Budapest GPS koordinátái,
de a gráf koordinátái kis számok (`0.0, 5.0` stb.).

Mivel `FindNearestNodeId` Euklideszi távolságot számol, az összes futár és rendelés
ugyanarra a node-ra (Node 2, East Side) képeződik le → Dijkstra 0 lépést mér.

**Megoldás (következő lépésben):** A `Courier.json`-ban és `Order.json`-ban lévő
koordinátákat össze kell hangolni a gráf koordinátáival, VAGY a futárok
kiindulópontját node ID-val kell megadni a JSON-ban.

---

## 📊 Jelenlegi működés

```
4 warehouse megtalálva (zónánként 1)
→ 15 rendelés betöltve
→ 15/15 hozzárendelve (zóna-szűrés + kapacitás, max 3/futár)
→ 15 szimuláció lefut egymás után
→ Helyes warehouse-ból veszi fel a csomagot (futár zónája alapján)
→ 0 perc eredmény (adatprobléma, koordináta-ütközés — következő lépésben javítandó)
```

### Hozzárendelés eredménye (helyes zóna-elosztás):
```
Kovács János  [Zóna 1,2]: ORD-0001 (Z1), ORD-0002 (Z2), ORD-0003 (Z2)
Nagy Eszter   [Zóna 2,3]: ORD-0004 (Z3), ORD-0005 (Z3), ORD-0010 (Z2)
Tóth Péter    [Zóna 1,4]: ORD-0006 (Z4), ORD-0007 (Z4), ORD-0008 (Z1)
Kiss Gábor    [Zóna 1,2,3]: ORD-0009 (Z1), ORD-0013 (Z1), ORD-0014 (Z2)
Szabó Anna    [Zóna 3,4]: ORD-0011 (Z3), ORD-0012 (Z4), ORD-0015 (Z3)
```

---

## 🗂️ Fájlstruktúra változások

```
Data/
  city-graph.json              ✏️ 8 → 11 csúcs, 10 → 16 él

Domain/Entities/
  Courier.cs                   ✏️ MaxCapacity, HasCapacity, RemainingCapacity hozzáadva

Services/Assignment/
  GreedyAssignmentService.cs   ✏️ zóna-szűrés, kapacitás-ellenőrzés, AssignNextBatch

Services/Simulation/
  DeliverySimulationService.cs ✏️ warehouse-keresés javítva (futár zónája alapján)
```

---

## ➡️ Következő lépések

### 1. Koordináta-összehangolás (adatjavítás)
A futárok és rendelések koordinátáit össze kell hangolni a gráf koordinátáival,
hogy a Dijkstra valódi időket mérjen (ne 0 percet).

**Két lehetőség:**
- A `Courier.json`-ban és `Order.json`-ban gráf-koordinátákat használunk (pl. `x: 2.0, y: -5.0`)
- VAGY a futárok kiindulópontját node ID-val adjuk meg a JSON-ban (`"startNodeId": 2`)

### 2. Queue-alapú szimuláció (`DeliverySimulationService` átírás)
A jelenlegi szimuláció még mindig egymás után fut (szekvenciális).
A következő nagy lépés: queue-alapú logika, ahol a futár visszatér,
`AssignNextBatch`-t hív, és folytatja — amíg van várakozó rendelés.

### 3. TPL — párhuzamos futárok (`Task.WhenAll`)

### 4. `LiveConsoleRenderer` implementálása

### 5. Riportok (`Reporting/` mappa feltöltése)


# REFACTORING_04 — Időszámítás és warehouse cache javítás

**Branch:** `refactor`
**Érintett fájl:** `Services/Simulation/DeliverySimulationService.cs`

---

## Háttér

Az előző szekcióban (`REFACTORING_03`) sikeresen implementáltuk a zóna-alapú
warehouse-választást. A tesztelés során azonban a konzolkimenetben furcsa
időértékek jelentek meg:

```
✅ Időben kézbesítve | Tényleges: 24 perc | Ideális: 12 perc
✅ Időben kézbesítve | Tényleges: 19 perc | Ideális: 7 perc
```

Az ideális idő következetesen a fele volt a ténylegesnek. A vizsgálat 3 egymással
összefüggő bugot tárt fel.

---

## Bug #1 — Cache poisoning (`order.NearestWarehouseNodeId`)

### Mi volt a probléma?

A `SimulateDeliveryAsync` metódus az alábbi logikával kereste meg a warehouse-t:

```csharp
if (order.NearestWarehouseNodeId.HasValue)
{
    warehouseNodeId = order.NearestWarehouseNodeId.Value;  // ← ez futott le MINDIG
}
else
{
    // zóna-alapú logika — SOHA nem futott le
    var courierZoneWarehouses = allWarehouses
        .Where(w => courier.AssignedZoneIds.Contains(w.ZoneId.Value))
        ...
    order.NearestWarehouseNodeId = warehouseNodeId;  // ← beállította, és ezzel "zárolt"
}
```

**Következmény:** Az `order` objektum az első szimuláció után megkapta a
`NearestWarehouseNodeId` értéket (pl. Node 8). Minden további futtatásnál az
`if` ág futott le, a zóna-alapú számítás kikerülésével — azaz az összes futár
ugyanarról a warehouse-ról indult, zónától függetlenül.

### Javítás

A cache-elés teljesen eltávolítva. A warehouse mindig frissen, a futár
`AssignedZoneIds` alapján kerül meghatározásra:

```csharp
// MINDIG zóna alapján, soha nem cache-ből
var courierZoneWarehouses = allWarehouses
    .Where(w => w.ZoneId.HasValue && courier.AssignedZoneIds.Contains(w.ZoneId.Value))
    .ToList();

GraphNode? bestWarehouse = null;
int shortestWhTime = int.MaxValue;

foreach (var wh in courierZoneWarehouses)
{
    var (_, whTime) = _cityGraph.FindShortestPath(courierStartNodeId, wh.Id);
    if (whTime < shortestWhTime) { shortestWhTime = whTime; bestWarehouse = wh; }
}
// Fallback ha nincs zónás warehouse: FindNearestWarehouseFromNode(...)
```

---

## Bug #2 — `idealTime` inkonzisztens definíció

### Mi volt a probléma?

```csharp
// ELŐTTE: csak warehouse → cím
int idealTime = _cityGraph.CalculateIdealTime(warehouseNodeId, deliveryNodeId);
```

Az `idealTime` kizárólag a `warehouse → kézbesítési cím` szakaszt mérte,
de a `totalActualTime` tartalmazta a `futár → warehouse` menetidőt is.

**Következmény:** Pl. ha a futár 12 percet ment a warehouse-ig, majd 12 percet
a célhoz, a kimenet ezt mutatta:

```
Tényleges: 24 perc | Ideális: 12 perc   ← látszólag 100%-os késés!
```

### Javítás

Az `idealTime` a **teljes utat** méri, forgalom nélkül:

```csharp
// UTÁNA: futár → raktár → kézbesítési cím
int idealWarehouseTime = _cityGraph.CalculateIdealTime(courierStartNodeId, warehouseNodeId);
int idealDeliveryTime  = _cityGraph.CalculateIdealTime(warehouseNodeId, deliveryNodeId);
int idealTime = idealWarehouseTime + idealDeliveryTime;
```

A logban is megjelenik a bontás:
```
⏱️  Ideális kézbesítési idő (forgalom nélkül): 19 perc (raktárhoz: 12 + kézbesítés: 7)
```

---

## Bug #3 — Késés-detektálás rossz változón futott

### Mi volt a probléma?

```csharp
// ELŐTTE: deliveryTime (csak warehouse→cím rész)
bool wasDelayed = deliveryTime > idealTime * DelayThreshold;
int delayMinutes = deliveryTime - idealTime;
```

A `wasDelayed` a `deliveryTime`-ot (warehouse→cím) hasonlította az `idealTime`-hoz
(teljes út) — inkonzisztens mértékegységek. A logban mégis `totalActualTime`
jelent meg, ami tovább növelte a zavart.

### Javítás

```csharp
// UTÁNA: mindkét oldal a teljes utat méri
bool wasDelayed = totalActualTime > idealTime * DelayThreshold;
int delayMinutes = totalActualTime - idealTime;

_logger.LogInformation(
    "🟢 Időben kézbesítve! {Time} perc (ideális: {Ideal} perc)",
    totalActualTime, idealTime);  // ← korábban deliveryTime volt
```

---

## Megjegyzés: Tényleges < Ideális esetek

A javítás után néhány rendelésnél a tényleges idő **kisebb** az ideálisnál:

```
Tényleges: 2 perc | Ideális: 3 perc
```

Ez **nem bug**, hanem helyes viselkedés. Magyarázat:

- `idealTime` = `CalculateIdealTime()` → az **alap él-súlyokat** használja
  (forgalom nélkül, `TrafficMultiplier = 1.0`)
- `totalActualTime` = `TraversePath()` → az `edge.CurrentTimeMinutes`-t
  használja, ami a **valós idejű forgalomtól függ**
- Ha az előző szimulált utak forgalma **lecsökkentette** egy él
  `TrafficMultiplier`-jét (pl. `0.8x`), a futár gyorsabb lesz az ideálisnál

A forgalom szimulációja tehát kétirányú: lassíthat is és gyorsíthat is.

---

## Összefoglalás

| Bug | Érintett sor | Javítás |
|-----|-------------|---------|
| Cache poisoning | `order.NearestWarehouseNodeId` cache-elés | Cache eltávolítva, mindig zóna alapján számít |
| idealTime definíció | Csak `warehouse→cím` | Teljes `futár→warehouse→cím` |
| Késés-detektálás | `deliveryTime` vs `idealTime` | `totalActualTime` vs `idealTime` |

### Ellenőrzött kimenet (dotnet run, 2026-03-04 22:22)

```
Kovács János  | Node 0  (Zóna 1 WH) | 3 kézb. | 0 késés | Átlag: 20.7 perc ✅
Nagy Eszter   | Node 8  (Zóna 2 WH) | 3 kézb. | 0 késés | Átlag:  2.7 perc ✅
Tóth Péter    | Node 10 (Zóna 4 WH) | 3 kézb. | 0 késés | Átlag:  2.7 perc ✅
Szabó Anna    | Node 9  (Zóna 3 WH) | 3 kézb. | 0 késés | Átlag:  3.7 perc ✅
Kiss Gábor    | Node 8  (Zóna 2 WH) | 3 kézb. | 0 késés | Átlag:  8.0 perc ✅
```

**15/15 rendelés sikeresen kézbesítve — 0 késés, 0 hiba.**

#### Megjegyzések a kimenethez

**ORD-0011 (Szabó Anna → Bodnár Tamás): Tényleges: 0 perc | Ideális: 0 perc**
Szabó Anna az előző kézbesítés után éppen a raktár és a cél node-jával egyező
pozícióban volt. Dijkstra szerint az út hossza 0 — ez nem bug, hanem a gráf
topológiájának természetes következménye.

**ORD-0009 (Kiss Gábor → Németh Dániel): Node 8 warehouse (nem Node 0)**
Kiss Gábor Zóna 1, 2, 3-as futár. Habár a Zone 1 WH (Node 0) is elérhető
lett volna, a Dijkstra a futár aktuális pozíciójából Node 8-at (Zone 2 WH)
találta közelebb. Ez a helyes és elvárt viselkedés — a warehouse-választás
mindig futárpozíció-alapú, nem zóna-ID prioritás alapján.

---

## Commit

```bash
git add Services/Simulation/DeliverySimulationService.cs
git commit -m "fix: időszámítás javítás és warehouse cache eltávolítás

- idealTime: warehouse→cím helyett teljes futár→warehouse→cím út
  (CalculateIdealTime kétszer hívva és összeadva)
- wasDelayed: deliveryTime helyett totalActualTime az összehasonlítás alapja
- NearestWarehouseNodeId cache-elés eltávolítva — minden szimulációnál
  frissen számítjuk zóna alapján (különböző futárok más zónán dolgoznak)
- Fallback megmarad: ha futár zónájában nincs warehouse → legközelebbi

Teszteredmény: 15/15 kézbesítve, 0 késés, 0 hiba"

git push origin refactor
```
