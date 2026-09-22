using System.Collections.Generic;
using System.Text;

namespace DungeonCrawler.Core
{
    public enum GameStatus
    {
        Playing,
        Dead,
        Victory
    }

    /// <summary>Run-wide tuning. Kept in one place so the balance pass is a single file to edit.</summary>
    public sealed class GameConfig
    {
        public int MaxDepth = 8;
        public int FovRadius = Player.SightRadius;

        /// <summary>Enemies per room is rolled in 0..(this + depth / 3).</summary>
        public int EnemiesPerRoomBase = 1;
        public int MaxEnemiesPerRoom = 3;

        /// <summary>Chance a given room contains loot.</summary>
        public double ItemChancePerRoom = 0.6;

        /// <summary>No monster spawns this close to where the player arrives.</summary>
        public int SafeRadiusAroundSpawn = 4;

        /// <summary>
        /// The player regains 1 HP every this many turns. Without it a level-1 character has no way
        /// back from a bad fight, and every run ends on floor 1 or 2 waiting for a potion to drop.
        /// </summary>
        public int TurnsPerHpRegen = 9;
    }

    /// <summary>End-of-run summary, shown on the death and victory screens.</summary>
    public sealed class RunStats
    {
        public int Turns;
        public int Kills;
        public int Gold;
        public int DeepestDepth = 1;
        public int Level = 1;
        public int ItemsUsed;
        public string KilledBy;

        public void Reset()
        {
            Turns = 0;
            Kills = 0;
            Gold = 0;
            DeepestDepth = 1;
            Level = 1;
            ItemsUsed = 0;
            KilledBy = null;
        }
    }

    /// <summary>
    /// The entire simulation: map, actors, items, turn order and rules. Contains no engine types, so
    /// the whole game can be driven headlessly by tests and by <see cref="SelfPlay"/>.
    /// </summary>
    public sealed class GameState
    {
        readonly GameConfig _config;

        public GameState(int seed, GameConfig config = null)
        {
            _config = config ?? new GameConfig();
            Seed = seed;
            Rng = new Rng(seed);
            Player = new Player();
            Enemies = new List<Enemy>();
            Ground = new List<GroundItem>();
            Log = new MessageLog();
            Stats = new RunStats();
        }

        public int Seed { get; private set; }
        public Rng Rng { get; private set; }
        public GameConfig Config { get { return _config; } }
        public DungeonData Dungeon { get; private set; }
        public Player Player { get; private set; }
        public List<Enemy> Enemies { get; private set; }
        public List<GroundItem> Ground { get; private set; }
        public MessageLog Log { get; private set; }
        public RunStats Stats { get; private set; }
        public int Depth { get; private set; }
        public int Turn { get; private set; }
        public GameStatus Status { get; private set; }
        public bool[,] Visible { get; private set; }
        public bool[,] Explored { get; private set; }

        /// <summary>Bumped whenever a new floor is built, so views know to rebuild their tiles.</summary>
        public int FloorVersion { get; private set; }

        /// <summary>Last floor validation result. Non-valid means the generator produced a bad map.</summary>
        public ValidationReport LastValidation { get; private set; }

        public int MaxDepth { get { return _config.MaxDepth; } }
        public bool IsPlayerOnStairsDown { get { return Dungeon != null && Player.Position == Dungeon.StairsDown; } }

        // ---------------------------------------------------------------- run lifecycle

        public void StartNewRun()
        {
            Player.Reset();
            Stats.Reset();
            Log.Clear();
            Status = GameStatus.Playing;
            Turn = 0;
            Depth = 0;
            Log.Add("You descend into the dungeon. Arrows or WASD to move, Q/E/Z/C for diagonals.");
            BuildNextFloor();
        }

        /// <summary>Permadeath restart: a brand new run on a fresh seed derived from the current one.</summary>
        public void Restart()
        {
            Seed = (int)Rng.NextUInt();
            Rng = new Rng(Seed);
            StartNewRun();
        }

        void BuildNextFloor()
        {
            Depth++;
            if (Depth > Stats.DeepestDepth) Stats.DeepestDepth = Depth;

            Dungeon = DungeonGenerator.Generate(Depth, Rng);
            LastValidation = DungeonValidator.Validate(Dungeon);

            Visible = new bool[Dungeon.Width, Dungeon.Height];
            Explored = new bool[Dungeon.Width, Dungeon.Height];

            Player.Position = Dungeon.SpawnPoint;
            Enemies.Clear();
            Ground.Clear();

            PopulateFloor();
            RecomputeFov();
            FloorVersion++;

            Log.Add("-- Floor " + Depth + " of " + MaxDepth + " --");
        }

        void PopulateFloor()
        {
            var taken = new HashSet<Vec2I> { Player.Position, Dungeon.StairsDown };

            for (int i = 0; i < Dungeon.Rooms.Count; i++)
            {
                Room room = Dungeon.Rooms[i];

                int enemyCount = Rng.RangeInclusive(0, _config.EnemiesPerRoomBase + Depth / 3);
                if (enemyCount > _config.MaxEnemiesPerRoom) enemyCount = _config.MaxEnemiesPerRoom;

                for (int e = 0; e < enemyCount; e++)
                {
                    Vec2I? spot = FindFreeTileInRoom(room, taken, _config.SafeRadiusAroundSpawn);
                    if (spot == null) break;
                    taken.Add(spot.Value);
                    Enemies.Add(new Enemy(EnemyCatalog.Roll(Depth, Rng), Depth, spot.Value));
                }

                if (Rng.Chance(_config.ItemChancePerRoom))
                {
                    Vec2I? spot = FindFreeTileInRoom(room, taken, 0);
                    if (spot != null)
                    {
                        taken.Add(spot.Value);
                        Ground.Add(new GroundItem(LootTable.Roll(Depth, Rng), spot.Value));
                    }
                }
            }

            // Every floor holds at least one potion, and on floor 1 it sits in the room you start in.
            // Otherwise a dry spell in the loot table is indistinguishable from a broken run.
            bool hasPotion = false;
            for (int i = 0; i < Ground.Count && !hasPotion; i++)
                hasPotion = Ground[i].Item.Def.Kind == ItemKind.Potion;

            if (!hasPotion || Depth == 1)
            {
                Room room = Depth == 1 ? Dungeon.Rooms[0] : Dungeon.Rooms[Rng.Range(0, Dungeon.Rooms.Count)];
                Vec2I? spot = FindFreeTileInRoom(room, taken, 0);
                if (spot != null)
                {
                    taken.Add(spot.Value);
                    Ground.Add(new GroundItem(new Item(ItemCatalog.HealingPotion), spot.Value));
                }
            }
        }

        Vec2I? FindFreeTileInRoom(Room room, HashSet<Vec2I> taken, int minDistanceFromPlayer)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vec2I p = room.RandomInteriorTile(Rng);
                if (!Dungeon.IsWalkable(p)) continue;
                if (taken.Contains(p)) continue;
                if (minDistanceFromPlayer > 0 && Vec2I.ChebyshevDistance(p, Player.Position) < minDistanceFromPlayer) continue;
                return p;
            }
            return null;
        }

        // ---------------------------------------------------------------- player actions

        /// <summary>
        /// Steps one tile, attacking whatever is in the way. Returns true when the action consumed a
        /// turn (a bump into a wall does not).
        /// </summary>
        public bool PlayerStep(Vec2I direction)
        {
            if (Status != GameStatus.Playing) return false;
            if (direction == Vec2I.Zero) return PlayerWait();

            Vec2I target = Player.Position + direction;

            Enemy occupant = EnemyAt(target);
            if (occupant != null)
            {
                AttackResult result = Combat.Resolve(Player, occupant, Rng);
                Log.Add(result.Describe());
                if (!occupant.IsAlive) KillEnemy(occupant);
                EndPlayerTurn();
                return true;
            }

            if (!Dungeon.IsWalkable(target))
            {
                Log.Add("A wall blocks your way.");
                return false;
            }

            Player.Position = target;
            PickUpItemsHere();
            EndPlayerTurn();
            return true;
        }

        public bool PlayerWait()
        {
            if (Status != GameStatus.Playing) return false;
            EndPlayerTurn();
            return true;
        }

        /// <summary>Drinks, reads, or equips the item in a pack slot.</summary>
        public bool PlayerUseItem(int slot)
        {
            if (Status != GameStatus.Playing) return false;
            if (slot < 0 || slot >= Player.Pack.Count) return false;

            Item item = Player.Pack[slot];
            switch (item.Def.Kind)
            {
                case ItemKind.Potion:
                {
                    if (Player.Hp >= Player.MaxHp)
                    {
                        Log.Add("You are already at full health.");
                        return false;
                    }
                    int before = Player.Hp;
                    Player.Heal(item.Def.Power);
                    Player.Pack.RemoveAt(slot);
                    Stats.ItemsUsed++;
                    Log.Add("You drink the " + item.Def.Name + " and recover " + (Player.Hp - before) + " HP.");
                    break;
                }
                case ItemKind.Scroll:
                {
                    Player.ApplyBuff(item.Def.Buff, item.Def.Power, item.Def.Duration);
                    Player.Pack.RemoveAt(slot);
                    Stats.ItemsUsed++;
                    Log.Add("You read the " + item.Def.Name + ". +" + item.Def.Power + " " +
                            (item.Def.Buff == BuffKind.Attack ? "attack" : "defense") +
                            " for " + item.Def.Duration + " turns.");
                    break;
                }
                case ItemKind.Weapon:
                case ItemKind.Armor:
                {
                    Player.Pack.RemoveAt(slot);
                    Item replaced = Player.Equip(item);
                    Log.Add("You equip the " + item.Def.Name + ".");
                    if (replaced != null)
                    {
                        if (Player.Pack.TryAdd(replaced)) Log.Add("You stow the " + replaced.Def.Name + ".");
                        else
                        {
                            Ground.Add(new GroundItem(replaced, Player.Position));
                            Log.Add("You drop the " + replaced.Def.Name + " — your pack is full.");
                        }
                    }
                    break;
                }
                default:
                    return false;
            }

            EndPlayerTurn();
            return true;
        }

        /// <summary>Takes the stairs down, or wins the run when this was the last floor.</summary>
        public bool PlayerDescend()
        {
            if (Status != GameStatus.Playing) return false;
            if (!IsPlayerOnStairsDown)
            {
                Log.Add("There are no stairs down here.");
                return false;
            }

            if (Depth >= MaxDepth)
            {
                Status = GameStatus.Victory;
                Stats.Level = Player.Level;
                Log.Add("You climb out of the dungeon alive. You win!");
                return true;
            }

            BuildNextFloor();
            return true;
        }

        void PickUpItemsHere()
        {
            for (int i = Ground.Count - 1; i >= 0; i--)
            {
                GroundItem ground = Ground[i];
                if (ground.Position != Player.Position) continue;

                if (ground.Item.Def.Kind == ItemKind.Gold)
                {
                    Player.Gold += ground.Item.Amount;
                    Stats.Gold = Player.Gold;
                    Log.Add("You pick up " + ground.Item.Amount + " gold.");
                    Ground.RemoveAt(i);
                    continue;
                }

                if (Player.Pack.TryAdd(ground.Item))
                {
                    Log.Add("You pick up the " + ground.Item.Def.Name + ".");
                    Ground.RemoveAt(i);
                }
                else
                {
                    Log.Add("Your pack is full — the " + ground.Item.Def.Name + " stays here.");
                }
            }
        }

        void KillEnemy(Enemy enemy)
        {
            Enemies.Remove(enemy);
            Stats.Kills++;

            int levels = Player.GainXp(enemy.XpValue);
            Stats.Level = Player.Level;
            if (levels > 0)
            {
                Log.Add("You reach level " + Player.Level + "! Max HP " + Player.MaxHp +
                        ", attack " + Player.TotalAttack + ", defense " + Player.TotalDefense + ".");
            }

            // Every kill has a chance to leave something behind, richer the deeper you are.
            if (Rng.Chance(0.25 + Depth * 0.02))
            {
                Ground.Add(new GroundItem(LootTable.Roll(Depth, Rng), enemy.Position));
            }
        }

        // ---------------------------------------------------------------- turn resolution

        void EndPlayerTurn()
        {
            Turn++;
            Stats.Turns++;

            string expired = Player.TickBuffs();
            if (expired != null) Log.Add(expired);

            if (_config.TurnsPerHpRegen > 0 && Turn % _config.TurnsPerHpRegen == 0) Player.Heal(1);

            RecomputeFov();
            EnemyTurns();
            RecomputeFov();

            if (!Player.IsAlive && Status == GameStatus.Playing)
            {
                Status = GameStatus.Dead;
                Stats.Level = Player.Level;
                Log.Add("You die on floor " + Depth + ".");
            }
            else if (IsPlayerOnStairsDown && Status == GameStatus.Playing)
            {
                Log.Add(Depth >= MaxDepth
                    ? "Stairs out of the dungeon. Press > to escape."
                    : "You find stairs down. Press > to descend.");
            }
        }

        void EnemyTurns()
        {
            // Snapshot: an enemy can die mid-loop when the player retaliates through a trap or script.
            var acting = Enemies.ToArray();
            for (int i = 0; i < acting.Length; i++)
            {
                Enemy enemy = acting[i];
                if (!enemy.IsAlive || !Player.IsAlive) continue;
                TakeEnemyTurn(enemy);
            }
        }

        void TakeEnemyTurn(Enemy enemy)
        {
            EnemyArchetype archetype = enemy.Archetype;
            int distance = Vec2I.ChebyshevDistance(enemy.Position, Player.Position);
            bool canSee = distance <= archetype.SightRadius
                          && Fov.HasLineOfSight(Dungeon, enemy.Position, Player.Position);

            if (canSee)
            {
                enemy.IsAware = true;
                enemy.LastKnownPlayerPosition = Player.Position;
            }

            // Brutes doze until something is right next to them.
            if (archetype.Sluggish && !enemy.IsAware && distance > 1) return;
            if (!enemy.IsAware)
            {
                if (distance <= 1) enemy.IsAware = true;
                else
                {
                    Wander(enemy);
                    return;
                }
            }

            // Cowards break off first; only a cornered one stays and fights.
            if (enemy.IsFleeing && TryFlee(enemy)) return;

            if (distance <= 1)
            {
                AttackResult result = Combat.Resolve(enemy, Player, Rng);
                Log.Add(result.Describe());
                if (!Player.IsAlive) Stats.KilledBy = enemy.Name;
                return;
            }

            if (archetype.Ranged && canSee && distance <= archetype.AttackRange)
            {
                AttackResult result = Combat.Resolve(enemy, Player, Rng, true);
                Log.Add(result.Describe());
                if (!Player.IsAlive) Stats.KilledBy = enemy.Name;
                return;
            }

            Vec2I goal = canSee ? Player.Position : enemy.LastKnownPlayerPosition;
            if (!canSee && enemy.Position == goal)
            {
                // Lost the trail.
                enemy.IsAware = false;
                Wander(enemy);
                return;
            }

            Vec2I step = Pathfinding.FirstStepToward(Dungeon, enemy.Position, goal, OccupiedTiles(enemy));
            if (step == Vec2I.Zero) step = enemy.Position.StepToward(goal);
            if (!TryMove(enemy, step))
            {
                // Slide along the blocking wall rather than standing still.
                if (!TryMove(enemy, new Vec2I(step.X, 0))) TryMove(enemy, new Vec2I(0, step.Y));
            }
        }

        /// <summary>Tries to put distance between an enemy and the player. False when it is boxed in.</summary>
        bool TryFlee(Enemy enemy)
        {
            Vec2I away = Player.Position.StepToward(enemy.Position);
            if (TryMove(enemy, away)) return true;
            if (TryMove(enemy, new Vec2I(away.X, 0))) return true;
            if (TryMove(enemy, new Vec2I(0, away.Y))) return true;

            // Sidestep: keep the distance rather than stepping back toward the player.
            if (TryMove(enemy, new Vec2I(away.Y, away.X))) return true;
            return TryMove(enemy, new Vec2I(-away.Y, -away.X));
        }

        void Wander(Enemy enemy)
        {
            if (enemy.WanderTurnsLeft <= 0 || enemy.WanderDirection == Vec2I.Zero)
            {
                if (!Rng.Chance(0.6)) return; // Idle most turns; monsters are not restless.
                enemy.WanderDirection = Rng.Pick(Vec2I.Directions8);
                enemy.WanderTurnsLeft = Rng.RangeInclusive(2, 5);
            }

            enemy.WanderTurnsLeft--;
            if (!TryMove(enemy, enemy.WanderDirection)) enemy.WanderTurnsLeft = 0;
        }

        bool TryMove(Enemy enemy, Vec2I direction)
        {
            if (direction == Vec2I.Zero) return false;
            Vec2I target = enemy.Position + direction;
            if (!Dungeon.IsWalkable(target)) return false;
            if (target == Player.Position) return false;
            if (EnemyAt(target) != null) return false;
            enemy.Position = target;
            return true;
        }

        HashSet<Vec2I> OccupiedTiles(Enemy exclude)
        {
            var set = new HashSet<Vec2I>();
            for (int i = 0; i < Enemies.Count; i++)
            {
                if (Enemies[i] == exclude) continue;
                set.Add(Enemies[i].Position);
            }
            return set;
        }

        void RecomputeFov()
        {
            Fov.Compute(Dungeon, Player.Position, _config.FovRadius, Visible, Explored);
        }

        // ---------------------------------------------------------------- queries

        public Enemy EnemyAt(Vec2I position)
        {
            for (int i = 0; i < Enemies.Count; i++)
                if (Enemies[i].Position == position) return Enemies[i];
            return null;
        }

        public GroundItem GroundItemAt(Vec2I position)
        {
            for (int i = 0; i < Ground.Count; i++)
                if (Ground[i].Position == position) return Ground[i];
            return null;
        }

        public bool IsVisible(Vec2I p)
        {
            return Dungeon.InBounds(p) && Visible[p.X, p.Y];
        }

        public string StatsSummary()
        {
            var sb = new StringBuilder();
            sb.Append(Status == GameStatus.Victory ? "ESCAPED THE DUNGEON" : "YOU DIED").Append('\n');
            if (Status == GameStatus.Dead && !string.IsNullOrEmpty(Stats.KilledBy))
                sb.Append("Killed by ").Append(Phrasing.WithArticle(Stats.KilledBy)).Append('\n');
            sb.Append("Floor reached: ").Append(Stats.DeepestDepth).Append(" / ").Append(MaxDepth).Append('\n');
            sb.Append("Level: ").Append(Stats.Level).Append('\n');
            sb.Append("Monsters slain: ").Append(Stats.Kills).Append('\n');
            sb.Append("Gold: ").Append(Stats.Gold).Append('\n');
            sb.Append("Turns: ").Append(Stats.Turns);
            return sb.ToString();
        }
    }
}
