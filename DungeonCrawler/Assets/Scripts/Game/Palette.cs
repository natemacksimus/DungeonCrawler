using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// Every colour in the game. Placeholder visuals are still a look: one cool stone palette, warm
    /// accents for things that matter (you, loot, stairs), red reserved for danger.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background = new Color32(0x0B, 0x0D, 0x12, 0xFF);

        public static readonly Color WallFace = new Color32(0x3A, 0x41, 0x55, 0xFF);
        public static readonly Color WallTop = new Color32(0x4C, 0x55, 0x6E, 0xFF);
        public static readonly Color FloorBase = new Color32(0x1A, 0x1F, 0x2B, 0xFF);
        public static readonly Color FloorSpeck = new Color32(0x24, 0x2B, 0x3A, 0xFF);
        public static readonly Color DoorWood = new Color32(0x8A, 0x5A, 0x2B, 0xFF);
        public static readonly Color StairsDown = new Color32(0xF2, 0xC0, 0x4C, 0xFF);
        public static readonly Color StairsUp = new Color32(0x6E, 0x7C, 0x99, 0xFF);

        public static readonly Color Player = new Color32(0x7F, 0xD8, 0xFF, 0xFF);
        public static readonly Color Gold = new Color32(0xF2, 0xC0, 0x4C, 0xFF);
        public static readonly Color Potion = new Color32(0x6E, 0xE7, 0x8B, 0xFF);
        public static readonly Color Scroll = new Color32(0xE8, 0xDF, 0xC0, 0xFF);
        public static readonly Color Weapon = new Color32(0xC9, 0xD4, 0xE8, 0xFF);
        public static readonly Color Armor = new Color32(0xA8, 0xB6, 0xD0, 0xFF);

        public static readonly Color EnemyWeak = new Color32(0xB4, 0x8C, 0x6E, 0xFF);
        public static readonly Color EnemyBasic = new Color32(0x8C, 0xC9, 0x5E, 0xFF);
        public static readonly Color EnemySneak = new Color32(0xD8, 0x9E, 0x4C, 0xFF);
        public static readonly Color EnemyRanged = new Color32(0xE0, 0xE6, 0xEF, 0xFF);
        public static readonly Color EnemyBrute = new Color32(0xE0, 0x5A, 0x4C, 0xFF);

        public static readonly Color HudPanel = new Color32(0x10, 0x14, 0x1C, 0xE6);
        public static readonly Color HudText = new Color32(0xD6, 0xDD, 0xEA, 0xFF);
        public static readonly Color HudDim = new Color32(0x8A, 0x93, 0xA6, 0xFF);
        public static readonly Color HpFill = new Color32(0xD8, 0x4C, 0x4C, 0xFF);
        public static readonly Color HpTrack = new Color32(0x2A, 0x1E, 0x22, 0xFF);
        public static readonly Color XpFill = new Color32(0x4C, 0x9C, 0xD8, 0xFF);

        public static Color ForEnemy(EnemyArchetype archetype)
        {
            if (archetype == EnemyCatalog.GiantRat) return EnemyWeak;
            if (archetype == EnemyCatalog.Goblin) return EnemyBasic;
            if (archetype == EnemyCatalog.KoboldThief) return EnemySneak;
            if (archetype == EnemyCatalog.SkeletonArcher) return EnemyRanged;
            if (archetype == EnemyCatalog.OrcBrute) return EnemyBrute;
            return EnemyBasic;
        }

        public static Color ForItem(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return Potion;
                case ItemKind.Scroll: return Scroll;
                case ItemKind.Weapon: return Weapon;
                case ItemKind.Armor: return Armor;
                default: return Gold;
            }
        }
    }
}
