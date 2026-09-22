namespace DungeonCrawler.Core
{
    /// <summary>
    /// Behaviour switches rather than bespoke AI per monster. Every enemy shares one decision
    /// routine in <see cref="GameState"/>; these flags pick the branches it takes.
    /// </summary>
    public sealed class EnemyArchetype
    {
        public string Name;
        public char Glyph;
        public int BaseHp;
        public int BaseAttack;
        public int BaseDefense;
        public int BaseXp;
        public int SightRadius = 7;

        /// <summary>Attacks from a distance instead of closing in.</summary>
        public bool Ranged;

        /// <summary>Tiles a ranged attack can cross.</summary>
        public int AttackRange = 1;

        /// <summary>Runs away once badly hurt.</summary>
        public bool FleesWhenHurt;

        /// <summary>Ignores the player until attacked or stepped next to.</summary>
        public bool Sluggish;

        /// <summary>Earliest floor this enemy appears on.</summary>
        public int MinDepth = 1;

        public int BaseWeight = 10;

        /// <summary>Per-floor stat growth, applied as (depth - 1) * these.</summary>
        public int HpPerDepth = 2;
        public int AttackPerDepth = 1;
        public double DefensePerDepth = 0.5;
    }

    /// <summary>A live monster on the current floor.</summary>
    public sealed class Enemy : Actor
    {
        public Enemy(EnemyArchetype archetype, int depth, Vec2I position)
        {
            Archetype = archetype;
            Position = position;
            Name = archetype.Name;

            int steps = depth - 1;
            MaxHp = archetype.BaseHp + steps * archetype.HpPerDepth;
            Hp = MaxHp;
            Attack = archetype.BaseAttack + steps * archetype.AttackPerDepth;
            Defense = archetype.BaseDefense + (int)(steps * archetype.DefensePerDepth);
            XpValue = archetype.BaseXp + steps * 2;
        }

        public EnemyArchetype Archetype { get; private set; }
        public int XpValue { get; private set; }

        /// <summary>True once the enemy has seen the player; it then keeps hunting.</summary>
        public bool IsAware;

        /// <summary>Where the player was last seen. An aware enemy heads here when sight is broken.</summary>
        public Vec2I LastKnownPlayerPosition;

        /// <summary>Current wander heading, reused for a few turns so idling reads as pacing.</summary>
        public Vec2I WanderDirection;
        public int WanderTurnsLeft;

        public bool IsFleeing
        {
            get { return Archetype.FleesWhenHurt && Hp * 100 <= MaxHp * 35; }
        }

        public char Glyph { get { return Archetype.Glyph; } }
    }

    /// <summary>The five v1 monsters, differentiated by stats plus one behaviour flag each.</summary>
    public static class EnemyCatalog
    {
        public static readonly EnemyArchetype GiantRat = new EnemyArchetype
        {
            Name = "giant rat", Glyph = 'r',
            BaseHp = 6, BaseAttack = 3, BaseDefense = 0, BaseXp = 5,
            SightRadius = 5, BaseWeight = 30, HpPerDepth = 1, AttackPerDepth = 1
        };

        public static readonly EnemyArchetype Goblin = new EnemyArchetype
        {
            Name = "goblin", Glyph = 'g',
            BaseHp = 10, BaseAttack = 4, BaseDefense = 1, BaseXp = 9,
            SightRadius = 7, BaseWeight = 26
        };

        public static readonly EnemyArchetype KoboldThief = new EnemyArchetype
        {
            Name = "kobold thief", Glyph = 'k',
            BaseHp = 9, BaseAttack = 5, BaseDefense = 1, BaseXp = 11,
            SightRadius = 8, FleesWhenHurt = true, MinDepth = 2, BaseWeight = 18
        };

        public static readonly EnemyArchetype SkeletonArcher = new EnemyArchetype
        {
            Name = "skeleton archer", Glyph = 'a',
            BaseHp = 8, BaseAttack = 4, BaseDefense = 1, BaseXp = 13,
            SightRadius = 9, Ranged = true, AttackRange = 5, MinDepth = 3, BaseWeight = 16
        };

        public static readonly EnemyArchetype OrcBrute = new EnemyArchetype
        {
            Name = "orc brute", Glyph = 'O',
            BaseHp = 20, BaseAttack = 8, BaseDefense = 3, BaseXp = 18,
            SightRadius = 6, Sluggish = true, MinDepth = 4, BaseWeight = 14,
            HpPerDepth = 3, AttackPerDepth = 1, DefensePerDepth = 0.75
        };

        public static readonly EnemyArchetype[] All =
        {
            GiantRat, Goblin, KoboldThief, SkeletonArcher, OrcBrute
        };

        /// <summary>Depth-weighted pick: rats thin out as the nastier archetypes unlock.</summary>
        public static EnemyArchetype Roll(int depth, Rng rng)
        {
            int total = 0;
            var weights = new int[All.Length];
            for (int i = 0; i < All.Length; i++)
            {
                EnemyArchetype a = All[i];
                if (depth < a.MinDepth) continue;

                int weight = a.BaseWeight;
                if (a.MinDepth > 1) weight += (depth - a.MinDepth) * 5;
                else weight = System.Math.Max(4, weight - (depth - 1) * 3);

                weights[i] = weight;
                total += weight;
            }

            if (total <= 0) return GiantRat;

            int roll = rng.Range(0, total);
            for (int i = 0; i < All.Length; i++)
            {
                if (weights[i] == 0) continue;
                roll -= weights[i];
                if (roll < 0) return All[i];
            }
            return Goblin;
        }
    }
}
