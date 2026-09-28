using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// Editor-authored description of one <see cref="ItemDef"/>. Every field mirrors the Core class
    /// one-to-one, so this asset is purely Inspector-editable data — the item logic still lives in
    /// <see cref="GameState"/>, which only ever sees the plain <see cref="ItemDef"/> that
    /// <see cref="ToItemDef"/> builds. Create one via Assets &gt; Create &gt; Dungeon Crawler &gt; Item
    /// Archetype, then add it to an <see cref="ItemCatalogAsset"/> to include it in a run.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemArchetype", menuName = "Dungeon Crawler/Item Archetype")]
    public sealed class ItemArchetypeAsset : ScriptableObject
    {
        [Header("Identity")]
        public string ItemName = "new item";
        public ItemKind Kind = ItemKind.Potion;

        [Tooltip("Single character used by ASCII dumps and the self-play log.")]
        public char Glyph = '?';

        [Header("Effect")]
        [Tooltip("Heal amount, stat bonus, or buff magnitude depending on Kind.")]
        public int Power;

        [Tooltip("Turns a scroll buff lasts. Only used when Kind is Scroll.")]
        public int Duration;

        [Tooltip("Which stat a scroll buffs. Only used when Kind is Scroll.")]
        public BuffKind Buff = BuffKind.None;

        [Header("Spawning")]
        [Tooltip("Earliest floor this item can drop on.")]
        public int MinDepth = 1;

        [Tooltip("Relative drop weight before depth weighting is applied.")]
        public int BaseWeight = 10;

        [Header("Visual")]
        [Tooltip("Optional artwork. Leave empty to use the default shape-by-kind look.")]
        public Sprite Sprite;

        /// <summary>Builds the plain Core-side definition the simulation actually runs on.</summary>
        public ItemDef ToItemDef()
        {
            return new ItemDef
            {
                Name = ItemName,
                Kind = Kind,
                Power = Power,
                Duration = Duration,
                Buff = Buff,
                MinDepth = MinDepth,
                BaseWeight = BaseWeight,
                Glyph = Glyph
            };
        }
    }
}
