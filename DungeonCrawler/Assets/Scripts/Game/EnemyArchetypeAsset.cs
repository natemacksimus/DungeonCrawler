using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// Editor-authored description of one <see cref="EnemyArchetype"/>. Every field mirrors the Core
    /// class one-to-one, so this asset is purely Inspector-editable data — the behaviour it drives
    /// still lives in <see cref="GameState"/>, which only ever sees the plain <see cref="EnemyArchetype"/>
    /// that <see cref="ToArchetype"/> builds. Create one via Assets &gt; Create &gt; Dungeon Crawler &gt;
    /// Enemy Archetype, then add it to an <see cref="EnemyCatalogAsset"/> to include it in a run.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyArchetype", menuName = "Dungeon Crawler/Enemy Archetype")]
    public sealed class EnemyArchetypeAsset : ScriptableObject
    {
        [Header("Identity")]
        public string EnemyName = "new enemy";

        [Tooltip("Single character used by ASCII dumps and the self-play log.")]
        public char Glyph = '?';

        [Header("Base stats (before depth scaling)")]
        public int BaseHp = 10;
        public int BaseAttack = 4;
        public int BaseDefense = 1;
        public int BaseXp = 10;
        public int SightRadius = 7;

        [Header("Behaviour")]
        [Tooltip("Attacks from a distance instead of closing in.")]
        public bool Ranged;

        [Tooltip("Tiles a ranged attack can cross.")]
        public int AttackRange = 1;

        [Tooltip("Runs away once badly hurt.")]
        public bool FleesWhenHurt;

        [Tooltip("Ignores the player until attacked or stepped next to.")]
        public bool Sluggish;

        [Header("Spawning")]
        [Tooltip("Earliest floor this enemy can appear on.")]
        public int MinDepth = 1;

        [Tooltip("Relative spawn weight before depth weighting is applied.")]
        public int BaseWeight = 10;

        [Header("Per-floor growth")]
        [Tooltip("Added per floor past floor 1: MaxHp += depth-1 times this.")]
        public int HpPerDepth = 2;

        [Tooltip("Added per floor past floor 1: Attack += depth-1 times this.")]
        public int AttackPerDepth = 1;

        [Tooltip("Added per floor past floor 1: Defense += depth-1 times this (truncated to an int).")]
        public double DefensePerDepth = 0.5;

        [Header("Visual")]
        [Tooltip("Optional artwork. Leave empty to use the default shape-and-colour look.")]
        public Sprite Sprite;

        /// <summary>Builds the plain Core-side archetype the simulation actually runs on.</summary>
        public EnemyArchetype ToArchetype()
        {
            return new EnemyArchetype
            {
                Name = EnemyName,
                Glyph = Glyph,
                BaseHp = BaseHp,
                BaseAttack = BaseAttack,
                BaseDefense = BaseDefense,
                BaseXp = BaseXp,
                SightRadius = SightRadius,
                Ranged = Ranged,
                AttackRange = AttackRange,
                FleesWhenHurt = FleesWhenHurt,
                Sluggish = Sluggish,
                MinDepth = MinDepth,
                BaseWeight = BaseWeight,
                HpPerDepth = HpPerDepth,
                AttackPerDepth = AttackPerDepth,
                DefensePerDepth = DefensePerDepth
            };
        }
    }
}
