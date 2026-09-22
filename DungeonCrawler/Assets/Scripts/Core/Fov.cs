namespace DungeonCrawler.Core
{
    /// <summary>
    /// Field of view. A tile is visible when an unobstructed Bresenham line reaches it from the
    /// origin: symmetric, cheap at the radii this game uses, and easy to assert on in tests.
    /// </summary>
    public static class Fov
    {
        public static void Compute(DungeonData dungeon, Vec2I origin, int radius, bool[,] visible, bool[,] explored = null)
        {
            for (int y = 0; y < dungeon.Height; y++)
                for (int x = 0; x < dungeon.Width; x++)
                    visible[x, y] = false;

            if (!dungeon.InBounds(origin)) return;

            visible[origin.X, origin.Y] = true;
            if (explored != null) explored[origin.X, origin.Y] = true;

            int r2 = radius * radius;
            for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
            {
                for (int x = origin.X - radius; x <= origin.X + radius; x++)
                {
                    if (!dungeon.InBounds(x, y)) continue;
                    int dx = x - origin.X;
                    int dy = y - origin.Y;
                    if (dx * dx + dy * dy > r2) continue;
                    if (visible[x, y]) continue;
                    if (!HasLineOfSight(dungeon, origin, new Vec2I(x, y))) continue;

                    visible[x, y] = true;
                    if (explored != null) explored[x, y] = true;
                }
            }
        }

        /// <summary>
        /// True when nothing strictly between the two tiles blocks sight. The endpoints never block,
        /// so you can always see the wall you are looking at.
        /// </summary>
        public static bool HasLineOfSight(DungeonData dungeon, Vec2I from, Vec2I to)
        {
            int x = from.X, y = from.Y;
            int dx = System.Math.Abs(to.X - x), dy = System.Math.Abs(to.Y - y);
            int sx = to.X > x ? 1 : -1, sy = to.Y > y ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x == to.X && y == to.Y) return true;

                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }

                if (x == to.X && y == to.Y) return true;
                if (dungeon.BlocksSight(x, y)) return false;
            }
        }

        public static bool WithinRange(Vec2I a, Vec2I b, int range)
        {
            return Vec2I.ChebyshevDistance(a, b) <= range;
        }
    }
}
