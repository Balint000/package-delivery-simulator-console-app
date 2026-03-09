# 📦 Csomagkézbesítés Szimulátor

Modern .NET konzolos alkalmazás, amely egy komplex logisztikai rendszer működését szimulálja várossal, zónákkal, futárokkal és optimalizált útvonaltervezéssel. A projekt egyetemi feladatként készült a .NET alapelvek és modern szoftverfejlesztési gyakorlatok demonstrálására.

## ✨ Főbb Funkciók

- **Intelligens Futár Hozzárendelés:** Greedy algoritmus alapú automatikus hozzárendelés (legközelebbi futár stratégia)
- **Útvonal Optimalizálás:** Nearest neighbor közelítő módszer a hatékony kézbesítési útvonalakhoz
- **Párhuzamos Szimuláció:** Task Parallel Library (TPL) használatával több futár egyidejű működése
- **Valós Idejű Státuszkövetés:** Élő konzolos megjelenítés a futárok aktuális helyzetéről
- **Zóna Alapú Logisztika:** Város felosztása zónákra a terhelés optimalizálásához
- **Teljesítmény Monitoring:** Futárok teljesítményének és késések elemzése
- **Értesítési Rendszer:** Automatikus értesítés késések esetén


## 🛠️ Technológiák

- **Nyelv:** C# 13 (file-scoped namespaces, nullable reference types)
- **Keretrendszer:** .NET 10.0
- **Dependency Injection:** Microsoft.Extensions.DependencyInjection
- **Hosting:** Generic Host (IHost) az életciklus kezeléshez
- **Logging:** Microsoft.Extensions.Logging (strukturált naplózás)
- **Konfiguráció:** Options Pattern (IOptions<T>) appsettings.json-ből
- **Aszinkronitás:** Teljes async/await lánc CancellationToken támogatással

## 📋 Előfeltételek

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) vagy újabb
- Linux (Fedora 43), Windows 11, vagy macOS

## 🚀 Telepítés és Futtatás

### Klónozás és Build

```bash
# Repository klónozása
git clone https://github.com/Balint000/package-delivery-simulator-console-app.git
cd package-delivery-simulator-console-app

# Függőségek visszaállítása
dotnet restore

# Fordítás
dotnet build

# Futtatás
dotnet run

## 📊 Algoritmusok

### Futár Hozzárendelés (Greedy)
A legközelebbi elérhető futárt rendeli a megrendeléshez távolság alapján, O(n) időkomplexitással.

### Útvonal Optimalizálás (Nearest Neighbor)
Közelítő megoldás a Traveling Salesman Problem (TSP) problémára, amely lokálisan optimális útvonalakat hoz létre.

### Párhuzamos Végrehajtás
TPL alapú párhuzamosítás több futár egyidejű szimulációjához, thread-safe adatstruktúrákkal.
## 📈 Riportok

A szimuláció végén az alkalmazás részletes riportokat generál:
- Futáronkénti teljesítmény (sikeres kézbesítések, átlagos idő)
- Zónánkénti terhelés elemzés
- Késések statisztikái
- Rendszer összteljesítménye

## 🤝 Közreműködés

Két fős csapatmunka keretében készült BSc programozási projektként. További fejlesztési ötletek és pull request-ek várhatók.
### 👥 Fejlesztők
- Balint000
- Mogyi13
