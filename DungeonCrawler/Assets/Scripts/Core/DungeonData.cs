using System.Collections.Generic;

namespace DungeonCrawler.Core
{
    /// <summary>
    /// The immutable-ish output of the generator: a tile grid plus the room list and spawn points.
    /// Every other system reads the map through this object.
    /// </summary>
    public sealed class DungeonData
    {
        readonly TileType[,] _tiles;

        public DungeonData(int width, int height)
        {
            Width = width;
            Height = height;
            _tiles = new TileType[width, height];
            Rooms = new List<Room>();
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public List<Room> Rooms { get; private set; }
        public Vec2I SpawnPoint { get; set; }
        public Vec2I StairsDown { get; set; }
        public Vec2I StairsUp { get; set; }

        public TileType this[int x, int y]
        {
            get { return _tiles[x, y]; }
            set { _tiles[x, y] = value; }
        }

        public TileType this[Vec2I p]
        {
            get { return _tiles[p.X, p.Y]; }
            set { _tiles[p.X, p.Y] = value; }
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public bool InBounds(Vec2I p) { return InBounds(p.X, p.Y); }

        public bool IsWalkable(int x, int y)
        {
            return InBounds(x, y) && _tiles[x, y].IsWalkable();
        }

        public bool IsWalkable(Vec2I p) { return IsWalkable(p.X, p.Y); }

        public bool BlocksSight(int x, int y)
        {
            return !InBounds(x, y) || _tiles[x, y].BlocksSight();
        }

        public int CountTiles(TileType type)
        {
            int n = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_tiles[x, y] == type) n++;
            return n;
        }

        public void Fill(TileType type)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    _tiles[x, y] = type;
        }

        /// <summary>Which room contains this tile, or -1 for corridors.</summary>
        public int RoomIndexAt(Vec2I p)
        {
            for (int i = 0; i < Rooms.Count; i++)
                if (Rooms[i].Contains(p)) return i;
            return -1;
        }

        /// <summary>ASCII dump — used by tests and log output to inspect a floor without the editor.</summary>
        public string ToAscii()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = new Vec2I(x, y);
                    if (p == SpawnPoint) { sb.Append('@'); continue; }
                    switch (_tiles[x, y])
                    {
                        case TileType.Wall: sb.Append('#'); break;
                        case TileType.Floor: sb.Append('.'); break;
                        case TileType.Door: sb.Append('+'); break;
                        case TileType.StairsDown: sb.Append('>'); break;
                        case TileType.StairsUp: sb.Append('<'); break;
                    }
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
