using NUnit.Framework;
using DungeonCrawler.Core;

namespace DungeonCrawler.Tests
{
    public class FovAndPathfindingTests
    {
        /// <summary>An open room with a wall pillar, built by hand so expectations are exact.</summary>
        static DungeonData OpenRoom(int width = 15, int height = 11)
        {
            var dungeon = new DungeonData(width, height);
            dungeon.Fill(TileType.Wall);
            for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                    dungeon[x, y] = TileType.Floor;
            dungeon.Rooms.Add(new Room(1, 1, width - 2, height - 2));
            dungeon.SpawnPoint = new Vec2I(2, 2);
            dungeon.StairsDown = new Vec2I(width - 3, height - 3);
            return dungeon;
        }

        [Test]
        public void OpenGroundIsFullyVisibleWithinTheRadius()
        {
            DungeonData dungeon = OpenRoom();
            var visible = new bool[dungeon.Width, dungeon.Height];
            var origin = new Vec2I(7, 5);

            Fov.Compute(dungeon, origin, 4, visible);

            Assert.IsTrue(visible[origin.X, origin.Y]);
            Assert.IsTrue(visible[origin.X + 3, origin.Y]);
            Assert.IsTrue(visible[origin.X, origin.Y + 3]);
            Assert.IsFalse(visible[origin.X + 5, origin.Y], "beyond the radius");
        }

        [Test]
        public void WallsBlockSightBehindThem()
        {
            DungeonData dungeon = OpenRoom();
            var origin = new Vec2I(3, 5);
            dungeon[5, 5] = TileType.Wall;

            var visible = new bool[dungeon.Width, dungeon.Height];
            Fov.Compute(dungeon, origin, 8, visible);

            Assert.IsTrue(visible[5, 5], "you can see the wall itself");
            Assert.IsFalse(visible[6, 5], "but not the tile directly behind it");
            Assert.IsFalse(visible[7, 5]);
        }

        [Test]
        public void LineOfSightIsSymmetric()
        {
            DungeonData dungeon = OpenRoom();
            dungeon[7, 5] = TileType.Wall;

            var a = new Vec2I(3, 5);
            var b = new Vec2I(11, 5);
            Assert.AreEqual(Fov.HasLineOfSight(dungeon, a, b), Fov.HasLineOfSight(dungeon, b, a));
            Assert.IsFalse(Fov.HasLineOfSight(dungeon, a, b));
        }

        [Test]
        public void ExploredMemoryAccumulatesAcrossViewpoints()
        {
            DungeonData dungeon = OpenRoom();
            var visible = new bool[dungeon.Width, dungeon.Height];
            var explored = new bool[dungeon.Width, dungeon.Height];

            Fov.Compute(dungeon, new Vec2I(2, 2), 3, visible, explored);
            Assert.IsTrue(explored[2, 3]);

            Fov.Compute(dungeon, new Vec2I(12, 8), 3, visible, explored);
            Assert.IsFalse(visible[2, 3], "no longer in view");
            Assert.IsTrue(explored[2, 3], "but still remembered");
            Assert.IsTrue(explored[12, 7]);
        }

        [Test]
        public void DistanceFieldMeasuresEightDirectionalSteps()
        {
            DungeonData dungeon = OpenRoom();
            int[,] dist = Pathfinding.BfsDistanceField(dungeon, new Vec2I(1, 1));

            Assert.AreEqual(0, dist[1, 1]);
            Assert.AreEqual(1, dist[2, 2], "diagonals cost one step");
            Assert.AreEqual(4, dist[5, 3]);
            Assert.AreEqual(Pathfinding.Unreachable, dist[0, 0], "walls are never reached");
        }

        [Test]
        public void FirstStepMovesTowardTheTarget()
        {
            DungeonData dungeon = OpenRoom();
            var from = new Vec2I(2, 2);
            var to = new Vec2I(10, 2);

            Vec2I step = Pathfinding.FirstStepToward(dungeon, from, to);

            // In the open there are several equally short first steps, so assert progress, not a
            // specific tile.
            Assert.AreEqual(1, step.X, "must head east");
            Assert.IsTrue(dungeon.IsWalkable(from + step));
            Assert.Less(Vec2I.ChebyshevDistance(from + step, to), Vec2I.ChebyshevDistance(from, to));
        }

        [Test]
        public void FirstStepRoutesAroundAWall()
        {
            DungeonData dungeon = OpenRoom();
            // Seal a vertical wall with a single gap at the top.
            for (int y = 1; y <= 7; y++) dungeon[7, y] = TileType.Wall;

            var from = new Vec2I(3, 2);
            var to = new Vec2I(11, 2);
            Vec2I step = Pathfinding.FirstStepToward(dungeon, from, to);

            Assert.AreNotEqual(Vec2I.Zero, step, "a path exists through the gap");
            Assert.IsTrue(dungeon.IsWalkable(from + step));

            // Following the path must eventually arrive.
            var at = from;
            for (int i = 0; i < 200 && at != to; i++)
            {
                Vec2I next = Pathfinding.FirstStepToward(dungeon, at, to);
                Assert.AreNotEqual(Vec2I.Zero, next, "path stalled at " + at);
                at = at + next;
            }
            Assert.AreEqual(to, at);
        }

        [Test]
        public void NoPathReturnsZero()
        {
            DungeonData dungeon = OpenRoom();
            for (int y = 0; y < dungeon.Height; y++) dungeon[7, y] = TileType.Wall;

            Vec2I step = Pathfinding.FirstStepToward(dungeon, new Vec2I(3, 2), new Vec2I(11, 2));
            Assert.AreEqual(Vec2I.Zero, step);
        }

        [Test]
        public void BlockedTilesAreRoutedAround()
        {
            DungeonData dungeon = OpenRoom();
            var from = new Vec2I(2, 5);
            var to = new Vec2I(6, 5);

            // Block the three tiles straight ahead; the step must leave the straight line.
            var blocked = new System.Collections.Generic.HashSet<Vec2I>
            {
                new Vec2I(3, 5), new Vec2I(4, 5), new Vec2I(5, 5)
            };

            Vec2I step = Pathfinding.FirstStepToward(dungeon, from, to, blocked);
            Assert.AreNotEqual(Vec2I.Zero, step);
            Assert.IsFalse(blocked.Contains(from + step), "stepped onto a blocked tile");
        }
    }
}
