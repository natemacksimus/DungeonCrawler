using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonCrawler.Core
{
    /// <summary>What one automated run found.</summary>
    public sealed class SelfPlayReport
    {
        public int Seed;
        public int Turns;
        public int DeepestDepth;
        public int Kills;
        public int Level;
        public GameStatus Status;
        public bool HitTurnLimit;
        public string Exception;
        public readonly List<string> Problems = new List<string>();

        /// <summary>State captured the moment something went wrong, so a failure is debuggable.</summary>
        public readonly List<string> Diagnostics = new List<string>();

        /// <summary>A run passes when nothing broke — dying to monsters is a fine outcome.</summary>
        public bool Passed { get { return Problems.Count == 0 && Exception == null; } }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Passed ? "PASS" : "FAIL");
            sb.Append(" seed=").Append(Seed);
            sb.Append(" status=").Append(Status);
            sb.Append(" depth=").Append(DeepestDepth);
            sb.Append(" turns=").Append(Turns);
            sb.Append(" kills=").Append(Kills);
            sb.Append(" level=").Append(Level);
            if (HitTurnLimit) sb.Append(" [turn limit]");
            if (Exception != null) sb.Append(" exception=").Append(Exception);
            for (int i = 0; i < Problems.Count; i++) sb.Append("\n  - ").Append(Problems[i]);
            for (int i = 0; i < Diagnostics.Count; i++) sb.Append("\n      ").Append(Diagnostics[i]);
            return sb.ToString();
        }
    }

    /// <summary>
    /// A scripted agent that plays the game start to finish with no human and no engine. It exists to
    /// catch soft-locks (unreachable stairs, states where no action makes progress), broken invariants
    /// and wild balance — the failures that one hand-played session cannot reveal.
    /// </summary>
    public static class SelfPlay
    {
        /// <summary>Turns without reaching a new floor before we call it a soft-lock.</summary>
        public const int StagnationLimit = 1200;

        public static SelfPlayReport Run(int seed, int maxTurns = 6000, GameConfig config = null)
        {
            var report = new SelfPlayReport { Seed = seed };

            try
            {
                var game = new GameState(seed, config);
                game.StartNewRun();

                var agent = new Agent(game, new Rng(seed ^ 0x5F3759DF));
                int checkedFloorVersion = -1;
                int lastDepth = 0;
                int turnsAtLastDepthChange = 0;

                while (game.Status == GameStatus.Playing && game.Turn < maxTurns)
                {
                    if (game.FloorVersion != checkedFloorVersion)
                    {
                        checkedFloorVersion = game.FloorVersion;
                        if (game.LastValidation != null && !game.LastValidation.IsValid)
                        {
                            report.Problems.Add("floor " + game.Depth + " failed validation: " + game.LastValidation);
                            break;
                        }
                    }

                    if (game.Depth != lastDepth)
                    {
                        lastDepth = game.Depth;
                        turnsAtLastDepthChange = game.Turn;
                    }
                    else if (game.Turn - turnsAtLastDepthChange > StagnationLimit)
                    {
                        report.Problems.Add("no progress for " + StagnationLimit + " turns on floor " + game.Depth +
                                            " (possible soft-lock)");
                        Snapshot(game, report);
                        break;
                    }

                    string invariant = CheckInvariants(game);
                    if (invariant != null)
                    {
                        report.Problems.Add("turn " + game.Turn + ": " + invariant);
                        Snapshot(game, report);
                        break;
                    }

                    string failure = agent.TakeTurn();
                    if (failure != null)
                    {
                        report.Problems.Add(failure);
                        Snapshot(game, report);
                        break;
                    }
                }

                report.Turns = game.Turn;
                report.DeepestDepth = game.Stats.DeepestDepth;
                report.Kills = game.Stats.Kills;
                report.Level = game.Player.Level;
                report.Status = game.Status;
                report.HitTurnLimit = game.Status == GameStatus.Playing && game.Turn >= maxTurns;
            }
            catch (Exception e)
            {
                report.Exception = e.GetType().Name + ": " + e.Message;
            }

            return report;
        }

        /// <summary>
        /// The playing policy. It keeps a little state — chiefly a loot target it stays committed to —
        /// because a purely per-turn policy oscillates between two goals and never reaches either.
        /// </summary>
        sealed class Agent
        {
            const int LootDetourRange = 10;
            const int LootCommitmentTurns = 40;

            readonly GameState _game;
            readonly Rng _rng;

            Vec2I _lootTarget;
            bool _hasLootTarget;
            int _lootTurnsLeft;

            public Agent(GameState game, Rng rng)
            {
                _game = game;
                _rng = rng;
            }

            /// <summary>Plays one turn. Returns null normally, or a description of a stuck state.</summary>
            public string TakeTurn()
            {
                Player player = _game.Player;

                if (player.Hp * 100 <= player.MaxHp * 50)
                {
                    int potion = player.Pack.FindFirst(ItemKind.Potion);
                    if (potion >= 0 && _game.PlayerUseItem(potion)) return null;
                }

                int weapon = BetterGearSlot(player, ItemKind.Weapon);
                if (weapon >= 0 && _game.PlayerUseItem(weapon)) return null;
                int armor = BetterGearSlot(player, ItemKind.Armor);
                if (armor >= 0 && _game.PlayerUseItem(armor)) return null;

                Enemy adjacent = FindAdjacentEnemy();

                // Buff up as a fight starts rather than hoarding scrolls until death.
                if (adjacent != null && player.AttackBuffTurns == 0 && player.DefenseBuffTurns == 0)
                {
                    int scroll = player.Pack.FindFirst(ItemKind.Scroll);
                    if (scroll >= 0 && _game.PlayerUseItem(scroll)) return null;
                }

                if (adjacent != null && _game.PlayerStep(player.Position.StepToward(adjacent.Position))) return null;

                Vec2I lootStep = LootStep();
                if (lootStep != Vec2I.Zero && _game.PlayerStep(lootStep)) return null;

                if (_game.IsPlayerOnStairsDown && _game.PlayerDescend()) return null;

                if (!_rng.Chance(0.1))
                {
                    Vec2I step = Pathfinding.FirstStepToward(_game.Dungeon, player.Position, _game.Dungeon.StairsDown);
                    if (step == Vec2I.Zero)
                    {
                        return "no path from " + player.Position + " to stairs at " + _game.Dungeon.StairsDown +
                               " on floor " + _game.Depth;
                    }
                    if (_game.PlayerStep(step)) return null;
                }

                // Wander a little so rooms off the direct route still get visited.
                for (int i = 0; i < 8; i++)
                {
                    Vec2I dir = _rng.Pick(Vec2I.Directions8);
                    if (_game.Dungeon.IsWalkable(player.Position + dir) && _game.PlayerStep(dir)) return null;
                }

                return _game.PlayerWait() ? null : "no action was possible at " + player.Position;
            }

            /// <summary>
            /// Step toward the committed loot target, picking a new one when there is none. The
            /// commitment is what prevents flip-flopping between loot and the stairs.
            /// </summary>
            Vec2I LootStep()
            {
                Player player = _game.Player;
                if (player.Pack.IsFull)
                {
                    _hasLootTarget = false;
                    return Vec2I.Zero;
                }

                if (_hasLootTarget)
                {
                    bool stillThere = _game.GroundItemAt(_lootTarget) != null;
                    if (!stillThere || _lootTurnsLeft <= 0 || player.Position == _lootTarget) _hasLootTarget = false;
                }

                if (!_hasLootTarget)
                {
                    GroundItem best = null;
                    int bestDistance = int.MaxValue;
                    for (int i = 0; i < _game.Ground.Count; i++)
                    {
                        GroundItem ground = _game.Ground[i];
                        if (!_game.Explored[ground.Position.X, ground.Position.Y]) continue;

                        int distance = Vec2I.ChebyshevDistance(player.Position, ground.Position);
                        if (distance == 0 || distance > LootDetourRange || distance >= bestDistance) continue;

                        best = ground;
                        bestDistance = distance;
                    }
                    if (best == null) return Vec2I.Zero;

                    _lootTarget = best.Position;
                    _hasLootTarget = true;
                    _lootTurnsLeft = LootCommitmentTurns;
                }

                _lootTurnsLeft--;
                Vec2I step = Pathfinding.FirstStepToward(_game.Dungeon, player.Position, _lootTarget);
                if (step == Vec2I.Zero) _hasLootTarget = false;
                return step;
            }

            /// <summary>Pack slot holding gear strictly better than what is equipped, or -1.</summary>
            static int BetterGearSlot(Player player, ItemKind kind)
            {
                Item equipped = kind == ItemKind.Weapon ? player.Weapon : player.Armor;
                int equippedPower = equipped == null ? 0 : equipped.Def.Power;

                for (int i = 0; i < player.Pack.Count; i++)
                {
                    Item candidate = player.Pack[i];
                    if (candidate.Def.Kind != kind) continue;
                    if (candidate.Def.Power > equippedPower) return i;
                }
                return -1;
            }

            Enemy FindAdjacentEnemy()
            {
                for (int i = 0; i < Vec2I.Directions8.Length; i++)
                {
                    Enemy enemy = _game.EnemyAt(_game.Player.Position + Vec2I.Directions8[i]);
                    if (enemy != null) return enemy;
                }
                return null;
            }
        }

        /// <summary>Records enough of the live state to explain a failure without replaying it.</summary>
        static void Snapshot(GameState game, SelfPlayReport report)
        {
            Player player = game.Player;
            report.Diagnostics.Add("player " + player.Position + " hp " + player.Hp + "/" + player.MaxHp +
                                   " lvl " + player.Level + " atk " + player.TotalAttack + " def " + player.TotalDefense);
            report.Diagnostics.Add("stairs " + game.Dungeon.StairsDown +
                                   " chebyshev " + Vec2I.ChebyshevDistance(player.Position, game.Dungeon.StairsDown) +
                                   " firstStep " + Pathfinding.FirstStepToward(game.Dungeon, player.Position, game.Dungeon.StairsDown));

            var pack = new StringBuilder("pack " + player.Pack.Count + "/" + player.Pack.Capacity + ":");
            for (int i = 0; i < player.Pack.Count; i++) pack.Append(' ').Append(player.Pack[i].Name);
            report.Diagnostics.Add(pack.ToString());

            var adjacent = new StringBuilder("adjacent:");
            for (int i = 0; i < Vec2I.Directions8.Length; i++)
            {
                Enemy enemy = game.EnemyAt(player.Position + Vec2I.Directions8[i]);
                if (enemy == null) continue;
                adjacent.Append(' ').Append(enemy.Name)
                        .Append("(hp ").Append(enemy.Hp).Append('/').Append(enemy.MaxHp)
                        .Append(enemy.IsFleeing ? ", fleeing" : "")
                        .Append(enemy.IsAware ? ", aware" : "")
                        .Append(')');
            }
            report.Diagnostics.Add(adjacent + "  enemiesOnFloor=" + game.Enemies.Count);

            var loot = new StringBuilder("nearby loot:");
            for (int i = 0; i < game.Ground.Count; i++)
            {
                GroundItem ground = game.Ground[i];
                if (Vec2I.ChebyshevDistance(player.Position, ground.Position) > 10) continue;
                loot.Append(' ').Append(ground.Item.Name).Append('@').Append(ground.Position)
                    .Append(game.Explored[ground.Position.X, ground.Position.Y] ? "(seen)" : "(unseen)");
            }
            report.Diagnostics.Add(loot.ToString());

            foreach (string line in game.Log.Tail(12)) report.Diagnostics.Add("log| " + line);
        }

        /// <summary>Rules that must hold after every turn. Returns a description of the first breach.</summary>
        public static string CheckInvariants(GameState game)
        {
            if (!game.Dungeon.IsWalkable(game.Player.Position))
                return "player standing on a non-walkable tile at " + game.Player.Position;

            if (game.Player.Hp > game.Player.MaxHp)
                return "player HP " + game.Player.Hp + " exceeds max " + game.Player.MaxHp;

            if (game.Player.Pack.Count > game.Player.Pack.Capacity)
                return "pack holds " + game.Player.Pack.Count + " items, capacity is " + game.Player.Pack.Capacity;

            var seen = new HashSet<Vec2I>();
            for (int i = 0; i < game.Enemies.Count; i++)
            {
                Enemy enemy = game.Enemies[i];
                if (!game.Dungeon.IsWalkable(enemy.Position))
                    return enemy.Name + " standing in a wall at " + enemy.Position;
                if (!enemy.IsAlive)
                    return "dead " + enemy.Name + " still in the enemy list";
                if (enemy.Position == game.Player.Position)
                    return enemy.Name + " sharing the player tile at " + enemy.Position;
                if (!seen.Add(enemy.Position))
                    return "two enemies share tile " + enemy.Position;
            }
            return null;
        }

        /// <summary>Runs a batch of seeds and returns one line per run plus a summary.</summary>
        public static string RunBatch(int firstSeed, int runs, int maxTurns = 6000)
        {
            var sb = new StringBuilder();
            int passed = 0;
            int victories = 0;
            int deaths = 0;
            int totalDepth = 0;

            for (int i = 0; i < runs; i++)
            {
                SelfPlayReport report = Run(firstSeed + i * 7919, maxTurns);
                if (report.Passed) passed++;
                if (report.Status == GameStatus.Victory) victories++;
                if (report.Status == GameStatus.Dead) deaths++;
                totalDepth += report.DeepestDepth;
                sb.Append(report).Append('\n');
            }

            sb.Append("--- ").Append(passed).Append('/').Append(runs).Append(" runs clean; ")
              .Append(victories).Append(" escaped, ").Append(deaths).Append(" died, avg depth ")
              .Append((totalDepth / (double)runs).ToString("0.0")).Append(" ---");
            return sb.ToString();
        }
    }
}
