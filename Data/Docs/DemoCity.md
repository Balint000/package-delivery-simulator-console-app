Készítek egy ASCII art reprezentációt a városról:

                    North District (1) 🏠
                            |
                         (5 min)
                            |
           West End (4) --- Suburb (6) --- Downtown (5) --- East Side (2) 🏠
               🏠            |    (7 min)      |    (7 min)      |
               |         (6 min)          (5 min)           (4 min)
            (9 min)          |              |                  |
               |        [0] Warehouse    (8 min)          Industrial (7)
               |             ⭐              |                  |
               |                                            (5 min)
               |                                               |
           South Park (3) 🏠 ----------------------------- (5 min)


LEGENDA:
⭐ = Warehouse (Raktár)
🏠 = DeliveryPoint (Kézbesítési pont)
◆ = Intersection (Kereszteződés)
Számok () = Utazási idő percben

CSÚCSOK RÉSZLETESEN:
[0] Central Warehouse (⭐) - Zone 1 - Kiindulási pont
[1] North District (🏠) - Zone 1 - Kézbesítési cím
[2] East Side (🏠) - Zone 2 - Kézbesítési cím
[3] South Park (🏠) - Zone 3 - Kézbesítési cím
[4] West End (🏠) - Zone 4 - Kézbesítési cím
[5] Downtown (◆) - Zone 1 - Kereszteződés
[6] Suburb (◆) - Zone 1 - Kereszteződés
[7] Industrial (◆) - Zone 3 - Kereszteződés

PÉLDA ÚTVONAL (Warehouse → North District):
Opció 1: [0] → [5] → [1]  = 5 + 8 = 13 perc (ideális)
Opció 2: [0] → [6] → [1]  = 7 + 5 = 12 perc (rövidebb!)
