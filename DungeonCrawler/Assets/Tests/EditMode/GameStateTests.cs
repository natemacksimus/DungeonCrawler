using NUnit.Framework;
using DungeonCrawler.Core;

namespace DungeonCrawler.Tests
{
    public class GameStateTests
    {
        static GameState NewGame(int seed = 2024, int maxDepth = 8)
        {
            var game = new GameState(seed, new GameConfig { MaxDepth = maxDepth });
            game.StartNewRun();
            return game;
        }

        [Test]
        public void NewRunStartsOnFloorOneAtTheSpawnPoint()
        {
            GameState game = NewGame();

            Assert.AreEqual(1, game.Depth);
            Assert.AreEqual(0, game.Turn);
            Assert.AreEqual(GameStatus.Playing, game.Status);
            Assert.AreEqual(game.Dungeon.SpawnPoint, game.Player.Position);
            Assert.IsTrue(game.LastValidation.IsValid);
            Assert.AreEqual(Player.StartingMaxHp, game.Player.Hp);
        }

        [Test]
        public void WalkingIntoAWallCostsNoTurn()
        {
            GameState game = NewGame();
            game.Enemies.Clear();

            // The spawn tile is a room centre, so it never touches a wall. Stand on a room edge.
            Room room = game.Dungeon.Rooms[0];
            game.Player.Position = new Vec2I(room.X, room.Y);

            Vec2I before = game.Player.Position;
            Vec2I wallDirection = Vec2I.Zero;
            for (int i = 0; i < Vec2I.Directions8.Length; i++)
            {
                if (!game.Dungeon.IsWalkable(before + Vec2I.Directions8[i]))
                {
                    wallDirection = Vec2I.Directions8[i];
                    break;
                }
            }
            Assert.AreNotEqual(Vec2I.Zero, wallDirection, "a room corner should face at least one wall");

            Assert.IsFalse(game.PlayerStep(wallDirection));
            Assert.AreEqual(before, game.Player.Position);
            Assert.AreEqual(0, game.Turn);
        }

        [Test]
        public void MovingOntoAFloorTileAdvancesOneTurn()
        {
            GameState game = NewGame();
            Vec2I open = Vec2I.Zero;
            for (int i = 0; i < Vec2I.Directions8.Length; i++)
            {
                Vec2I candidate = game.Player.Position + Vec2I.Directions8[i];
                if (game.Dungeon.IsWalkable(candidate) && game.EnemyAt(candidate) == null)
                {
                    open = Vec2I.Directions8[i];
                    break;
                }
            }

            Assert.IsTrue(game.PlayerStep(open));
            Assert.AreEqual(1, game.Turn);
        }

        [Test]
        public void MovingIntoAnEnemyAttacksItInsteadOfSwapping()
        {
            GameState game = NewGame();
            Vec2I spot = game.Player.Position + new Vec2I(1, 0);
            if (!game.Dungeon.IsWalkable(spot)) spot = game.Player.Position + new Vec2I(-1, 0);
            Assert.IsTrue(game.Dungeon.IsWalkable(spot), "need one open tile beside the player");

            var target = new Enemy(EnemyCatalog.GiantRat, 1, spot);
            target.MaxHp = 500;
            target.Hp = 500;
            game.Enemies.Add(target);

            Vec2I playerBefore = game.Player.Position;
            Assert.IsTrue(game.PlayerStep(spot - playerBefore));

            Assert.AreEqual(playerBefore, game.Player.Position, "the player should not step onto the enemy");
            Assert.Less(target.Hp, 500);
        }

        [Test]
        public void KillingAnEnemyGrantsXpAndRemovesIt()
        {
            GameState game = NewGame();
            game.Enemies.Clear();

            Vec2I spot = FirstOpenNeighbour(game);
            var target = new Enemy(EnemyCatalog.GiantRat, 1, spot);
            target.Hp = 1;
            game.Enemies.Add(target);
            int xpValue = target.XpValue;

            game.PlayerStep(spot - game.Player.Position);

            Assert.IsFalse(game.Enemies.Contains(target));
            Assert.AreEqual(1, game.Stats.Kills);
            Assert.AreEqual(xpValue, game.Player.Xp);
        }

        [Test]
        public void SteppingOnGoldAddsItToThePurseWithoutUsingASlot()
        {
            GameState game = NewGame();
            game.Enemies.Clear();
            game.Ground.Clear();

            Vec2I spot = FirstOpenNeighbour(game);
            game.Ground.Add(new GroundItem(new Item(ItemCatalog.GoldPile, 42), spot));

            game.PlayerStep(spot - game.Player.Position);

            Assert.AreEqual(42, game.Player.Gold);
            Assert.AreEqual(0, game.Player.Pack.Count);
            Assert.AreEqual(0, game.Ground.Count);
        }

        [Test]
        public void SteppingOnAnItemPicksItUp()
        {
            GameState game = NewGame();
            game.Enemies.Clear();
            game.Ground.Clear();

            Vec2I spot = FirstOpenNeighbour(game);
            game.Ground.Add(new GroundItem(new Item(ItemCatalog.HealingPotion), spot));

            game.PlayerStep(spot - game.Player.Position);

            Assert.AreEqual(1, game.Player.Pack.Count);
            Assert.AreEqual(ItemCatalog.HealingPotion, game.Player.Pack[0].Def);
        }

        [Test]
        public void AFullPackLeavesTheItemOnTheFloor()
        {
            GameState game = NewGame();
            game.Enemies.Clear();
            game.Ground.Clear();
            for (int i = 0; i < game.Player.Pack.Capacity; i++) game.Player.Pack.TryAdd(new Item(ItemCatalog.Dagger));

            Vec2I spot = FirstOpenNeighbour(game);
            game.Ground.Add(new GroundItem(new Item(ItemCatalog.HealingPotion), spot));

            game.PlayerStep(spot - game.Player.Position);

            Assert.AreEqual(1, game.Ground.Count);
            StringAssert.Contains("pack is full", game.Log.LastLine.Replace("—", "-"));
        }

        [Test]
        public void DrinkingAPotionHealsAndConsumesIt()
        {
            GameState game = NewGame();
            game.Enemies.Clear();
            game.Player.TakeDamage(15);
            game.Player.Pack.TryAdd(new Item(ItemCatalog.HealingPotion));

            int before = game.Player.Hp;
            Assert.IsTrue(game.PlayerUseItem(0));

            Assert.Greater(game.Player.Hp, before);
            Assert.AreEqual(0, game.Player.Pack.Count);
            Assert.AreEqual(1, game.Stats.ItemsUsed);
        }

        [Test]
        public void DrinkingAtFullHealthIsRefusedAndCostsNoTurn()
        {
            GameState game = NewGame();
            game.Player.Pack.TryAdd(new Item(ItemCatalog.HealingPotion));

            Assert.IsFalse(game.PlayerUseItem(0));
            Assert.AreEqual(1, game.Player.Pack.Count);
            Assert.AreEqual(0, game.Turn);
        }

        [Test]
        public void ReadingAScrollAppliesTheBuff()
        {
            GameState game = NewGame();
            game.Enemies.Clear();
            game.Player.Pack.TryAdd(new Item(ItemCatalog.ScrollOfMight));
            int before = game.Player.TotalAttack;

            Assert.IsTrue(game.PlayerUseItem(0));

            Assert.Greater(game.Player.TotalAttack, before);
            Assert.Greater(game.Player.AttackBuffTurns, 0);
        }

        [Test]
        public void EquippingFromThePackSwapsGearAndStowsTheOldPiece()
        {
            GameState game = NewGame();
            game.Enemies.Clear();
            game.Player.Equip(new Item(ItemCatalog.Dagger));
            game.Player.Pack.TryAdd(new Item(ItemCatalog.BattleAxe));

            Assert.IsTrue(game.PlayerUseItem(0));

            Assert.AreEqual(ItemCatalog.BattleAxe, game.Player.Weapon.Def);
            Assert.AreEqual(1, game.Player.Pack.Count);
            Assert.AreEqual(ItemCatalog.Dagger, game.Player.Pack[0].Def);
        }

        [Test]
        public void DescendingOnlyWorksFromTheStairs()
        {
            GameState game = NewGame();
            Assert.IsFalse(game.PlayerDescend());
            Assert.AreEqual(1, game.Depth);

            game.Player.Position = game.Dungeon.StairsDown;
            Assert.IsTrue(game.PlayerDescend());
            Assert.AreEqual(2, game.Depth);
            Assert.AreEqual(game.Dungeon.SpawnPoint, game.Player.Position);
            Assert.IsTrue(game.LastValidation.IsValid);
        }

        [Test]
        public void DescendingKeepsTheCharacterButRebuildsTheFloor()
        {
            GameState game = NewGame();
            game.Player.GainXp(25);
            game.Player.Equip(new Item(ItemCatalog.ShortSword));
            int level = game.Player.Level;
            int floorVersion = game.FloorVersion;

            game.Player.Position = game.Dungeon.StairsDown;
            game.PlayerDescend();

            Assert.AreEqual(level, game.Player.Level);
            Assert.AreEqual(ItemCatalog.ShortSword, game.Player.Weapon.Def);
            Assert.AreNotEqual(floorVersion, game.FloorVersion);
            Assert.AreEqual(2, game.Stats.DeepestDepth);
        }

        [Test]
        public void TakingTheStairsOnTheLastFloorWinsTheRun()
        {
            GameState game = NewGame(77, 2);

            game.Player.Position = game.Dungeon.StairsDown;
            game.PlayerDescend();
            Assert.AreEqual(2, game.Depth);

            game.Player.Position = game.Dungeon.StairsDown;
            Assert.IsTrue(game.PlayerDescend());
            Assert.AreEqual(GameStatus.Victory, game.Status);
        }

        [Test]
        public void DeathEndsTheRunAndNamesTheKiller()
        {
            GameState game = NewGame();
            game.Enemies.Clear();
            game.Ground.Clear();

            var killer = new Enemy(EnemyCatalog.OrcBrute, 8, FirstOpenNeighbour(game));
            killer.Attack = 999;
            game.Enemies.Add(killer);
            game.Player.Hp = 1;

            game.PlayerWait();

            Assert.AreEqual(GameStatus.Dead, game.Status);
            Assert.AreEqual(killer.Name, game.Stats.KilledBy);
            StringAssert.Contains("You die", game.Log.LastLine);
        }

        [Test]
        public void NoActionsAreAcceptedAfterDeath()
        {
            GameState game = NewGame();
            game.Player.Hp = 1;
            game.Enemies.Clear();
            var killer = new Enemy(EnemyCatalog.OrcBrute, 8, FirstOpenNeighbour(game));
            killer.Attack = 999;
            game.Enemies.Add(killer);
            game.PlayerWait();

            int turn = game.Turn;
            Assert.IsFalse(game.PlayerWait());
            Assert.IsFalse(game.PlayerStep(new Vec2I(1, 0)));
            Assert.IsFalse(game.PlayerDescend());
            Assert.AreEqual(turn, game.Turn);
        }

        [Test]
        public void RestartGivesAFreshLevelOneRunOnFloorOne()
        {
            GameState game = NewGame();
            game.Player.GainXp(500);
            game.Player.Position = game.Dungeon.StairsDown;
            game.PlayerDescend();

            game.Restart();

            Assert.AreEqual(1, game.Depth);
            Assert.AreEqual(1, game.Player.Level);
            Assert.AreEqual(0, game.Stats.Kills);
            Assert.AreEqual(1, game.Stats.DeepestDepth);
            Assert.AreEqual(GameStatus.Playing, game.Status);
        }

        [Test]
        public void EnemiesNeverSpawnOnTopOfEachOtherOrThePlayer()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var game = new GameState(seed);
                game.StartNewRun();
                for (int floor = 0; floor < 4; floor++)
                {
                    Assert.IsNull(SelfPlay.CheckInvariants(game), "seed " + seed + " floor " + game.Depth);
                    game.Player.Position = game.Dungeon.StairsDown;
                    game.PlayerDescend();
                }
            }
        }

        [Test]
        public void EnemiesRespectTheSafeRadiusAroundTheArrivalTile()
        {
            var config = new GameConfig { SafeRadiusAroundSpawn = 4 };
            for (int seed = 50; seed < 70; seed++)
            {
                var game = new GameState(seed, config);
                game.StartNewRun();
                for (int i = 0; i < game.Enemies.Count; i++)
                {
                    int distance = Vec2I.ChebyshevDistance(game.Enemies[i].Position, game.Dungeon.SpawnPoint);
                    Assert.GreaterOrEqual(distance, 4, "seed " + seed + ": " + game.Enemies[i].Name + " spawned too close");
                }
            }
        }

        [Test]
        public void AnAwareEnemyClosesTheDistanceOverSeveralTurns()
        {
            GameState game = NewGame(31);
            game.Enemies.Clear();

            // Drop a goblin at a reachable tile a few steps away and let it hunt.
            int[,] dist = Pathfinding.BfsDistanceField(game.Dungeon, game.Player.Position);
            Vec2I start = Vec2I.Zero;
            for (int y = 0; y < game.Dungeon.Height && start == Vec2I.Zero; y++)
                for (int x = 0; x < game.Dungeon.Width; x++)
                    if (dist[x, y] >= 4 && dist[x, y] <= 6) { start = new Vec2I(x, y); break; }
            Assert.AreNotEqual(Vec2I.Zero, start, "no tile at the right distance");

            game.Player.MaxHp = 500;
            game.Player.Hp = 500;

            var goblin = new Enemy(EnemyCatalog.Goblin, 1, start);
            goblin.IsAware = true;
            goblin.LastKnownPlayerPosition = game.Player.Position;
            goblin.MaxHp = 900;
            goblin.Hp = 900;
            game.Enemies.Add(goblin);

            int before = Vec2I.ChebyshevDistance(goblin.Position, game.Player.Position);
            for (int i = 0; i < 8; i++) game.PlayerWait();
            int after = Vec2I.ChebyshevDistance(goblin.Position, game.Player.Position);

            Assert.Less(after, before, "the goblin should have closed in");
        }

        [Test]
        public void AWoundedKoboldRunsAway()
        {
            GameState game = NewGame(19);
            game.Enemies.Clear();

            Vec2I spot = FirstOpenNeighbour(game);
            var kobold = new Enemy(EnemyCatalog.KoboldThief, 1, spot);
            kobold.Hp = 1;
            kobold.IsAware = true;
            kobold.LastKnownPlayerPosition = game.Player.Position;
            game.Enemies.Add(kobold);

            Assert.IsTrue(kobold.IsFleeing);
            int before = Vec2I.ChebyshevDistance(kobold.Position, game.Player.Position);
            game.PlayerWait();
            int after = Vec2I.ChebyshevDistance(kobold.Position, game.Player.Position);

            Assert.Greater(after, before, "a fleeing kobold should break away");
        }

        static Vec2I FirstOpenNeighbour(GameState game)
        {
            for (int i = 0; i < Vec2I.Directions8.Length; i++)
            {
                Vec2I candidate = game.Player.Position + Vec2I.Directions8[i];
                if (game.Dungeon.IsWalkable(candidate) && game.EnemyAt(candidate) == null) return candidate;
            }
            Assert.Fail("player is walled in");
            return Vec2I.Zero;
        }
    }
}
