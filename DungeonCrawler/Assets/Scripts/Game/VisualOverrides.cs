using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// Optional artwork that replaces the built-in procedural shapes and baked tile colours. Every
    /// field defaults to empty, and <see cref="DungeonView"/> falls back to <see cref="SpriteFactory"/>
    /// shapes and <see cref="Palette"/> colours wherever a field here is left unassigned, so dropping
    /// this asset into the scene with nothing wired up changes nothing.
    ///
    /// Wire it up by creating an instance via Assets &gt; Create &gt; Dungeon Crawler &gt; Visual
    /// Overrides, dragging in sprites/textures, and assigning that asset to
    /// <see cref="GameBootstrap"/>'s Visuals field. Tile textures are sampled with
    /// <see cref="Texture2D.GetPixelBilinear"/>, which requires "Read/Write Enabled" in the texture's
    /// import settings.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualOverrides", menuName = "Dungeon Crawler/Visual Overrides")]
    public sealed class VisualOverrides : ScriptableObject
    {
        [Header("Tiles (sampled into the baked floor texture)")]
        public Texture2D WallTexture;
        public Texture2D FloorTexture;
        public Texture2D DoorTexture;

        [Header("Player")]
        public Sprite PlayerSprite;

        [Header("Enemies")]
        public Sprite GiantRatSprite;
        public Sprite GoblinSprite;
        public Sprite KoboldThiefSprite;
        public Sprite SkeletonArcherSprite;
        public Sprite OrcBruteSprite;

        [Header("Items (one sprite per kind, matching the default shape-per-kind look)")]
        public Sprite PotionSprite;
        public Sprite ScrollSprite;
        public Sprite WeaponSprite;
        public Sprite ArmorSprite;
        public Sprite GoldSprite;

        [Header("Stairs")]
        public Sprite StairsDownSprite;
        public Sprite StairsUpSprite;

        public Texture2D TextureForTile(TileType tile)
        {
            switch (tile)
            {
                case TileType.Wall: return WallTexture;
                case TileType.Door: return DoorTexture;
                default: return FloorTexture;
            }
        }

        public Sprite SpriteForEnemy(EnemyArchetype archetype)
        {
            if (archetype == EnemyCatalog.GiantRat) return GiantRatSprite;
            if (archetype == EnemyCatalog.Goblin) return GoblinSprite;
            if (archetype == EnemyCatalog.KoboldThief) return KoboldThiefSprite;
            if (archetype == EnemyCatalog.SkeletonArcher) return SkeletonArcherSprite;
            if (archetype == EnemyCatalog.OrcBrute) return OrcBruteSprite;
            return null;
        }

        public Sprite SpriteForItem(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return PotionSprite;
                case ItemKind.Scroll: return ScrollSprite;
                case ItemKind.Weapon: return WeaponSprite;
                case ItemKind.Armor: return ArmorSprite;
                default: return GoldSprite;
            }
        }
    }
}
