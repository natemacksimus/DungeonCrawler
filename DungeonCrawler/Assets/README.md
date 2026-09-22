# Roguelike Dungeon Crawler

A turn-based, grid-based dungeon crawler built to `roguelike-spec.md`. Descend eight procedurally
generated floors, fight what lives there, and try to climb back out. Death is permanent.

Open `Assets/Scenes/SampleScene.unity` and press Play. The scene needs a single `Dungeon Crawler`
object carrying `GameBootstrap`; everything else — camera setup, sprites, HUD — is built at runtime,
so the project needs no imported art.

## Controls

| Action | Keys |
| --- | --- |
| Move / attack | Arrows, WASD, or numpad |
| Diagonals | Q E Z C, or numpad 7 9 1 3 |
| Wait a turn | Space, numpad 5 |
| Use or equip pack item | 1–9, 0 |
| Take the stairs down | `.` / `>` or Enter |
| Restart after death | R |
| Print the floor as ASCII | M |
| Run the self-play diagnostics | T |

Attacking is walking into a monster. Items are picked up by stepping on them; gold goes straight to
your purse and never takes a pack slot.

## Layout

```
Assets/Scripts/Core/    engine-free simulation  (DungeonCrawler.Core)
Assets/Scripts/Game/    Unity presentation      (DungeonCrawler.Game)
Assets/Editor/          editor menu items       (DungeonCrawler.Editor)
Assets/Tests/EditMode/  NUnit tests             (DungeonCrawler.Tests)
```

`DungeonCrawler.Core` is compiled with `noEngineReferences`, so the rules cannot accidentally depend
on Unity. That is what lets the whole game be played headlessly by the tests and by the self-play
agent — no scene, no frames, no play mode.

| File | Role |
| --- | --- |
| `DungeonGenerator` | Rooms scattered at random, joined by L-shaped corridors, stairs in the room farthest from spawn by BFS |
| `DungeonValidator` | Flood-fills from spawn and proves every walkable tile, stairs included, is reachable |
| `GameState` | Map, actors, items, turn order, and every rule |
| `Fov` / `Pathfinding` | Bresenham line of sight; BFS distance fields and next-step queries |
| `SelfPlay` | A scripted agent that plays whole runs and reports soft-locks, broken invariants and balance |

## Diagnostics

The **Dungeon Crawler** editor menu runs the headless checks without entering play mode:

- *Set Up Play Scene* — adds the bootstrap object to the open scene
- *Run Self-Play Batch* — ten full automated runs
- *Validate 200 Floors* — 25 seeds × 8 depths through the reachability validator
- *Print A Sample Floor* — one floor as ASCII in the console

The game validates every floor it generates as it generates it and logs an error if one is not
playable, so a bad map can never be mistaken for bad luck.

## Balance

Tuned against the self-play agent, which plays competently but not well. Over 40 fixed seeds it
reaches floor 6.2 on average and escapes about one run in eight; a human who retreats and manages
potions should do better. The knobs are `GameConfig`, `Player`'s constants, and `EnemyCatalog`.
