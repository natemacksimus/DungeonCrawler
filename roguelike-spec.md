# Roguelike Dungeon Crawler — Spec & Task List
*For Claude + Unity MCP, minimal human input*

## 1. Core Concept
A turn-based, top-down dungeon crawler. Player descends procedurally generated floors, fights enemies, collects loot, and tries to survive as deep as possible. Permadeath on death (restart from floor 1). Turn-based removes the need for tight real-time physics/animation polish — every action (move, attack, use item) advances the game by one "tick," which is far easier for Claude to implement and test deterministically than real-time movement.

**Why turn-based over real-time:** no physics tuning, no animation blending, no "game feel" judgment calls — just discrete state transitions Claude can unit-test.

## 2. Scope for v1 (keep it small)
- Single character class, no multiplayer, no save/load (session-only)
- 2D grid-based (not 3D) — simplifies collision, pathfinding, and rendering entirely to code
- Primitive/placeholder visuals (colored sprites or Unity primitives) — no custom art needed
- 5–10 procedurally generated floors, increasing difficulty

## 3. Core Systems (build in this order)

### 3.1 Grid & Map Representation
- 2D array (`TileType[,]`) representing walls, floors, doors, stairs
- Tile enum: `Wall, Floor, Door, StairsDown, StairsUp`

### 3.2 Procedural Dungeon Generator
- Algorithm: **randomized room-and-corridor** (simplest reliable approach) or BSP tree
  - Generate N non-overlapping rectangular rooms at random positions/sizes
  - Connect room centers with L-shaped corridors
  - Place stairs-down in the room farthest from spawn (BFS distance)
- Output: a `DungeonData` object (tile grid + room list + spawn points) that all other systems read from
- **Self-testable**: Claude can write a validator that checks every floor tile is reachable from spawn (flood fill) — no human needed to "eyeball" the map

### 3.3 Turn Manager
- Central loop: player acts → enemies act → repeat
- Simple priority queue or just "player turn, then iterate all enemies"

### 3.4 Player Controller
- Grid-based movement (one tile per turn, 4 or 8 directions)
- Attack = move into enemy tile
- Stats: HP, Attack, Defense, Level
- Inventory: simple list, fixed slots (e.g., 10)

### 3.5 Enemy System
- Base `Enemy` class: HP, Attack, simple AI (move toward player if in line of sight/range, else wander/idle)
- 3–5 enemy types differentiated only by stat values + one behavior flag (e.g., "ranged," "flees at low HP") — avoids needing bespoke AI per enemy
- Spawn enemies per room based on floor difficulty (scale count/stats with floor number)

### 3.6 Combat Resolution
- Simple formula: `damage = max(1, attacker.Attack - defender.Defense + random(-2,2))`
- Deterministic and easy to log/debug

### 3.7 Items & Loot
- Categories: potions (heal), scrolls (buffs), weapons/armor (stat mods), gold
- Loot table: weighted random drop per floor, scaling rarity with depth
- Pickup on tile-enter, use from inventory menu

### 3.8 UI
- HP bar, inventory panel, floor number, message log ("You hit the goblin for 4 damage")
- Minimal Unity UI Canvas — text and bars only, no custom art

### 3.9 Progression & Death
- XP on kill → level up → stat increase (simple linear/curve formula)
- On death: show stats summary, restart at floor 1 (no persistence needed for v1)

## 4. Suggested Build Order (task list)
1. Set up Unity project structure (scenes, folders, base scripts)
2. Grid + tile rendering (draw a static test dungeon)
3. Dungeon generator + reachability validator
4. Turn manager skeleton (empty player/enemy turn stubs)
5. Player movement + collision with walls
6. Camera follow
7. Basic enemy: spawn, wander AI, collision with player
8. Combat resolution + HP/death
9. UI: HP bar, message log
10. Inventory + 2–3 item types (healing potion first)
11. Loot drops + pickup
12. Stairs/floor transition + difficulty scaling per floor
13. XP/leveling
14. Death/restart flow
15. Playtest pass: have Claude self-play via scripted test agent or logged random-action runs to catch soft-locks (e.g., unreachable stairs, infinite loops)

## 5. What still needs YOU
- Occasional playtesting for "does this feel fair/fun" (Claude can test functionality, not fun)
- Final art/theme pass if you want it to look like more than colored squares
- Naming, tone, flavor text (optional — can also hand this to Claude with a one-line style prompt)

## 6. Stretch Goals (only after v1 works end-to-end)
- Multiple character classes
- Status effects (poison, stun)
- Boss floors every 5 levels
- Save/load between sessions
- Ranged weapons / line-of-sight targeting
