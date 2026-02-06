namespace PackageDelivery.Models;

/// <summary>
/// A várost lefedő zónák modellje.
/// Segít a zónánkénti terhelés mérésében és a futárok terület alapú szétosztásában.
/// </summary>
public class Zone
{
    // Elsődleges kulcs az adatbázishoz
    public int Id { get; set; }

    // A zóna megnevezése (pl. "Belváros", "Északi lakótelep")
    public string Name { get; set; } = string.Empty;

    // A zóna középpontja vagy határai (a legegyszerűbb számításhoz)
    public double CenterX { get; set; }
    public double CenterY { get; set; }

    // Statisztikához: Hány rendelés van jelenleg ebben a zónában?
    // Ezt a "zónánkénti terhelés" kimutatásához fogjuk használni.
    public int CurrentLoad { get; set; }
}
