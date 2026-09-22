using System.Collections.Generic;
using System.Text;

namespace DungeonCrawler.Core
{
    /// <summary>Outcome of validating one generated floor.</summary>
    public sealed class ValidationReport
    {
        public bool SpawnWalkable;
        public bool StairsDownWalkable;
        public bool StairsDownReachable;
        public int WalkableTiles;
        public int ReachableTiles;
        public int RoomCount;
        public readonly List<Vec2I> OrphanTiles = new List<Vec2I>();

        public bool AllWalkableReachable { get { return OrphanTiles.Count == 0; } }

        public bool IsValid
        {
            get
            {
                return SpawnWalkable && StairsDownWalkable && StairsDownReachable
                       && AllWalkableReachable && RoomCount > 0 && WalkableTiles > 0;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(IsValid ? "VALID" : "INVALID");
            sb.Append(" rooms=").Append(RoomCount);
            sb.Append(" walkable=").Append(WalkableTiles);
            sb.Append(" reachable=").Append(ReachableTiles);
            sb.Append(" spawnOk=").Append(SpawnWalkable);
            sb.Append(" stairsWalkable=").Append(StairsDownWalkable);
            sb.Append(" stairsReachable=").Append(StairsDownReachable);
            if (OrphanTiles.Count > 0) sb.Append(" orphans=").Append(OrphanTiles.Count);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Flood-fills from the spawn point and proves every walkable tile — the stairs above all — can
    /// be reached. The game runs this on every floor it generates, so a soft-locked map fails loudly
    /// instead of quietly trapping the player.
    /// </summary>
    public static class DungeonValidator
    {
        public static ValidationReport Validate(DungeonData dungeon)
        {
            var report = new ValidationReport
            {
                RoomCount = dungeon.Rooms.Count,
                SpawnWalkable = dungeon.IsWalkable(dungeon.SpawnPoint),
                StairsDownWalkable = dungeon.IsWalkable(dungeon.StairsDown)
            };

            int[,] dist = Pathfinding.BfsDistanceField(dungeon, dungeon.SpawnPoint);

            for (int y = 0; y < dungeon.Height; y++)
            {
                for (int x = 0; x < dungeon.Width; x++)
                {
                    if (!dungeon[x, y].IsWalkable()) continue;
                    report.WalkableTiles++;
                    if (dist[x, y] != Pathfinding.Unreachable) report.ReachableTiles++;
                    else if (report.OrphanTiles.Count < 32) report.OrphanTiles.Add(new Vec2I(x, y));
                }
            }

            report.StairsDownReachable = report.StairsDownWalkable
                && dist[dungeon.StairsDown.X, dungeon.StairsDown.Y] != Pathfinding.Unreachable;
            return report;
        }
    }
}
