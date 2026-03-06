┌─────────────────────────────────────────────────────────────────────────┐
│  🚚 LIVE DELIVERY SIMULATION - Demo City                    [12:45:33]  │
└─────────────────────────────────────────────────────────────────────────┘

┌─ COURIERS ────────────────────────────────────────────────────────────┐
│ [1] Kovács János    🚗  North District → East Side      [2/3] ⏱ 3m   │
│ [2] Nagy Eszter     ⏸  Central Warehouse               [0/0] idle    │
│ [3] Tóth Péter      🚗  Downtown → South Park           [1/2] ⏱ 5m   │
│ [4] Szabó Anna      📦  East Side (loading)             [3/5] wait    │
│ [5] Kiss Gábor      ✅  Delivery complete               [5/5] done    │
└───────────────────────────────────────────────────────────────────────┘

┌─ TRAFFIC MAP ─────────────────────────────────────────────────────────┐
│  North District                                                        │
│       ↑ ▓▓░░ (1.3x) 1 courier                                         │
│   Suburb ←──── Downtown                                               │
│       ↓ ░░░░ (1.0x)    ↓ ▓▓▓▓ (1.8x) 2 couriers                      │
│  Warehouse         Industrial                                          │
│       ↓ ░░░░            ↓ ▓░░░ (1.2x)                                │
│  West End          South Park                                          │
│                                                                        │
│  Legend: ░░ Low  ▓░ Medium  ▓▓ High  ▓▓▓ Critical                    │
└───────────────────────────────────────────────────────────────────────┘

┌─ EVENT LOG ───────────────────────────────────────────────────────────┐
│ 12:45:33 ✅ [1] Kovács delivered ORD-0003 to Varga Katalin            │
│ 12:45:30 🚗 [3] Tóth moving: Downtown → South Park (5 min, 1.2x)      │
│ 12:45:25 📦 [4] Szabó picked up 3 packages at East Side               │
│ 12:45:20 ⚠️  Traffic increased on Downtown→East Side (1.5x → 1.8x)   │
│ 12:45:15 🚗 [1] Kovács moving: North District → East Side (7 min)     │
│ 12:45:10 📍 [5] Kiss arrived at Central Warehouse                     │
│ ... (scrolls down)                                                     │
└───────────────────────────────────────────────────────────────────────┘
