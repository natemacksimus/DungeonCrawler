using System.Collections.Generic;

namespace DungeonCrawler.Core
{
    /// <summary>Grid BFS helpers. Maps are small and actors are few, so plain BFS beats a subtle A*.</summary>
    public static class Pathfinding
    {
        public const int Unreachable = -1;

        /// <summary>Breadth-first step distance from <paramref name="origin"/> to every walkable tile.</summary>
        public static int[,] BfsDistanceField(DungeonData dungeon, Vec2I origin, bool allowDiagonal = true)
        {
            var dist = new int[dungeon.Width, dungeon.Height];
            for (int y = 0; y < dungeon.Height; y++)
                for (int x = 0; x < dungeon.Width; x++)
                    dist[x, y] = Unreachable;

            if (!dungeon.IsWalkable(origin)) return dist;

            var dirs = allowDiagonal ? Vec2I.Directions8 : Vec2I.Directions4;
            var queue = new Queue<Vec2I>();
            dist[origin.X, origin.Y] = 0;
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                Vec2I cur = queue.Dequeue();
                int next = dist[cur.X, cur.Y] + 1;
                for (int i = 0; i < dirs.Length; i++)
                {
                    Vec2I n = cur + dirs[i];
                    if (!dungeon.IsWalkable(n)) continue;
                    if (dist[n.X, n.Y] != Unreachable) continue;
                    dist[n.X, n.Y] = next;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>
        /// First step of a shortest path from <paramref name="from"/> to <paramref name="to"/>, treating
        /// <paramref name="blocked"/> tiles as impassable (other actors). The search runs backwards from
        /// the target so the answer falls out as soon as it touches the start. Returns
        /// <see cref="Vec2I.Zero"/> when there is no path.
        /// </summary>
        public static Vec2I FirstStepToward(DungeonData dungeon, Vec2I from, Vec2I to, ICollection<Vec2I> blocked = null, int maxNodes = 4096)
        {
            if (from == to) return Vec2I.Zero;

            var seen = new HashSet<Vec2I>();
            var queue = new Queue<Vec2I>();
            queue.Enqueue(to);
            seen.Add(to);
            int expanded = 0;

            while (queue.Count > 0 && expanded++ < maxNodes)
            {
                Vec2I cur = queue.Dequeue();
                for (int i = 0; i < Vec2I.Directions8.Length; i++)
                {
                    Vec2I n = cur + Vec2I.Directions8[i];
                    if (n == from)
                    {
                        // cur neighbours the start, so stepping onto it advances along a shortest path.
                        return new Vec2I(cur.X - from.X, cur.Y - from.Y);
                    }
                    if (seen.Contains(n)) continue;
                    if (!dungeon.IsWalkable(n)) continue;
                    seen.Add(n);
                    if (blocked != null && blocked.Contains(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return Vec2I.Zero;
        }
    }
}
