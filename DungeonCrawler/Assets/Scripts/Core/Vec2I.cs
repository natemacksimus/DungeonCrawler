using System;

namespace DungeonCrawler.Core
{
    /// <summary>Integer grid coordinate. Engine-free so the core simulation stays unit-testable.</summary>
    public struct Vec2I : IEquatable<Vec2I>
    {
        public int X;
        public int Y;

        public Vec2I(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2I Zero = new Vec2I(0, 0);

        /// <summary>The eight grid directions, starting north and going clockwise.</summary>
        public static readonly Vec2I[] Directions8 =
        {
            new Vec2I(0, 1), new Vec2I(1, 1), new Vec2I(1, 0), new Vec2I(1, -1),
            new Vec2I(0, -1), new Vec2I(-1, -1), new Vec2I(-1, 0), new Vec2I(-1, 1)
        };

        public static readonly Vec2I[] Directions4 =
        {
            new Vec2I(0, 1), new Vec2I(1, 0), new Vec2I(0, -1), new Vec2I(-1, 0)
        };

        /// <summary>Chebyshev distance: the number of 8-directional steps between two tiles.</summary>
        public static int ChebyshevDistance(Vec2I a, Vec2I b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return dx > dy ? dx : dy;
        }

        public static int ManhattanDistance(Vec2I a, Vec2I b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        public static Vec2I operator +(Vec2I a, Vec2I b) { return new Vec2I(a.X + b.X, a.Y + b.Y); }
        public static Vec2I operator -(Vec2I a, Vec2I b) { return new Vec2I(a.X - b.X, a.Y - b.Y); }
        public static bool operator ==(Vec2I a, Vec2I b) { return a.X == b.X && a.Y == b.Y; }
        public static bool operator !=(Vec2I a, Vec2I b) { return !(a == b); }

        /// <summary>Unit step (each component clamped to -1..1) pointing from this tile toward <paramref name="target"/>.</summary>
        public Vec2I StepToward(Vec2I target)
        {
            return new Vec2I(Math.Sign(target.X - X), Math.Sign(target.Y - Y));
        }

        public bool Equals(Vec2I other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is Vec2I other && Equals(other); }
        public override int GetHashCode() { return (X * 73856093) ^ (Y * 19349663); }
        public override string ToString() { return "(" + X + ", " + Y + ")"; }
    }
}
