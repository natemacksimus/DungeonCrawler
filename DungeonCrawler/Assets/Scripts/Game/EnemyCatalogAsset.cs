using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// The enemy roster for a run: an ordered list of <see cref="EnemyArchetypeAsset"/> references.
    /// Assign this to <see cref="GameBootstrap"/> to replace the built-in five-enemy roster (see
    /// <see cref="DungeonCrawler.Core.EnemyCatalog.All"/>) with no code changes — add, remove or
    /// reorder entries in the Inspector and the depth-weighted spawn table follows automatically.
    /// Leaving GameBootstrap's field empty keeps the built-in roster.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "Dungeon Crawler/Enemy Catalog")]
    public sealed class EnemyCatalogAsset : ScriptableObject
    {
        public List<EnemyArchetypeAsset> Enemies = new List<EnemyArchetypeAsset>();

        /// <summary>Converts every wired-in entry into the plain data the simulation reads. Empty slots are skipped.</summary>
        public List<EnemyArchetype> BuildRoster()
        {
            var roster = new List<EnemyArchetype>(Enemies.Count);
            for (int i = 0; i < Enemies.Count; i++)
            {
                if (Enemies[i] != null) roster.Add(Enemies[i].ToArchetype());
            }
            return roster;
        }

        /// <summary>
        /// Enemy name to custom sprite, for whichever entries have one wired in. Keyed by name (not
        /// reference) because the simulation only ever sees the plain <see cref="EnemyArchetype"/> that
        /// <see cref="EnemyArchetypeAsset.ToArchetype"/> builds, not this asset.
        /// </summary>
        public Dictionary<string, Sprite> BuildSpriteLookup()
        {
            var lookup = new Dictionary<string, Sprite>();
            for (int i = 0; i < Enemies.Count; i++)
            {
                EnemyArchetypeAsset asset = Enemies[i];
                if (asset == null || asset.Sprite == null) continue;
                lookup[asset.EnemyName] = asset.Sprite;
            }
            return lookup;
        }
    }
}
