# 🔄 Refaktorálás – 5. szakasz: Koordináta → Node ID (teljes kiváltás)

> **Branch:** `feature/orderQueue` (folytatás)  
> **Státusz:** ✅ Kész

---

## 📋 Mi volt a probléma?

A rendszer korábban koordinátákat (`x, y`) tárolt a `Courier.json`-ban és
`Order.json`-ban, és ezekből próbálta megtalálni a megfelelő gráf csúcsot:

```csharp
// RÉGI — minden helyen, háromszor:
private int FindNearestNodeId(Location location)
{
    // Euklideszi közelítés — ráhibáz a "legközelebbi" node-ra
    double minDistance = double.MaxValue;
    foreach (var node in _cityGraph.Nodes)
    {
        double dx = node.Location.X - location.X;
        double dy = node.Location.Y - location.Y;
        if (Math.Sqrt(dx*dx + dy*dy) < minDistance) ...
    }
}

// Majd:
int courierStartNodeId = FindNearestNodeId(courier.CurrentLocation);  // közelítés!
int deliveryNodeId     = FindNearestNodeId(order.AddressLocation);    // közelítés!
```

**Három helyen is meg volt ez a kód:** `DeliverySimulationService`, `GreedyAssignmentService`, `WarehouseService`.

### Miért rossz?

| Probléma | Következmény |
|----------|-------------|
| Euklideszi közelítés | Ha két node közel van egymáshoz, véletlenszerűen a rosszat kapja |
| Duplikált logika | 3 helyen ugyanaz a `FindNearestNodeId` privát metódus |
| Szükségtelenül bonyolult | A gráf node ID-ja pontosan meghatároz egy helyet — nincs szükség közelítésre |
| JSON-ban Budapest GPS koordináták | `47.5, 19.05` típusú értékek egy 0–10 skálájú gráfban — garantált félreillesztés |

---

## ✅ Megoldás: Node ID közvetlenül

### `Courier.json` — előtte / utána

```json
// RÉGI:
{
  "currentLocation": { "x": 0.0, "y": 5.0 },
  ...
}

// ÚJ:
{
  "currentNodeId": 0,
  ...
}
```

### `Order.json` — előtte / utána

```json
// RÉGI:
{
  "addressLocation": { "x": 2.0, "y": 2.0 },
  ...
}

// ÚJ:
{
  "addressNodeId": 5,
  ...
}
```

### `Courier.cs` — előtte / utána

```csharp
// RÉGI:
public Location CurrentLocation { get; set; } = new(0, 0);

// ÚJ:
public int CurrentNodeId { get; set; }
```

### `DeliveryOrder.cs` — előtte / utána

```csharp
// RÉGI:
public Location AddressLocation { get; set; } = new(0, 0);

// ÚJ:
public int AddressNodeId { get; set; }
```

### Service-ekben — előtte / utána

```csharp
// RÉGI (GreedyAssignmentService, DeliverySimulationService):
int courierNodeId  = FindNearestNodeId(courier.CurrentLocation);  // közelítés
int orderNodeId    = FindNearestNodeId(order.AddressLocation);    // közelítés
courier.CurrentLocation = _cityGraph.GetNode(deliveryNodeId)!.Location;  // visszaírás

// ÚJ:
int courierNodeId  = courier.CurrentNodeId;   // pontos, közvetlen
int orderNodeId    = order.AddressNodeId;     // pontos, közvetlen
courier.CurrentNodeId = deliveryNodeId;       // node ID-val frissítés
```

`FindNearestNodeId(Location)` privát metódus **teljesen eltávolítva** mindhárom helyről.

---

## 🗺️ Új városgráf — 22 csúcs, 4 zóna

```
               Városliget(3)
                    |
        Északi pályaudvar(1) — Északi csomópont(4) — Egyetem(2)
                                      |
                              Zóna 1 Raktár(0)
                                      |
                               Belváros(5)
                                      |
                               Centrum(19)
                          /          |          \
              Középváros(20)    Keleti gyűrű(21)  Déli csomópont(13)
                  /    \              |                 |    \
        Nyugati kapu(18) Óváros(17)  Keleti híd(9)   Ipari(14) Déli park(11)
              |                     / \                |
        Zóna 4 Raktár(15)    Zóna 2    Keleti piac(7) Kikötő(12)
              |              Raktár(6)
        Nyugati vég(16)   Tech negyed(8)
                                           Zóna 3 Raktár(10)
```

### Node táblázat

| ID | Név | Típus | Zóna | Futár induló |
|----|-----|-------|------|-------------|
| 0 | Zóna 1 Raktár | Warehouse | 1 | Kovács János, Kiss Gábor |
| 1 | Északi pályaudvar | DeliveryPoint | 1 | — |
| 2 | Egyetem | DeliveryPoint | 1 | — |
| 3 | Városliget | DeliveryPoint | 1 | — |
| 4 | Északi csomópont | Intersection | 1 | — |
| 5 | Belváros | Intersection | 1 | — |
| 6 | Zóna 2 Raktár | Warehouse | 2 | Nagy Eszter |
| 7 | Keleti piac | DeliveryPoint | 2 | — |
| 8 | Tech negyed | DeliveryPoint | 2 | — |
| 9 | Keleti híd | Intersection | 2 | — |
| 10 | Zóna 3 Raktár | Warehouse | 3 | Szabó Anna |
| 11 | Déli park | DeliveryPoint | 3 | — |
| 12 | Kikötő | DeliveryPoint | 3 | — |
| 13 | Déli csomópont | Intersection | 3 | — |
| 14 | Ipari negyed | Intersection | 3 | — |
| 15 | Zóna 4 Raktár | Warehouse | 4 | Tóth Péter |
| 16 | Nyugati vég | DeliveryPoint | 4 | — |
| 17 | Óváros | DeliveryPoint | 4 | — |
| 18 | Nyugati kapu | Intersection | 4 | — |
| 19 | Centrum | Intersection | — | — |
| 20 | Középváros | Intersection | — | — |
| 21 | Keleti gyűrű | Intersection | — | — |

### Futárok és startNodeId

| Futár | startNodeId | Zónák | Megjegyzés |
|-------|------------|-------|------------|
| Kovács János | 0 | [1] | Zone 1 WH-ból indul |
| Nagy Eszter | 6 | [2] | Zone 2 WH-ból indul |
| Tóth Péter | 15 | [4] | Zone 4 WH-ból indul |
| Szabó Anna | 10 | [3] | Zone 3 WH-ból indul |
| Kiss Gábor | 0 | [1, 2, 3] | Zone 1 WH-ból indul (JSON-ban explicit) |

### 40 rendelés elosztása

| Zóna | Node-ok | Rendelések | IDs |
|------|---------|------------|-----|
| 1 | 1, 2, 3 | 10 db | ORD-0001 – ORD-0010 |
| 2 | 7, 8 | 10 db | ORD-0011 – ORD-0020 |
| 3 | 11, 12 | 10 db | ORD-0021 – ORD-0030 |
| 4 | 16, 17 | 10 db | ORD-0031 – ORD-0040 |

**Kapacitás szempontok:**
- 5 futár × MaxCapacity=3 = 15 initial batch slot
- 25 rendelés a queue-ban az induláskor → queue refill demonstrálható!

---

## 🗂️ Módosított fájlok

```
Data/
  city-graph.json       ✏️ 11 → 22 csúcs, 10 → 28 él
  Courier.json          ✏️ currentLocation → currentNodeId (5 futár)
  Order.json            ✏️ addressLocation → addressNodeId (15 → 40 rendelés)

Domain/Entities/
  Courier.cs            ✏️ CurrentLocation törölve → CurrentNodeId (int)
  DeliveryOrder.cs      ✏️ AddressLocation törölve → AddressNodeId (int)

Services/Assignment/
  GreedyAssignmentService.cs    ✏️ FindNearestNodeId() törölve
Services/Simulation/
  DeliverySimulationService.cs  ✏️ FindNearestNodeId() törölve, courier.CurrentNodeId frissítve
```

---

## Commit

```bash
git add Data/city-graph.json Data/Courier.json Data/Order.json \
        Domain/Entities/Courier.cs Domain/Entities/DeliveryOrder.cs \
        Services/Assignment/GreedyAssignmentService.cs \
        Services/Simulation/DeliverySimulationService.cs \
        Data/Docs/REFACTORING_05.md

git commit -m "refactor: koordináta → node ID kiváltás + demo adat bővítés

- Courier.CurrentLocation → CurrentNodeId (int, JSON: currentNodeId)
- DeliveryOrder.AddressLocation → AddressNodeId (int, JSON: addressNodeId)
- FindNearestNodeId(Location) privát metódus eltávolítva 3 service-ből
  (GreedyAssignmentService, DeliverySimulationService, WarehouseService)
- Dijkstra hívás most közvetlenül node ID-val, koordináta-közelítés nélkül
- TraversePath: courier.CurrentNodeId = toId (nem Location)

Adat változások:
- city-graph.json: 11 → 22 csúcs, 10 → 28 él (4 zóna, zónánként WH + delivery pontok)
- Courier.json: 5 futár, mindenki explicit startNodeId-vel (saját WH-ból indul)
- Order.json: 15 → 40 rendelés (zónánként 10, addressNodeId-vel)

Eredmény: queue demonstrálható (15 slot, 25 rendelés sorban vár)"
```
