# 🗺️ Infrastructure/Graph — Részletes Architektúra Dokumentáció

> **Mappa:** `Infrastructure/Graph/`  
> **Utoljára frissítve:** 2026. március

---

## 📋 Tartalom

1. [Áttekintés — mi ez az egész?](#áttekintés)
2. [Fájlstruktúra és felelősségek](#fájlstruktúra)
3. [ICityGraph.cs — Az interfész](#icityGraphcs)
4. [CityGraphCore.cs — Alap adatstruktúrák](#citygraphcorecs)
5. [CityGraphPathfinding.cs — Dijkstra algoritmus](#citygraphpathfindingcs)
6. [CityGraphTraffic.cs — Forgalomkezelés](#citygraphtrafficcs)
7. [CityGraphDebug.cs — Konzol kiíratok](#citygraphdebugcs)
8. [Hogyan illeszkednek össze? (partial class)](#partial-class)
9. [Forgalom és ideális idő — hogyan hasonlítunk?](#forgalom-és-ideális-idő)
10. [Ismert hiányosságok és TODO-k](#ismert-hiányosságok)

---

## Áttekintés

Ez a mappa tartalmazza a szimuláció **szívét**: a városgráfot és minden kapcsolódó logikát.

A városgráf egy **súlyozott, irányítatlan gráf**, ahol:
- **Csúcsok (nodes)** = helyek a városban (raktár, kézbesítési cím, kereszteződés)
- **Élek (edges)** = utak a helyek között, amelyeknek **utazási idő** a súlya
- **Forgalom** = az élek súlyát dinamikusan módosító szorzó

```
[Raktár] ──5 perc──► [Downtown] ──8 perc──► [North District]
                          │
                       7 perc
                          │
                          ▼
                      [East Side]
```

A gráf **nem koordinátaalapú** navigációt végez — az útvonalakat az élek mentén
Dijkstra algoritmussal számítja ki, nem légvonalbeli távolsággal.

---

## Fájlstruktúra

```
Infrastructure/Graph/
├── ICityGraph.cs              ← Interfész (mit tud a gráf?)
├── CityGraphCore.cs           ← Adatstruktúrák, csúcs/él kezelés
├── CityGraphPathfinding.cs    ← Dijkstra + ideális idő számítás
├── CityGraphTraffic.cs        ← Forgalomváltozás szimuláció
└── CityGraphDebug.cs          ← Konzol kiíró metódusok
```

A négy `CityGraph*.cs` fájl egyetlen osztályt alkot — `partial class` technikával
van szétbontva. Erről bővebben a [partial class](#partial-class) szekcióban.

---

## ICityGraph.cs

**Szerepe:** Meghatározza, mit "tud csinálni" egy városgráf — anélkül, hogy a
konkrét implementációhoz kötné a többi service-t.

### Miért kell ez?

A `DeliverySimulationService`, `WarehouseService`, `GreedyAssignmentService` mind
`ICityGraph`-ra hivatkoznak, nem `CityGraph`-ra. Ez azt jelenti:

- **Teszteléskor** behelyettesíthető egy `MockCityGraph`-fal
- **Fejlesztéskor** a konkrét implementáció cserélhető, ha megfelel az interfésznek
- A service-ek nem "tudják" és nem is kell tudják, hogyan van a gráf megvalósítva

### Csoportok az interfészben

| Csoport | Metódusok | Mire való? |
|---------|-----------|------------|
| Query | `Nodes`, `GetNode()`, `FindNodeByName()`, `GetEdge()`, `GetNeighbors()` | Adatok olvasása |
| Pathfinding | `FindShortestPath()`, `CalculateIdealTime()` | Útvonalkeresés |
| Traffic | `UpdateTrafficConditions()`, `RegisterCourierMovement()` | Forgalomkezelés |
| Debug | `PrintGraph()`, `PrintPath()` | Fejlesztői eszközök |

---

## CityGraphCore.cs

**Szerepe:** A gráf alapjainak tárolása — csúcsok, élek, szomszédossági mátrix.

### Adatstruktúrák

```csharp
private readonly List<GraphNode> _nodes;
private readonly EdgeWeight[,] _adjacencyMatrix;
private readonly int _nodeCount;
private readonly Random _random;
```

#### `_nodes` — Csúcslista

Egyszerű `List<GraphNode>`. A csúcs **ID-ja megegyezik a lista indexével** — ez
egy fontos megkötés, amit az `AddNode()` érvényre juttat:

```csharp
if (node.Id != _nodes.Count)
    throw new ArgumentException($"Node ID must be {_nodes.Count}, but got {node.Id}");
```

Ha pl. a 0. és 2. csúcsot adjuk hozzá, de kihagyjuk az 1-est, az kivételt dob.
Ez egyszerűsíti a mátrixindexelést: `_nodes[3]` mindig a 3-as ID-jú csúcs.

#### `_adjacencyMatrix` — Szomszédossági mátrix

Egy 2D tömb, ahol `_adjacencyMatrix[i, j]` az `i` és `j` csúcs közötti él súlya
(`EdgeWeight` objektum), vagy `null` ha nincs közvetlen él.

**Irányítatlan gráf:** `_adjacencyMatrix[i, j]` és `_adjacencyMatrix[j, i]`
**ugyanaz az objektum**. Ha a forgalmat módosítjuk, mindkét irányban hat.

```
       0    1    2    3
  0  [null][null][EW1][null]
  1  [null][null][null][EW2]
  2  [EW1] [null][null][EW3]   ← EW1 az i=0,j=2 és i=2,j=0 cellában ugyanaz!
  3  [null][EW2] [EW3] [null]
```

**Mért nem `Dictionary<(int,int), EdgeWeight>`?**  
A mátrix `O(1)` hozzáférést biztosít — a Dijkstra `n²` lépésben fut, minden
lépésnél él-lekérdezéssel, ezért fontos a sebesség.

### Főbb metódusok

#### `AddNode(GraphNode node)`

Ellenőrzi, hogy a csúcs ID folytatólagos-e, majd hozzáadja a listához.

#### `AddEdge(int nodeId1, int nodeId2, int idealTimeMinutes)`

Létrehoz egy `EdgeWeight` objektumot és **mindkét irányba** berakja a mátrixba:

```csharp
var edgeWeight = new EdgeWeight(idealTimeMinutes);
_adjacencyMatrix[nodeId1, nodeId2] = edgeWeight;
_adjacencyMatrix[nodeId2, nodeId1] = edgeWeight;  // ← UGYANAZ az objektum!
```

#### `GetNeighbors(int nodeId)`

Végigmegy a mátrix adott során, és visszaadja azokat az indexeket, ahol nem `null`
az érték. Ez `O(n)` — a teljes sort bejárja.

---

## CityGraphPathfinding.cs

**Szerepe:** Legrövidebb útvonal keresése és ideális idő számítása.

### `FindShortestPath(int startNodeId, int endNodeId)`

**Dijkstra algoritmus** — a **jelenlegi forgalommal** számol (`CurrentTimeMinutes`).

#### Lépések

```
1. Inicializálás:
   distances[] = [∞, ∞, ∞, ∞, ...]   ← minden csúcs végtelen messze
   distances[start] = 0               ← kivéve a kezdőpontot
   visited[] = [false, false, ...]    ← senki sem látogatott

2. Fő ciklus (n-1-szer ismétlődik):
   a) Megkeresi a legközelebbi NEM látogatott csúcsot
      → ahol distances[i] a legkisebb és visited[i] == false
   b) Megjelöli látogatottnak (visited[i] = true)
   c) Frissíti a szomszédok távolságát:
      ha distances[i] + edge.CurrentTimeMinutes < distances[szomszéd]:
          distances[szomszéd] = distances[i] + edge.CurrentTimeMinutes
          previous[szomszéd] = i

3. Útvonal visszaépítése:
   Visszafelé követjük a previous[] tömböt:
   cél → előző → előző előző → ... → start
   Majd megfordítjuk → start → ... → cél
```

#### Visszatérési érték

```csharp
(List<int> Path, int TotalTime)
```

- `Path` = csúcs ID-k listája a teljes útvonalhoz, pl. `[0, 5, 2]`
- `TotalTime` = az útvonal összes élének `CurrentTimeMinutes` összege

Ha nincs út: `(üres lista, int.MaxValue)`

#### Fontos: `CurrentTimeMinutes` vs `IdealTimeMinutes`

A Dijkstra **mindig `CurrentTimeMinutes`-t** használ — vagyis a forgalommal
terhelt, aktuális időket. Ez azt jelenti, hogy forgalmas időben más útvonalat
választhat, mint normál körülmények között.

---

### `CalculateIdealTime(int startNodeId, int endNodeId)`

Kiszámítja, mennyi ideig tartana az út **forgalom nélkül**, `TrafficMultiplier = 1.0` esetén.

#### Hogyan működik — az "átmeneti visszaállítás" technika

A gráfnak **nincs külön "ideális" másolata**. Ehelyett:

```
1. Elmenti az összes él jelenlegi TrafficMultiplier értékét egy ideiglenes tömbbe
2. Minden élt visszaállít 1.0x-ra (ideális)
3. Lefuttatja a Dijkstrát → ez adja az idealTime-ot
4. Visszaállítja az eredeti TrafficMultiplier értékeket
5. Visszaadja az idealTime-ot
```

```csharp
// Mentés
var originalMultipliers = new double[n, n];
for (i...) originalMultipliers[i,j] = edge.TrafficMultiplier;

// Ideiglenes reset
edge.UpdateTraffic(1.0);

// Dijkstra ideális körülmények között
var (_, idealTime) = FindShortestPath(start, end);

// Visszaállítás
edge.UpdateTraffic(originalMultipliers[i,j]);
```

**Mellékhatás-veszély:** Ha `FindShortestPath` és `CalculateIdealTime` egyszerre
fut (párhuzamos futárok esetén), a visszaállítás előtt valamelyik futár rossz
(ideális) forgalmi adatokat láthat. A jelenlegi szekvenciális szimulációban ez
nem okoz gondot, de TPL bevezetésekor `lock` vagy másolat kell majd.

---

## CityGraphTraffic.cs

**Szerepe:** A forgalom dinamikus szimulálása — az élek `TrafficMultiplier`-jének változtatása.

### `UpdateTrafficConditions()`

Véletlenszerű ±10%-os változást alkalmaz **minden élen**:

```csharp
double change = (_random.NextDouble() - 0.5) * 0.2;
// NextDouble() → [0.0, 1.0)
// - 0.5        → [-0.5, 0.5)
// * 0.2        → [-0.1, 0.1)  ← pontosan ±10%

edge.UpdateTraffic(edge.TrafficMultiplier + change);
```

Az `EdgeWeight.UpdateTraffic()` belül korlátoz: minimum `0.5x`, maximum `2.5x`.

**Mikor hívódik meg?**
A `DeliverySimulationService.TraversePath()` hívja meg **minden egyes él
bejárásakor** — vagyis minden szimulált lépésnél.

**Mit jelent ez a gyakorlatban?**
Minden egyes node-átlépés után az egész város forgalma véletlenszerűen változik.
Ez nem teljesen realisztikus (miért változna pl. West End forgalma azért, mert
egy futár Downtown-ban jár?), de a jelenlegi szekvenciális szimulációban
elfogadható közelítés.

---

### `RegisterCourierMovement(int fromNodeId, int toNodeId)`

> ⚠️ **Ez a metódus JELENLEG ÜRES.**

```csharp
public void RegisterCourierMovement(int fromNodeId, int toNodeId)
{
    var edge = GetEdge(fromNodeId, toNodeId);
    if (edge != null)
    {
        // ← Nincs semmi! A forgalomfokozás nincs implementálva.
    }
}
```

Az eredeti szándék az volt, hogy a futármozgás növelje az adott él forgalmát
(pl. `+0.1x` minden futár áthaladáskor). Ez **nincs megvalósítva** — a futár
áthaladása jelenleg semmilyen forgalomváltozást nem okoz közvetlenül.

A forgalom kizárólag a `UpdateTrafficConditions()` véletlenszerű ±10%-án keresztül
változik.

**Ha implementálni szeretnénk:**
```csharp
public void RegisterCourierMovement(int fromNodeId, int toNodeId)
{
    var edge = GetEdge(fromNodeId, toNodeId);
    if (edge != null)
    {
        // Futár áthaladás → +5% forgalom ezen az élen
        edge.UpdateTraffic(edge.TrafficMultiplier + 0.05);
    }
}
```

---

### `ResetAllTraffic()`

Visszaállítja **minden él** `TrafficMultiplier`-jét `1.0`-ra. Csak a felső
háromszögön megy végig (irányítatlan gráf → `[i,j]` és `[j,i]` ugyanaz az objektum,
elég egyszer resetelni).

---

## CityGraphDebug.cs

**Szerepe:** Fejlesztői segédeszközök — konzolra kiírja a gráf állapotát.

### `PrintGraph()`

Kiírja az összes csúcsot és élt, az aktuális forgalommal együtt. Például:

```
════════════════════════════════════════════════════════════
                    CITY GRAPH
════════════════════════════════════════════════════════════
📍 Nodes: 11 / 22
🔗 Edges: 16

NODES:
────────────────────────────────────────────────────────────
  [ 0] Zone 1 Warehouse     (Warehouse      ) at (0.00, 0.00)  | Zone 1
  [ 1] North District       (DeliveryPoint  ) at (0.00, 5.00)  | Zone 1
  ...

EDGES:
────────────────────────────────────────────────────────────
  Zone 1 Warehouse     <--> Downtown            |  5 min (ideal:  5, traffic: 1.00x)
  Zone 1 Warehouse     <--> Industrial          |  7 min (ideal:  6, traffic: 1.12x)
  ...
```

**Mikor kell használni:** Csak fejlesztés közben, pl. a gráf betöltése után
az ellenőrzéshez. Az élő szimulációban nem hívjuk, mert lassú és zajos.

### `PrintPath(List<int> path, int totalTime)`

Kiírja egy konkrét útvonal csúcsait és az élek idejét:

```
🗺️  PATH (3 nodes, 12 min total):
──────────────────────────────────────────────────
  [0] Zone 1 Warehouse → (5 min)
  [5] Downtown → (7 min)
  [2] East Side ✓
──────────────────────────────────────────────────
```

---

## Partial Class

A `CityGraph` osztály **négy fájlra van szétbontva** a `partial class` kulcsszóval.
A fordító egyetlen osztállyá fűzi össze őket — futásidőben nincs különbség.

```csharp
// CityGraphCore.cs
public partial class CityGraph : ICityGraph { /* adatok, csúcsok, élek */ }

// CityGraphPathfinding.cs
public partial class CityGraph : ICityGraph { /* Dijkstra */ }

// CityGraphTraffic.cs
public partial class CityGraph : ICityGraph { /* forgalom */ }

// CityGraphDebug.cs
public partial class CityGraph : ICityGraph { /* kiíratok */ }
```

**Miért ez a struktúra?**

| Alternatíva | Probléma |
|-------------|----------|
| Egy nagy `CityGraph.cs` | 500+ sor, nehéz navigálni |
| Külön osztályok (`PathfindingService`) | Hozzá kellene férni a privát `_adjacencyMatrix`-hoz |
| Partial class | Mindkét probléma megoldódik: a kód szét van választva, de az adatok közösek |

A privát `_adjacencyMatrix` és `_nodes` mezők elérhetők minden partial részből,
mintha egyetlen fájlban lennének.

---

## Forgalom és Ideális Idő

### A teljes kép — mi változik és mi nem?

```
EdgeWeight objektum:
  ├── IdealTimeMinutes  ← SOHA nem változik (JSON-ból töltjük, readonly)
  ├── TrafficMultiplier ← változik (UpdateTrafficConditions híváskor)
  └── CurrentTimeMinutes = (int)(IdealTimeMinutes * TrafficMultiplier)
```

### Összehasonlítás: tényleges vs ideális

```
Futár szimulál → TraversePath() fut
    └── minden él bejárásakor:
            ├── UpdateTrafficConditions()  → véletlenszerű változás minden élen
            └── totalActualTime += edge.CurrentTimeMinutes  → tényleges idő

Szimuláció végén:
    ├── idealTime = CalculateIdealTime(start, end)
    │       └── ideiglenesen 1.0x-ra állít mindent, Dijkstra fut, visszaállít
    └── wasDelayed = totalActualTime > idealTime * 1.2
```

**Fontos következmény:** Ha a forgalom éppen kedvező (pl. sok él `0.8x`-nál),
a tényleges idő **kisebb** lehet az ideálisnál. Ez nem hiba — a forgalom
szimmetikusan változhat: lassíthat is, gyorsíthat is.

---

## Ismert Hiányosságok

### 1. `RegisterCourierMovement()` — üres metódus

A futármozgás jelenleg nem növeli az érintett él forgalmát. Amennyiben
realisztikusabb szimulációt szeretnénk, itt kell implementálni a logikát.

**Javasolt irány:**
```csharp
edge.UpdateTraffic(edge.TrafficMultiplier + 0.05);
```

Ha TPL-lel párhuzamos futárok is mozognak, a hívás `lock`-ot igényel.

### 2. `CalculateIdealTime()` — thread-safety

Az ideiglenesen módosított gráf nem thread-safe. Párhuzamos futárok esetén
ez hibás ideális időket adhat. Megoldási lehetőségek:
- `lock (this)` blokk az egész metódus köré
- Külön, módosítatlan `_idealAdjacencyMatrix` tömb tárolása
- Az `IdealTimeMinutes` értékek közvetlen összeadása Dijkstrán belül

### 3. `UpdateTrafficConditions()` — globális változás

Minden lépésnél az egész gráf forgalma változik — nem csak az érintett él.
Ez egyszerű, de nem realisztikus. Kifinomultabb megközelítés: csak az érintett
él és szomszédai változzanak.

### 4. Koordináta → node mapping (`FindNearestNodeId`)

Több helyen is előfordul ez az Euklideszi-alapú segédmetódus
(`GreedyAssignmentService`, `DeliverySimulationService`, `WarehouseService`).
Érdemes lenne kiszervezni egy közös helyre (pl. `ICityGraph.FindNearestNode(Location)`
vagy egy önálló `NodeLocationMapper` service).
