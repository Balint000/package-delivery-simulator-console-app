# 📦 Csomagkézbesítés Szimuláció - Teljes Dokumentáció

## 📋 Tartalomjegyzék

- [Projekt Áttekintés](#projekt-áttekintés)
- [Technológiai Stack](#technológiai-stack)
- [Telepítés és Futtatás](#telepítés-és-futtatás)
- [Architektúra](#architektúra)
- [Adatbázis Modellek](#adatbázis-modellek)
- [Algoritmusok](#algoritmusok)
- [Szolgáltatások (Services)](#szolgáltatások-services)
- [Párhuzamos Végrehajtás (TPL)](#párhuzamos-végrehajtás-tpl)
- [Használat](#használat)
- [Továbbfejlesztési Lehetőségek](#továbbfejlesztési-lehetőségek)

---

## 🎯 Projekt Áttekintés

Ez a projekt egy **konzolos csomagkézbesítési szimulációs alkalmazás**, amely .NET 10.0 keretrendszerben készült. A program modellezi egy várost zónákkal, rendelésekkel és futárokkal, majd valós időben szimulálja a csomagok kiszállítását.

### Főbb Funkciók

- ✅ **5 DB entitás**: DeliveryOrder, Courier, Zone, RoutePlan, StatusHistory
- ✅ **Greedy algoritmus**: Legközelebbi futár hozzárendelése rendelésekhez
- ✅ **Nearest Neighbor**: Útvonal-optimalizálás (TSP közelítő megoldás)
- ✅ **TPL párhuzamosítás**: Több futár egyidejű szimulációja
- ✅ **Élő státusz**: Valós idejű konzolos megjelenítés
- ✅ **Késéskezelés**: Automatikus értesítés határidő túllépés esetén
- ✅ **Statisztikák**: Futárok teljesítménye, zónánkénti terhelés

---

## 🛠️ Technológiai Stack

| Technológia | Verzió | Felhasználás |
|------------|--------|--------------|
| **.NET** | 10.0 | Futtatókörnyezet |
| **Entity Framework Core** | 9.0.1 | ORM adatbázis kezeléshez |
| **SQLite** | - | Adatbázis motor |
| **Task Parallel Library (TPL)** | - | Párhuzamos végrehajtás |
| **C#** | 12.0 | Programozási nyelv |

---

## 🚀 Telepítés és Futtatás

### Előfeltételek

- **.NET 10.0 SDK** telepítve ([letöltés](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Git** (opcionális, klónozáshoz)

### 1. Projekt Klónozása

```bash
git clone https://github.com/Balint000/package-delivery-simulator-console-app.git
cd package-delivery-simulator-console-app
```

### 2. NuGet Csomagok Telepítése

A csomagok már a `.csproj` fájlban vannak definiálva, így automatikusan települnek:

```bash
dotnet restore
```

**Telepített csomagok:**
- `Microsoft.EntityFrameworkCore.Sqlite` (9.0.1)
- `Microsoft.EntityFrameworkCore.Design` (9.0.1)

### 3. Entity Framework Tools Telepítése

Globális EF Core tool telepítése (migrációkhoz):

```bash
dotnet tool install --global dotnet-ef
```

Ha már telepítve van, frissítsd:

```bash
dotnet tool update --global dotnet-ef
```

### 4. Adatbázis Migráció (Opcionális)

Ha újra akarod generálni a migrációkat:

```bash
# Migráció létrehozása
dotnet ef migrations add InitialCreate

# Adatbázis létrehozása/frissítése
dotnet ef database update
```

**Megjegyzés:** A `dotnet run` parancs automatikusan létrehozza az adatbázist, ha még nem létezik.

### 5. Projekt Futtatása

```bash
dotnet run
```

### 6. Build Parancs (Tesztelés)

```bash
dotnet build
```

---

## 🏗️ Architektúra

### Mappstruktúra

```
package-delivery-simulator-console-app/
├── Data/
│   ├── DeliveryDBContext.cs      # Entity Framework adatbázis kontextus
│   └── SeedData.cs                # Tesztadatok generálása
├── Models/
│   ├── Courier.cs                 # Futár modell
│   ├── DeliveryOrder.cs           # Rendelés modell
│   ├── Zone.cs                    # Zóna modell
│   ├── RoutePlan.cs               # Útvonalterv modell
│   └── StatusHistory.cs           # Státusztörténet modell
├── Services/
│   ├── AssignmentService.cs       # Greedy hozzárendelési algoritmus
│   ├── RoutingService.cs          # Nearest Neighbor útvonal-optimalizálás
│   └── SimulationEngine.cs        # TPL párhuzamos szimuláció
├── Utils/                         # (Későbbi bővítésekhez)
├── Migrations/                    # EF Core migrációs fájlok
├── Program.cs                     # Főprogram belépési pont
├── package-delivery-simulator.csproj
└── delivery.db                    # SQLite adatbázis (futás után)
```

---

## 🗄️ Adatbázis Modellek

### 1. **DeliveryOrder** (Rendelés)

Egy konkrét kiszállítandó csomag adatait tárolja.

```csharp
public class DeliveryOrder
{
    public int Id { get; set; }
    
    // Célállomás
    public string DestinationAddress { get; set; }
    public double DestX { get; set; }
    public double DestY { get; set; }
    
    // Időzítés
    public DateTime CreatedAt { get; set; }
    public DateTime Deadline { get; set; }
    public DateTime? DeliveredAt { get; set; }
    
    // Kapcsolatok
    public int ZoneId { get; set; }
    public int? AssignedCourierId { get; set; }
    
    // Státusz
    public string Status { get; set; } // "Pending", "Assigned", "InProgress", "Delivered", "Delayed"
    public bool WasDelayNotificationSent { get; set; }
}
```

**Státuszok:**
- `Pending` - Várakozik hozzárendelésre
- `Assigned` - Futárhoz rendelve
- `InProgress` - Kiszállítás alatt
- `Delivered` - Kiszállítva
- `Delayed` - Késéssel kiszállítva

### 2. **Courier** (Futár)

Futárokat reprezentál, koordinátákkal és teljesítményadatokkal.

```csharp
public class Courier
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    // Pozíció (algoritmusokhoz)
    public double CurrentLocationX { get; set; }
    public double CurrentLocationY { get; set; }
    
    // Állapot
    public bool IsAvailable { get; set; }
    
    // Teljesítmény statisztikák
    public int CompletedDeliveries { get; set; }
    public double TotalDistanceTraveled { get; set; }
    public int TotalDelayMinutes { get; set; }
}
```

### 3. **Zone** (Zóna)

Város zónáit reprezentálja (pl. "Belváros", "Külváros").

```csharp
public class Zone
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    // Koordináták
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    
    // Statisztika
    public int CurrentLoad { get; set; } // Zónánkénti terhelés
}
```

### 4. **RoutePlan** (Útvonalterv)

Futárok optimalizált útvonalát tárolja.

```csharp
public class RoutePlan
{
    public int Id { get; set; }
    public int CourierId { get; set; }
    
    // Optimalizált sorrend (vesszővel elválasztott Order ID-k)
    public string OptimizedOrderSequence { get; set; }
    
    // Becslések
    public int EstimatedTotalMinutes { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
```

**Példa OptimizedOrderSequence:** `"5,12,8,3"` - A futár ebben a sorrendben szállítja ki a rendeléseket.

### 5. **StatusHistory** (Státusztörténet)

Rendelések állapotváltozásait naplózza.

```csharp
public class StatusHistory
{
    public int Id { get; set; }
    public int DeliveryOrderId { get; set; }
    
    public string NewStatus { get; set; }
    public DateTime Timestamp { get; set; }
    public string Comment { get; set; }
}
```

---

## 🧮 Algoritmusok

### 1. **Greedy Hozzárendelés** (Assignment Service)

**Probléma:** Hogyan rendeljünk futárokat rendelésekhez hatékonyan?

**Megoldás:** Greedy (mohó) algoritmus - minden rendeléshez a **legközelebbi szabad futárt** választja.

#### Működés

```
1. Lekérdezzük az összes függőben lévő rendelést (Status = "Pending")
2. Rendezés deadline szerint (sürgősebbek előre)
3. Minden rendeléshez:
   a. Lekérdezzük az elérhető futárokat (IsAvailable = true)
   b. Kiszámítjuk a távolságot minden futártól a rendelés céljáig
   c. A legközelebbi futárt hozzárendeljük
   d. Futár foglalttá válik (IsAvailable = false)
   e. StatusHistory bejegyzés: "Assigned"
```

#### Távolság Számítás

**Euklideszi távolság:**

\[
d = \sqrt{(x_2 - x_1)^2 + (y_2 - y_1)^2}
\]

```csharp
private double CalculateDistance(double x1, double y1, double x2, double y2)
{
    return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
}
```

#### Komplexitás

- **Időkomplexitás:** O(n × m), ahol n = rendelések száma, m = futárok száma
- **Előny:** Gyors, egyszerű implementáció
- **Hátrány:** Nem garantálja a globálisan optimális megoldást

---

### 2. **Nearest Neighbor Útvonal-optimalizálás** (Routing Service)

**Probléma:** Traveling Salesman Problem (TSP) - egy futárnak több rendelést kell kiszállítani, mi a legrövidebb útvonal?

**Megoldás:** Nearest Neighbor heurisztika (TSP közelítő algoritmus).

#### Működés

```
1. Kiindulópont: futár jelenlegi pozíciója
2. Addig, amíg vannak kiszállítatlan rendelések:
   a. Megkeressük a legközelebbi következő rendelést
   b. Hozzáadjuk az útvonalhoz
   c. Frissítjük a pozíciót
   d. Eltávolítjuk a listából
3. Eredmény: Optimalizált rendelés sorrend
```

#### Példa

```
Futár pozíció: (10, 10)
Rendelések: A(12,13), B(50,50), C(15,12)

1. Legközelebbi: C(15,12) - távolság: 5.83
2. Új pozíció: (15,12)
3. Legközelebbi: A(12,13) - távolság: 3.16
4. Új pozíció: (12,13)
5. Legközelebbi: B(50,50) - távolság: 53.15

Optimalizált sorrend: C → A → B
Teljes távolság: 62.14
```

#### Komplexitás

- **Időkomplexitás:** O(n²), ahol n = rendelések száma futáronként
- **Előny:** Jelentősen csökkenti a megtett távolságot
- **Hátrány:** Nem mindig a legoptimálisabb megoldás (csak közelítés)

---

## 🔧 Szolgáltatások (Services)

### 1. AssignmentService

**Felelősség:** Futárok és rendelések párosítása.

**Főbb metódusok:**

- `FindNearestAvailableCourier(DeliveryOrder order)` - Legközelebbi szabad futár keresése
- `AssignOrderToCourier(DeliveryOrder order, Courier courier)` - Hozzárendelés végrehajtása
- `AssignAllPendingOrders()` - Összes függőben lévő rendelés feldolgozása

**Példa használat:**

```csharp
var assignmentService = new AssignmentService(context);
assignmentService.AssignAllPendingOrders();
```

---

### 2. RoutingService

**Felelősség:** Futárok útvonalának optimalizálása.

**Főbb metódusok:**

- `OptimizeRoute(int courierId)` - Egy futár útvonalának optimalizálása
- `OptimizeAllRoutes()` - Összes futár útvonalának optimalizálása

**Példa használat:**

```csharp
var routingService = new RoutingService(context);
routingService.OptimizeAllRoutes();
```

**Output példa:**

```
🗺️  János #1 - Optimalizált útvonal: 3 rendelés, becsült idő: 45 perc
🗺️  Péter #2 - Optimalizált útvonal: 2 rendelés, becsült idő: 32 perc
```

---

### 3. SimulationEngine

**Felelősség:** Párhuzamos szimuláció futtatása, valós idejű megjelenítés.

**Főbb metódusok:**

- `SimulateCourierAsync(int courierId, CancellationToken token)` - Egy futár szimulációja
- `DisplayStatusAsync(CancellationToken token)` - Konzolos státusz frissítés
- `RunSimulationAsync()` - Teljes szimuláció indítása

**Példa használat:**

```csharp
var simulationEngine = new SimulationEngine(context);
await simulationEngine.RunSimulationAsync();
```

---

## ⚡ Párhuzamos Végrehajtás (TPL)

### Task Parallel Library Használata

A szimuláció több futárt **párhuzamosan** futtat, minden futár egy külön `Task`-ban dolgozik.

#### Implementáció

```csharp
// Minden futár egy külön Task-ban fut
var courierTasks = courierIds.Select(id => SimulateCourierAsync(id, cts.Token)).ToList();

// Várunk, amíg minden Task befejeződik
await Task.WhenAll(courierTasks);
```

#### Thread-Safety

**Probléma:** Több Task egyidejűleg próbál írni az adatbázisba.

**Megoldás 1 - Külön DbContext példány:**

```csharp
// Minden Task saját kontextust használ
using var courierContext = new DeliveryDBContext();
```

**Megoldás 2 - Thread-safe státusz tárolás:**

```csharp
// ConcurrentDictionary használata
private readonly ConcurrentDictionary<int, string> _courierStatuses;
```

#### Valós Idejű Megjelenítés

```csharp
// Külön Task a konzolos megjelenítéshez
var displayTask = DisplayStatusAsync(cts.Token);

// 200ms-onként frissül a konzol
await Task.Delay(200, cancellationToken);
```

---

## 🎮 Használat

### Szimuláció Lépései

```bash
$ dotnet run
```

#### 1. **Indítás**

```
🚚 === CSOMAGKÉZBESÍTÉS SZIMULÁCIÓ ===

⚠️  Az adatbázis már tartalmaz adatokat. Töröljem és újra generáljam? (i/n): i
🗑️  Adatbázis törlése...
✅ Adatbázis törölve és újraépítve.
```

#### 2. **Adatgenerálás**

```
🌱 Tesztadatok generálása...
✅ 5 zóna létrehozva.
✅ 8 futár létrehozva.
✅ 20 rendelés létrehozva.
🎉 Tesztadatok generálása kész!
```

#### 3. **Hozzárendelés (Greedy)**

```
🔄 Rendelések hozzárendelése...
📦 Rendelés #1 -> Futár: János #1 (Távolság: 12.34)
📦 Rendelés #2 -> Futár: Péter #2 (Távolság: 8.56)
...
✅ 20/20 rendelés hozzárendelve.
```

#### 4. **Útvonal-optimalizálás (Nearest Neighbor)**

```
🗺️  Útvonalak optimalizálása...
🗺️  János #1 - Optimalizált útvonal: 3 rendelés, becsült idő: 45 perc
🗺️  Péter #2 - Optimalizált útvonal: 2 rendelés, becsült idő: 32 perc
...
✅ Útvonal-optimalizálás kész!
```

#### 5. **Szimuláció**

```
Nyomj ENTER-t a szimuláció indításához...
```

**Élő státusz képernyő:**

```
🚚 === CSOMAGKÉZBESÍTÉS SZIMULÁCIÓ - ÉLŐ STÁTUSZ ===

  János #1: Úton rendelés #5 felé (12.3 egység)
  Péter #2: ✅ Kiszállítva rendelés #7
  Anna #3: ⚠️ KÉSÉS! Rendelés #12 (5 perc)
  Kata #4: Úton rendelés #3 felé (8.9 egység)
  Zoltán #5: 🏁 Kész! (2 rendelés)
  László #6: Úton rendelés #18 felé (15.7 egység)
  Éva #7: ✅ Kiszállítva rendelés #9
  Gábor #8: Úton rendelés #14 felé (6.2 egység)

[Nyomj CTRL+C a leállításhoz]
```

#### 6. **Befejezés**

```
✅ Szimuláció befejeződött!

✅ Program vége! Nyomj ENTER-t a kilépéshez...
```

---

## 📊 Adatbázis Lekérdezések

### Statisztikák Lekérdezése

#### 1. Futárok Teljesítménye

```csharp
var topCouriers = context.Couriers
    .OrderByDescending(c => c.CompletedDeliveries)
    .Take(5)
    .ToList();

foreach (var courier in topCouriers)
{
    Console.WriteLine($"{courier.Name}: {courier.CompletedDeliveries} kiszállítás, " +
                      $"{courier.TotalDistanceTraveled:F2} egység, " +
                      $"{courier.TotalDelayMinutes} perc késés");
}
```

#### 2. Zónánkénti Terhelés

```csharp
var zoneLoad = context.Zones
    .OrderByDescending(z => z.CurrentLoad)
    .ToList();

foreach (var zone in zoneLoad)
{
    Console.WriteLine($"{zone.Name}: {zone.CurrentLoad} rendelés");
}
```

#### 3. Késések Listája

```csharp
var delays = context.DeliveryOrders
    .Where(o => o.DeliveredAt > o.Deadline)
    .Select(o => new 
    {
        o.Id,
        o.DestinationAddress,
        DelayMinutes = (o.DeliveredAt.Value - o.Deadline).TotalMinutes
    })
    .OrderByDescending(d => d.DelayMinutes)
    .ToList();
```

#### 4. Státusztörténet

```csharp
var history = context.StatusHistories
    .Where(sh => sh.DeliveryOrderId == orderId)
    .OrderBy(sh => sh.Timestamp)
    .ToList();

foreach (var entry in history)
{
    Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] {entry.NewStatus}: {entry.Comment}");
}
```

---

## 🎓 Védés - Magyarázatok

### Miért ezeket az algoritmusokat választottuk?

#### Greedy Hozzárendelés
- **Egyszerű és hatékony:** O(n×m) komplexitás, gyors végrehajtás
- **Valós életben használható:** Sok csomagkézbesítő rendszer használ hasonlót
- **Demonstrálja:** Alapvető optimalizációs gondolkodást

#### Nearest Neighbor
- **Klasszikus TSP megoldás:** Ismert algoritmus a szakirodalomban
- **Jó közelítés:** 25-30%-kal jobb, mint a véletlenszerű útvonal
- **Demonstrálja:** Útvonal-optimalizálási képességet

### TPL előnyei

1. **Skálázhatóság:** Automatikusan kihasználja a többmagos processzorokat
2. **Egyszerűség:** `async/await` egyszerűbb, mint manuális thread kezelés
3. **Teljesítmény:** Párhuzamos végrehajtás = gyorsabb szimuláció

### Entity Framework Core előnyei

1. **Code-First:** Modellekből generálja az adatbázist
2. **Kapcsolatok kezelése:** Automatikus foreign key, cascade delete
3. **LINQ támogatás:** Típusos lekérdezések

---

## 🚧 Továbbfejlesztési Lehetőségek

### 1. Fejlettebb Algoritmusok

- **Genetic Algorithm** (Genetikus algoritmus) TSP-hez
- **A* keresés** akadályokkal teli térképen
- **Machine Learning** predikció a kiszállítási időkhöz

### 2. Vizualizáció

- **Avalonia UI / WPF:** Grafikus térkép megjelenítés
- **SignalR:** Web-alapú valós idejű követés
- **Chart.js integráció:** Statisztikai grafikonok

### 3. Valós Idejű Funkciók

- **Dinamikus rendelések:** Futás közben új rendelések érkezése
- **Forgalmi dugók:** Változó útvonal költségek
- **Prioritások:** VIP rendelések előnyben részesítése

### 4. Bővített Analitika

- **Heatmap:** Zónánkénti terhelés vizualizáció
- **Teljesítmény dashboard:** Futárok összehasonlítása
- **CSV export:** Statisztikák exportálása

### 5. API Integráció

- **REST API:** Külső rendszerek számára
- **OpenStreetMap:** Valós térképadatok
- **Webhook értesítések:** Külső rendszerek értesítése

---

## 🐛 Hibaelhárítás

### Probléma: `dotnet-ef` nem található

**Megoldás:**

```bash
dotnet tool install --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"
```

### Probléma: Build hiba - property nem található

**Ok:** Modell property nevek eltérnek a kódban használttól.

**Megoldás:** Ellenőrizd a Models mappában lévő fájlokat, és használd a helyes property neveket.

### Probléma: Adatbázis zárolva (locked)

**Ok:** Előző futás nem fejeződött be rendesen.

**Megoldás:**

```bash
rm delivery.db
dotnet run
```

### Probléma: Migráció hiba

**Megoldás:**

```bash
# Töröld a Migrations mappát
rm -rf Migrations/

# Újra generálás
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## 📚 Hasznos Linkek

- [Entity Framework Core Docs](https://docs.microsoft.com/ef/core/)
- [Task Parallel Library](https://docs.microsoft.com/dotnet/standard/parallel-programming/task-parallel-library-tpl)
- [SQLite](https://www.sqlite.org/index.html)
- [Traveling Salesman Problem](https://en.wikipedia.org/wiki/Travelling_salesman_problem)
- [Greedy Algorithm](https://en.wikipedia.org/wiki/Greedy_algorithm)

---

## 👨‍💻 Szerző

**Projekt:** Csomagkézbesítés Szimuláció  
**Tárgy:** Programozás .NET (BSc)  
**Repository:** [GitHub](https://github.com/Balint000/package-delivery-simulator-console-app)

---

## 📄 Licensz

Ez a projekt oktatási célra készült.

---

**Utolsó frissítés:** 2026. február 11.
