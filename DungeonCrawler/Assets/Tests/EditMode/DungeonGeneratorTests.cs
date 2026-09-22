using NUnit.Framework;
using DungeonCrawler.Core;

namespace DungeonCrawler.Tests
{
    public class DungeonGeneratorTests
    {
        [Test]
        public void EveryFloorOfEverySeedIsFullyConnected()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var rng = new Rng(seed);
                for (int depth = 1; depth <= 8; depth++)
                {
                    DungeonData dungeon = DungeonGenerator.Generate(depth, rng);
                    ValidationReport report = DungeonValidator.Validate(dungeon);
                    Assert.IsTrue(report.IsValid,
                        "seed " + seed + " depth " + depth + ": " + report + "\n" + dungeon.ToAscii());
                }
            }
        }

        [Test]
        public void GenerationIsDeterministicForAGivenSeed()
        {
            DungeonData first = DungeonGenerator.Generate(3, new Rng(9876));
            DungeonData second = DungeonGenerator.Generate(3, new Rng(9876));

            Assert.AreEqual(first.ToAscii(), second.ToAscii());
            Assert.AreEqual(first.StairsDown, second.StairsDown);
            Assert.AreEqual(first.SpawnPoint, second.SpawnPoint);
        }

        [Test]
        public void StairsAreDistinctAndWalkable()
        {
            for (int seed = 100; seed < 120; seed++)
            {
                DungeonData dungeon = DungeonGenerator.Generate(2, new Rng(seed));
                Assert.AreNotEqual(dungeon.SpawnPoint, dungeon.StairsDown, "seed " + seed);
                Assert.IsTrue(dungeon.IsWalkable(dungeon.StairsDown), "seed " + seed);
                Assert.IsTrue(dungeon.IsWalkable(dungeon.SpawnPoint), "seed " + seed);
                Assert.AreEqual(TileType.StairsDown, dungeon[dungeon.StairsDown], "seed " + seed);
            }
        }

        [Test]
        public void StairsDownIsFarFromSpawn()
        {
            // The generator picks the farthest room by BFS, so the walk should never be trivial.
            for (int seed = 200; seed < 215; seed++)
            {
                DungeonData dungeon = DungeonGenerator.Generate(4, new Rng(seed));
                int[,] dist = Pathfinding.BfsDistanceField(dungeon, dungeon.SpawnPoint);
                int stairsDistance = dist[dungeon.StairsDown.X, dungeon.StairsDown.Y];
                Assert.Greater(stairsDistance, 8, "seed " + seed + " put the stairs next door");
            }
        }

        [Test]
        public void RoomsDoNotOverlap()
        {
            DungeonData dungeon = DungeonGenerator.Generate(5, new Rng(4242));
            for (int i = 0; i < dungeon.Rooms.Count; i++)
            {
                for (int j = i + 1; j < dungeon.Rooms.Count; j++)
                {
                    Assert.IsFalse(dungeon.Rooms[i].Overlaps(dungeon.Rooms[j], 0),
                        dungeon.Rooms[i] + " overlaps " + dungeon.Rooms[j]);
                }
            }
        }

        [Test]
        public void MapIsEnclosedByWalls()
        {
            DungeonData dungeon = DungeonGenerator.Generate(1, new Rng(77));
            for (int x = 0; x < dungeon.Width; x++)
            {
                Assert.AreEqual(TileType.Wall, dungeon[x, 0]);
                Assert.AreEqual(TileType.Wall, dungeon[x, dungeon.Height - 1]);
            }
            for (int y = 0; y < dungeon.Height; y++)
            {
                Assert.AreEqual(TileType.Wall, dungeon[0, y]);
                Assert.AreEqual(TileType.Wall, dungeon[dungeon.Width - 1, y]);
            }
        }

        [Test]
        public void ValidatorRejectsAnIsolatedPocket()
        {
            // A hand-built map with a sealed room must fail, or the validator proves nothing.
            var dungeon = new DungeonData(20, 12);
            dungeon.Fill(TileType.Wall);
            for (int x = 1; x <= 5; x++)
                for (int y = 1; y <= 5; y++)
                    dungeon[x, y] = TileType.Floor;
            dungeon[15, 8] = TileType.Floor; // Sealed pocket.

            dungeon.Rooms.Add(new Room(1, 1, 5, 5));
            dungeon.SpawnPoint = new Vec2I(2, 2);
            dungeon.StairsDown = new Vec2I(4, 4);

            ValidationReport report = DungeonValidator.Validate(dungeon);
            Assert.IsFalse(report.IsValid);
            Assert.IsFalse(report.AllWalkableReachable);
            Assert.AreEqual(1, report.OrphanTiles.Count);
        }
    }
}
