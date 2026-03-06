namespace package_delivery_simulator.Domain.ValueObjects
{
    /// <summary>
    /// Egy él súlyát reprezentálja a gráfban.
    /// Tartalmazza az ideális és aktuális utazási időt, valamint a forgalom hatását.
    ///
    /// FONTOS KONCEPCIÓ:
    /// - IdealTime: Mennyi idő lenne forgalom NÉLKÜL
    /// - CurrentTime: Mennyi idő JELENLEG (forgalommal együtt)
    /// - TrafficMultiplier: A forgalom szorzója (1.0 = normál, 2.0 = duplán lassú)
    /// </summary>
    public class EdgeWeight
    {
        /// <summary>
        /// Ideális utazási idő percben (forgalom nélkül).
        /// Ez az "alap" idő, ami soha nem változik.
        /// Példa: Warehouse -> Downtown ideálisan 5 perc
        /// </summary>
        public int IdealTimeMinutes { get; private set; }

        /// <summary>
        /// Aktuális utazási idő percben (forgalommal együtt).
        /// Ez változik a szimuláció során!
        /// Példa: Ha forgalom van, lehet 5 perc helyett 8 perc
        /// </summary>
        public int CurrentTimeMinutes { get; private set; }

        /// <summary>
        /// Forgalom szorzója.
        /// - 1.0 = Normál forgalom (nincs változás)
        /// - 0.5 = Fél annyi idő (nagyon gyors, üres utak)
        /// - 2.0 = Dupla idő (nagy dugó)
        ///
        /// Ezt frissítjük, amikor forgalom változik!
        /// </summary>
        public double TrafficMultiplier { get; private set; } // double + int !!

        /// <summary>
        /// Konstruktor - új él súly létrehozása.
        /// Kezdetben a forgalom 1.0 (normál), így CurrentTime = IdealTime
        /// </summary>
        /// <param name="idealTimeMinutes">Ideális utazási idő percben</param>
        public EdgeWeight(int idealTimeMinutes)
        {
            IdealTimeMinutes = idealTimeMinutes;
            CurrentTimeMinutes = idealTimeMinutes; // Kezdetben nincs forgalom
            TrafficMultiplier = 1.0; // Normál állapot
        }

        /// <summary>
        /// Forgalom frissítése egy új szorzóval.
        /// Automatikusan újraszámolja a CurrentTimeMinutes-t.
        ///
        /// KORLÁT: 0.5x - 2.5x között tartjuk (nem lehet túl gyors vagy túl lassú)
        /// </summary>
        /// <param name="multiplier">Új forgalom szorzó</param>
        public void UpdateTraffic(double multiplier)
        {
            // Biztonsági korlátok: minimum 0.5x, maximum 2.5x
            TrafficMultiplier = Math.Max(0.5, Math.Min(2.5, multiplier));

            // Aktuális idő újraszámolása
            // Példa: IdealTime=5, multiplier=1.5 → CurrentTime = 5 * 1.5 = 7.5 ≈ 8 perc
            CurrentTimeMinutes = (int)(IdealTimeMinutes * TrafficMultiplier);
        }

        /// <summary>
        /// Visszaállítás ideális állapotra (forgalom nélküli).
        /// Hasznos ideális idő számításhoz vagy szimuláció újraindításához.
        /// </summary>
        public void ResetToIdeal()
        {
            TrafficMultiplier = 1.0;
            CurrentTimeMinutes = IdealTimeMinutes;
        }

        /// <summary>
        /// Szöveges reprezentáció debug célokra.
        /// </summary>
        public override string ToString()
        {
            return $"{CurrentTimeMinutes} min (ideal: {IdealTimeMinutes}, traffic: {TrafficMultiplier:F2}x)";
        }
    }
}
