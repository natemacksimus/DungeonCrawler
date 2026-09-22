namespace DungeonCrawler.Core
{
    /// <summary>Axis-aligned rectangular room in grid space. X/Y is the lower-left inclusive corner.</summary>
    public struct Room
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Room(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int MaxX { get { return X + Width - 1; } }
        public int MaxY { get { return Y + Height - 1; } }
        public Vec2I Center { get { return new Vec2I(X + Width / 2, Y + Height / 2); } }

        public bool Contains(Vec2I p)
        {
            return p.X >= X && p.X <= MaxX && p.Y >= Y && p.Y <= MaxY;
        }

        /// <summary>Overlap test with <paramref name="padding"/> tiles of required space between rooms.</summary>
        public bool Overlaps(Room other, int padding)
        {
            return X - padding <= other.MaxX && MaxX + padding >= other.X
                && Y - padding <= other.MaxY && MaxY + padding >= other.Y;
        }

        public Vec2I RandomInteriorTile(Rng rng)
        {
            return new Vec2I(rng.RangeInclusive(X, MaxX), rng.RangeInclusive(Y, MaxY));
        }

        public override string ToString()
        {
            return "Room(" + X + "," + Y + " " + Width + "x" + Height + ")";
        }
    }
}
