using System;
using System.Collections.Generic;

namespace DungeonCrawler.Core
{
    /// <summary>Tuning knobs for <see cref="DungeonGenerator"/>. Values scale mildly with depth.</summary>
    public sealed class DungeonSettings
    {
        public int Width = 61;
        public int Height = 41;
        public int RoomAttempts = 80;
        public int MaxRooms = 12;
        public int MinRoomSize = 5;
        public int MaxRoomSize = 11;
        public int RoomPadding = 1;
        public double DoorChance = 0.45;

        public static DungeonSettings ForDepth(int depth)
        {
            // Deeper floors are a little larger and more subdivided.
            int d = Math.Min(Math.Max(depth, 1), 8);
            return new DungeonSettings
            {
                Width = 55 + d * 2,
                Height = 37 + d,
                MaxRooms = 8 + d,
                RoomAttempts = 80
            };
        }
    }

    /// <summary>
    /// Randomized room-and-corridor generator: scatter non-overlapping rectangles, carve them,
    /// then join consecutive room centers with L-shaped corridors. Connected by construction, and
    /// <see cref="DungeonValidator"/> proves it on every floor the game builds.
    /// </summary>
    public static class DungeonGenerator
    {
        public static DungeonData Generate(int depth, Rng rng, DungeonSettings settings = null)
        {
            settings = settings ?? DungeonSettings.ForDepth(depth);

            var dungeon = new DungeonData(settings.Width, settings.Height);
            dungeon.Fill(TileType.Wall);

            PlaceRooms(dungeon, rng, settings);
            if (dungeon.Rooms.Count == 0)
            {
                // Degenerate settings: carve one guaranteed room so the floor is still playable.
                var fallback = new Room(1, 1, Math.Min(7, settings.Width - 2), Math.Min(7, settings.Height - 2));
                dungeon.Rooms.Add(fallback);
                CarveRoom(dungeon, fallback);
            }

            ConnectRooms(dungeon, rng, settings);
            PlaceStairs(dungeon, rng);
            return dungeon;
        }

        static void PlaceRooms(DungeonData dungeon, Rng rng, DungeonSettings s)
        {
            for (int attempt = 0; attempt < s.RoomAttempts && dungeon.Rooms.Count < s.MaxRooms; attempt++)
            {
                int w = rng.RangeInclusive(s.MinRoomSize, s.MaxRoomSize);
                int h = rng.RangeInclusive(s.MinRoomSize, Math.Max(s.MinRoomSize, s.MaxRoomSize - 3));
                if (w + 2 >= dungeon.Width || h + 2 >= dungeon.Height) continue;

                int x = rng.RangeInclusive(1, dungeon.Width - w - 2);
                int y = rng.RangeInclusive(1, dungeon.Height - h - 2);
                var room = new Room(x, y, w, h);

                bool clear = true;
                for (int i = 0; i < dungeon.Rooms.Count; i++)
                {
                    if (room.Overlaps(dungeon.Rooms[i], s.RoomPadding)) { clear = false; break; }
                }
                if (!clear) continue;

                dungeon.Rooms.Add(room);
                CarveRoom(dungeon, room);
            }
        }

        static void CarveRoom(DungeonData dungeon, Room room)
        {
            for (int y = room.Y; y <= room.MaxY; y++)
                for (int x = room.X; x <= room.MaxX; x++)
                    dungeon[x, y] = TileType.Floor;
        }

        static void ConnectRooms(DungeonData dungeon, Rng rng, DungeonSettings s)
        {
            // Sort by center X so consecutive corridors stay short and readable.
            dungeon.Rooms.Sort((a, b) => a.Center.X.CompareTo(b.Center.X));

            var carved = new List<Vec2I>();
            for (int i = 1; i < dungeon.Rooms.Count; i++)
            {
                Vec2I from = dungeon.Rooms[i - 1].Center;
                Vec2I to = dungeon.Rooms[i].Center;
                CarveLCorridor(dungeon, from, to, rng.Chance(0.5), carved);
            }

            // One extra connection keeps floors from being pure trees, which navigate badly.
            if (dungeon.Rooms.Count > 3)
            {
                int a = rng.Range(0, dungeon.Rooms.Count);
                int b = rng.Range(0, dungeon.Rooms.Count);
                if (a != b)
                {
                    CarveLCorridor(dungeon, dungeon.Rooms[a].Center, dungeon.Rooms[b].Center, rng.Chance(0.5), carved);
                }
            }

            PlaceDoors(dungeon, rng, s, carved);
        }

        static void CarveLCorridor(DungeonData dungeon, Vec2I from, Vec2I to, bool horizontalFirst, List<Vec2I> carved)
        {
            Vec2I corner = horizontalFirst ? new Vec2I(to.X, from.Y) : new Vec2I(from.X, to.Y);
            CarveLine(dungeon, from, corner, carved);
            CarveLine(dungeon, corner, to, carved);
        }

        static void CarveLine(DungeonData dungeon, Vec2I from, Vec2I to, List<Vec2I> carved)
        {
            int dx = Math.Sign(to.X - from.X);
            int dy = Math.Sign(to.Y - from.Y);
            var p = from;
            while (true)
            {
                if (dungeon.InBounds(p) && dungeon[p] == TileType.Wall)
                {
                    dungeon[p] = TileType.Floor;
                    carved.Add(p);
                }
                if (p == to) break;
                p = new Vec2I(p.X + dx, p.Y + dy);
            }
        }

        /// <summary>
        /// A corridor tile sitting in a one-tile-wide pinch next to a room becomes a door. Purely
        /// cosmetic here (doors are walkable and transparent) but it reads as architecture.
        /// </summary>
        static void PlaceDoors(DungeonData dungeon, Rng rng, DungeonSettings s, List<Vec2I> carved)
        {
            for (int i = 0; i < carved.Count; i++)
            {
                var p = carved[i];
                if (dungeon[p] != TileType.Floor) continue;
                if (dungeon.RoomIndexAt(p) >= 0) continue;
                if (!IsCorridorPinch(dungeon, p)) continue;

                bool touchesRoom = false;
                for (int d = 0; d < Vec2I.Directions4.Length; d++)
                {
                    var n = p + Vec2I.Directions4[d];
                    if (dungeon.InBounds(n) && dungeon[n] == TileType.Floor && dungeon.RoomIndexAt(n) >= 0)
                    {
                        touchesRoom = true;
                        break;
                    }
                }
                if (touchesRoom && rng.Chance(s.DoorChance)) dungeon[p] = TileType.Door;
            }
        }

        static bool IsCorridorPinch(DungeonData dungeon, Vec2I p)
        {
            bool wallsAbove = dungeon.BlocksSight(p.X, p.Y + 1) && dungeon.BlocksSight(p.X, p.Y - 1);
            bool wallsBeside = dungeon.BlocksSight(p.X + 1, p.Y) && dungeon.BlocksSight(p.X - 1, p.Y);
            return wallsAbove ^ wallsBeside;
        }

        /// <summary>Spawn in the first room; stairs down in the room whose center is farthest by BFS.</summary>
        static void PlaceStairs(DungeonData dungeon, Rng rng)
        {
            Room spawnRoom = dungeon.Rooms[0];
            Vec2I spawn = spawnRoom.Center;
            dungeon.SpawnPoint = spawn;
            dungeon.StairsUp = spawn;
            dungeon[spawn] = TileType.StairsUp;

            int[,] dist = Pathfinding.BfsDistanceField(dungeon, spawn);
            Vec2I best = spawn;
            int bestDist = -1;
            for (int i = 1; i < dungeon.Rooms.Count; i++)
            {
                Vec2I c = dungeon.Rooms[i].Center;
                int d = dist[c.X, c.Y];
                if (d > bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            if (bestDist <= 0)
            {
                // Single-room floor: any other interior tile will do.
                for (int tries = 0; tries < 64; tries++)
                {
                    Vec2I candidate = spawnRoom.RandomInteriorTile(rng);
                    if (candidate != spawn) { best = candidate; break; }
                }
            }

            dungeon.StairsDown = best;
            dungeon[best] = TileType.StairsDown;
        }
    }
}
