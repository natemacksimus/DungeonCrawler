using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// Optional artwork for the pieces that don't carry their own — the player (by level), the stairs,
    /// and tile art by floor. Enemy and item sprites live on their own archetype assets instead (see
    /// <see cref="EnemyArchetypeAsset"/>, <see cref="ItemArchetypeAsset"/>), since those already exist
    /// per-enemy/per-item. Every field here defaults to empty, and <see cref="DungeonView"/> falls back
    /// to <see cref="SpriteFactory"/> shapes and <see cref="Palette"/> colours wherever a field is left
    /// unassigned, so dropping this asset into the scene with nothing wired up changes nothing.
    ///
    /// Wire it up by creating an instance via Assets &gt; Create &gt; Dungeon Crawler &gt; Visual
    /// Overrides, dragging in sprites/textures, and assigning that asset to
    /// <see cref="GameBootstrap"/>'s Visuals field. Tile textures need "Read/Write Enabled" in their
    /// import settings, since they are sampled on the CPU into the baked floor texture.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualOverrides", menuName = "Dungeon Crawler/Visual Overrides")]
    public sealed class VisualOverrides : ScriptableObject
    {
        [Header("Player by level")]
        [Tooltip("Each entry takes over from its Min Level onward, until a higher-level entry takes " +
                 "over from it. Leave the list empty to keep the procedural default at every level.")]
        public List<PlayerAppearance> PlayerAppearances = new List<PlayerAppearance>();

        /// <summary>The sprite in effect at a given level: the highest MinLevel that is still &lt;= level, or null.</summary>
        public Sprite SpriteForLevel(int level)
        {
            PlayerAppearance best = null;
            for (int i = 0; i < PlayerAppearances.Count; i++)
            {
                PlayerAppearance appearance = PlayerAppearances[i];
                if (appearance == null || appearance.Sprite == null || appearance.MinLevel > level) continue;
                if (best == null || appearance.MinLevel > best.MinLevel) best = appearance;
            }
            return best != null ? best.Sprite : null;
        }

        [Header("Stairs")]
        public Sprite StairsDownSprite;
        public Sprite StairsUpSprite;

        [Header("Tiles by floor (sampled into the baked floor texture)")]
        [Tooltip("Each theme takes over from its Min Depth onward, until a deeper theme's Min Depth " +
                 "takes over from it. Leave the list empty to keep the procedural default at every depth.")]
        public List<TileTheme> TileThemes = new List<TileTheme>();

        /// <summary>The theme in effect at a given depth: the deepest MinDepth that is still &lt;= depth.</summary>
        public TileTheme ThemeForDepth(int depth)
        {
            TileTheme best = null;
            for (int i = 0; i < TileThemes.Count; i++)
            {
                TileTheme theme = TileThemes[i];
                if (theme == null || theme.MinDepth > depth) continue;
                if (best == null || theme.MinDepth > best.MinDepth) best = theme;
            }
            return best;
        }

        /// <summary>The override texture for a tile type at a given depth, or null to keep the procedural default.</summary>
        public Texture2D TextureForTile(TileType tile, int depth)
        {
            TileTheme theme = ThemeForDepth(depth);
            if (theme == null) return null;

            switch (tile)
            {
                case TileType.Wall: return theme.WallTexture;
                case TileType.Door: return theme.DoorTexture;
                default: return theme.FloorTexture;
            }
        }
    }

    /// <summary>One depth-gated set of tile art. See <see cref="VisualOverrides.TileThemes"/>.</summary>
    [Serializable]
    public sealed class TileTheme
    {
        [Tooltip("This theme takes over from this floor onward.")]
        public int MinDepth = 1;

        public Texture2D WallTexture;
        public Texture2D FloorTexture;
        public Texture2D DoorTexture;
    }

    /// <summary>One level-gated player look. See <see cref="VisualOverrides.PlayerAppearances"/>.</summary>
    [Serializable]
    public sealed class PlayerAppearance
    {
        [Tooltip("This look takes over from this character level onward.")]
        public int MinLevel = 1;

        public Sprite Sprite;
    }
}
