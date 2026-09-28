using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// The item roster for a run: an ordered list of <see cref="ItemArchetypeAsset"/> references.
    /// Assign this to <see cref="GameBootstrap"/> to replace the built-in catalog (see
    /// <see cref="DungeonCrawler.Core.ItemCatalog.All"/>) with no code changes — add, remove or reorder
    /// entries in the Inspector and the depth-weighted loot table follows automatically. Leaving
    /// GameBootstrap's field empty keeps the built-in catalog.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Dungeon Crawler/Item Catalog")]
    public sealed class ItemCatalogAsset : ScriptableObject
    {
        public List<ItemArchetypeAsset> Items = new List<ItemArchetypeAsset>();

        /// <summary>Converts every wired-in entry into the plain data the simulation reads. Empty slots are skipped.</summary>
        public List<ItemDef> BuildRoster()
        {
            var roster = new List<ItemDef>(Items.Count);
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] != null) roster.Add(Items[i].ToItemDef());
            }
            return roster;
        }

        /// <summary>
        /// Item name to custom sprite, for whichever entries have one wired in. Keyed by name (not
        /// reference) because the simulation only ever sees the plain <see cref="ItemDef"/> that
        /// <see cref="ItemArchetypeAsset.ToItemDef"/> builds, not this asset.
        /// </summary>
        public Dictionary<string, Sprite> BuildSpriteLookup()
        {
            var lookup = new Dictionary<string, Sprite>();
            for (int i = 0; i < Items.Count; i++)
            {
                ItemArchetypeAsset asset = Items[i];
                if (asset == null || asset.Sprite == null) continue;
                lookup[asset.ItemName] = asset.Sprite;
            }
            return lookup;
        }
    }
}
