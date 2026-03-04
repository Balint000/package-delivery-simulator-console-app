# 🔄 Refaktorálás – 2. szakasz: Service réteg + Program.cs bekötése

> **Dátum:** 2026. március  
> **Státusz:** ✅ Kész — a program lefordul és fut

---

## 📋 Mit csináltunk ebben a szakaszban?

### 1. `DeliverySimulationService` — szétbontva felelősségek szerint

Az eredeti tervben egy nagy osztályba kerül minden. Ehelyett három külön osztályt hoztunk létre, mindegyiknek **egy felelőssége** van (Single Responsibility elv):

#### `Infrastructure/Loaders/CourierLoader.cs` *(ÚJ)*
- Betölti a futárokat a `Data/Courier.json` fájlból
- `async/await` — nem blokkolja a programot fájlolvasás közben
- `JsonStringEnumConverter` — kezeli, hogy a JSON-ban szöveg van (`"Available"`), nem szám (`0`)

#### `Infrastructure/Loaders/OrderLoader.cs` *(ÚJ)*
- Betölti a rendeléseket a `Data/Order.json` fájlból
- Ugyanolyan logika mint a `CourierLoader`, csak `DeliveryOrder` típussal

#### `Services/Assignment/GreedyAssignmentService.cs` *(korábban üres, most kész)*
- **Greedy (mohó) algoritmus**: minden rendeléshez a legközelebbi szabad futárt rendeli hozzá
- `AssignToNearest()` — egy rendelés hozzárendelése
- `AssignAll()` — több rendelés tömeges hozzárendelése
- Dijkstra-alapú távolságmérés (nem Euklideszi!)

#### `Services/Simulation/DeliverySimulationService.cs` *(korábban üres, most kész)*
- Egy futár teljes kézbesítési útját szimulálja
- Állapotgép: `Available → Busy → InTransit → Delivered → Available`
- Méri az ideális és tényleges kézbesítési időt
- Késés detektálás: ha a tényleges idő > ideális × 1.2 (20% tolerancia)
- Ügyfélértesítés szimulálása késés esetén (log üzenet)
- `CancellationToken` — Ctrl+C-re szépen leáll

#### `Services/Interfaces/IDeliverySimulationService.cs` *(frissítve)*
- A betöltő metódusok (`LoadCouriersAsync`, `LoadOrdersAsync`) kikerültek
- Ezeket most a dedikált Loader osztályok végzik
- Csak `AssignOrderToNearestCourier` és `SimulateDeliveryAsync` maradt

---

### 2. `Program.cs` — teljes átírás

A régi `Program.cs` egy hardcode-olt demót futtatott (1 futár, 1 rendelés, kézzel megadva).

Az új `Program.cs` **lépésről lépésre**, kommentekkel:

```
1. CityGraphLoader    → városgráf betöltése JSON-ból
2. WarehouseService   → raktárak inicializálása
3. CourierLoader      → futárok betöltése JSON-ból
4. OrderLoader        → rendelések betöltése JSON-ból
5. GreedyAssignment   → rendelések hozzárendelése futárokhoz
6. Szimuláció         → minden hozzárendelt pár szimulálása
7. Összesítő          → eredmények, futár teljesítmény
```

**Manuális bekötés** (`new`-val) — szándékosan egyszerűbb mint a DI container,
mert így látható pontosan mi jön létre és milyen sorrendben.

---

## 🐛 Megoldott hibák

| Hiba | Ok | Megoldás |
|------|----|----------|
| `JsonException: could not convert to CourierStatus` | A JSON-ban `"Available"` szöveg van, de .NET számot (`0`) várt | `JsonStringEnumConverter` hozzáadva a `CourierLoader`-be és `OrderLoader`-be |

---

## 📊 Jelenlegi működés (amit a `dotnet run` mutat)

```
5 futár betöltve → 15 rendelés betöltve
→ 5 rendelés kap futárt (greedy, egyszerre)
→ 10 rendelés nem kap futárt (már mindenki Busy)
→ 5 szimuláció lefut egymás után
→ összesítő + futár teljesítmény kiírás
```

### Miért csak 5 rendelés kap futárt?
Ez **helyes és elvárt viselkedés** ezen a ponton! Az `AssignAll()` egyszerre fut le,
mielőtt bármilyen szimuláció elkezdődne. Mivel csak 5 futár van, az első 5 rendelés
megkapja őket (Busy státuszra váltanak), a többi 10 nem kap senkit.

A teljes megoldás a következő szakaszban jön: szimuláció után a futár visszaáll
`Available`-re, és újra hozzárendelhető.

---

## 🗂️ Fájlstruktúra változások

```
Infrastructure/
  Loaders/
    CityGraphLoader.cs    ← már megvolt
    CourierLoader.cs      🆕 ÚJ
    OrderLoader.cs        🆕 ÚJ

Services/
  Assignment/
    GreedyAssignmentService.cs   ✅ korábban üres, most kész
  Interfaces/
    IDeliverySimulationService.cs  ✏️ frissítve (Load metódusok kivéve)
  Simulation/
    DeliverySimulationService.cs   ✅ korábban üres, most kész

Program.cs   ✏️ teljesen átírva
```

---

## ➡️ Következő lépések

### 1. Sorrendi (queue-alapú) szimuláció
**Probléma:** Jelenleg az összes rendelést egyszerre rendeljük hozzá, mielőtt a szimuláció elindul. Ezért csak 5 kap futárt 15-ből.

**Megoldás:** Miután egy futár befejez egy kézbesítést (visszaáll `Available`-re), azonnal kapja a következő várakozó rendelést — amíg van rendelés a várólistában.

```
Várakozó rendelések sorban: [ORD-006, ORD-007, ORD-008, ...]
Futár kész → következő rendelés kiosztása → szimuláció → következő...
```

### 2. `LiveConsoleRenderer` — élő konzol megjelenítő
A feladatkiírásban szerepel az **élő státuszkijelzés** konzolra:
- Futárok aktuális pozíciója és státusza
- Forgalmi térkép
- Eseménynapló (görgetős log alul)

Ez lesz a `Presentation/LiveConsoleRenderer.cs` implementációja
(az `ILiveConsoleRenderer` interfész már megvan).

### 3. TPL — párhuzamos futárok
Jelenleg a futárok **egymás után** szimulálnak. A feladatkiírás elvárja
a **párhuzamos** futtatást (`Task.WhenAll` vagy `Parallel.ForEach`).

Ez a `LiveConsoleRenderer` után jön, mert párhuzamos konzolíráshoz
thread-safe megjelenítő kell.

### 4. Riportok (`Reporting/` mappa)
- Késések listája és statisztikái
- Futár teljesítmény rangsor
- Zónánkénti terhelés

### 5. `Program.cs` → IHost + DI container
Jelenleg manuális `new`-os bekötés van. Később ez lecserélhető
`IHost` + `IServiceCollection`-re (`.NET` best practice).
