namespace DungeonCrawler.Core
{
    public enum TileType
    {
        Wall = 0,
        Floor = 1,
        Door = 2,
        StairsDown = 3,
        StairsUp = 4
    }

    public static class TileTypeExtensions
    {
        /// <summary>Can an actor stand on this tile?</summary>
        public static bool IsWalkable(this TileType tile)
        {
            return tile != TileType.Wall;
        }

        /// <summary>Does this tile block line of sight? Closed doors do; this game keeps doors open.</summary>
        public static bool BlocksSight(this TileType tile)
        {
            return tile == TileType.Wall;
        }
    }
}
