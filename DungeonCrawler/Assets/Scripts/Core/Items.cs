using System.Collections.Generic;

namespace DungeonCrawler.Core
{
    public enum ItemKind
    {
        Potion,
        Scroll,
        Weapon,
        Armor,
        Gold
    }

    /// <summary>Which stat a scroll buffs while its effect lasts.</summary>
    public enum BuffKind
    {
        None,
        Attack,
        Defense
    }

    /// <summary>Static definition of an item type. Instances are <see cref="Item"/>.</summary>
    public sealed class ItemDef
    {
        public string Name;
        public ItemKind Kind;

        /// <summary>Heal amount, stat bonus, or buff magnitude depending on <see cref="Kind"/>.</summary>
        public int Power;

        /// <summary>Turns a scroll buff lasts.</summary>
        public int Duration;

        public BuffKind Buff = BuffKind.None;

        /// <summary>Earliest floor this item can drop on.</summary>
        public int MinDepth = 1;

        /// <summary>Relative drop weight before depth weighting.</summary>
        public int BaseWeight = 10;

        /// <summary>Single character used by ASCII dumps and the self-play log.</summary>
        public char Glyph = '?';

        public string Describe()
        {
            switch (Kind)
            {
                case ItemKind.Potion: return Name + " (heals " + Power + ")";
                case ItemKind.Scroll: return Name + " (+" + Power + " " + Buff + ", " + Duration + " turns)";
                case ItemKind.Weapon: return Name + " (+" + Power + " attack)";
                case ItemKind.Armor: return Name + " (+" + Power + " defense)";
                default: return Name;
            }
        }
    }

    /// <summary>An item instance, either in the world or in a pack. Gold carries an amount.</summary>
    public sealed class Item
    {
        public Item(ItemDef def, int amount = 1)
        {
            Def = def;
            Amount = amount;
        }

        public ItemDef Def { get; private set; }
        public int Amount;

        public string Name
        {
            get { return Def.Kind == ItemKind.Gold ? Amount + " gold" : Def.Name; }
        }

        public string Describe()
        {
            return Def.Kind == ItemKind.Gold ? Amount + " gold" : Def.Describe();
        }
    }

    /// <summary>An item lying on a dungeon tile.</summary>
    public sealed class GroundItem
    {
        public GroundItem(Item item, Vec2I position)
        {
            Item = item;
            Position = position;
        }

        public Item Item;
        public Vec2I Position;
    }

    /// <summary>Every item type in v1. Deliberately short: stat variations, not bespoke behaviour.</summary>
    public static class ItemCatalog
    {
        public static readonly ItemDef HealingPotion = new ItemDef
        {
            Name = "Healing Potion", Kind = ItemKind.Potion, Power = 12,
            BaseWeight = 34, Glyph = '!'
        };

        public static readonly ItemDef GreaterHealingPotion = new ItemDef
        {
            Name = "Greater Healing Potion", Kind = ItemKind.Potion, Power = 28,
            MinDepth = 3, BaseWeight = 12, Glyph = '!'
        };

        public static readonly ItemDef ScrollOfMight = new ItemDef
        {
            Name = "Scroll of Might", Kind = ItemKind.Scroll, Power = 4, Duration = 20,
            Buff = BuffKind.Attack, BaseWeight = 14, Glyph = '?'
        };

        public static readonly ItemDef ScrollOfWarding = new ItemDef
        {
            Name = "Scroll of Warding", Kind = ItemKind.Scroll, Power = 4, Duration = 20,
            Buff = BuffKind.Defense, BaseWeight = 14, Glyph = '?'
        };

        public static readonly ItemDef Dagger = new ItemDef
        {
            Name = "Dagger", Kind = ItemKind.Weapon, Power = 2, BaseWeight = 12, Glyph = '/'
        };

        public static readonly ItemDef ShortSword = new ItemDef
        {
            Name = "Short Sword", Kind = ItemKind.Weapon, Power = 4, MinDepth = 2,
            BaseWeight = 10, Glyph = '/'
        };

        public static readonly ItemDef BattleAxe = new ItemDef
        {
            Name = "Battle Axe", Kind = ItemKind.Weapon, Power = 7, MinDepth = 4,
            BaseWeight = 8, Glyph = '/'
        };

        public static readonly ItemDef RunedGreatsword = new ItemDef
        {
            Name = "Runed Greatsword", Kind = ItemKind.Weapon, Power = 10, MinDepth = 6,
            BaseWeight = 6, Glyph = '/'
        };

        public static readonly ItemDef LeatherArmor = new ItemDef
        {
            Name = "Leather Armor", Kind = ItemKind.Armor, Power = 1, BaseWeight = 12, Glyph = '['
        };

        public static readonly ItemDef ChainMail = new ItemDef
        {
            Name = "Chain Mail", Kind = ItemKind.Armor, Power = 3, MinDepth = 3,
            BaseWeight = 10, Glyph = '['
        };

        public static readonly ItemDef PlateArmor = new ItemDef
        {
            Name = "Plate Armor", Kind = ItemKind.Armor, Power = 5, MinDepth = 5,
            BaseWeight = 7, Glyph = '['
        };

        public static readonly ItemDef GoldPile = new ItemDef
        {
            Name = "Gold", Kind = ItemKind.Gold, Power = 0, BaseWeight = 30, Glyph = '$'
        };

        public static readonly ItemDef[] All =
        {
            HealingPotion, GreaterHealingPotion, ScrollOfMight, ScrollOfWarding,
            Dagger, ShortSword, BattleAxe, RunedGreatsword,
            LeatherArmor, ChainMail, PlateArmor, GoldPile
        };
    }

    /// <summary>
    /// Weighted drop table. Items gated by <see cref="ItemDef.MinDepth"/> cannot appear early, and
    /// gear drops get relatively more likely with depth, so loot keeps pace with the enemies.
    /// </summary>
    public static class LootTable
    {
        public static Item Roll(int depth, Rng rng)
        {
            var candidates = new List<ItemDef>();
            var weights = new List<int>();
            int total = 0;

            for (int i = 0; i < ItemCatalog.All.Length; i++)
            {
                ItemDef def = ItemCatalog.All[i];
                if (depth < def.MinDepth) continue;

                int weight = def.BaseWeight;
                // Deep floors favour the rarer, higher-MinDepth entries.
                if (def.MinDepth > 1) weight += (depth - def.MinDepth) * 4;
                if (weight <= 0) continue;

                candidates.Add(def);
                weights.Add(weight);
                total += weight;
            }

            if (candidates.Count == 0) return new Item(ItemCatalog.HealingPotion);

            int roll = rng.Range(0, total);
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return Create(candidates[i], depth, rng);
            }
            return Create(candidates[candidates.Count - 1], depth, rng);
        }

        public static Item Create(ItemDef def, int depth, Rng rng)
        {
            if (def.Kind == ItemKind.Gold)
            {
                int amount = rng.RangeInclusive(5 + depth * 3, 15 + depth * 10);
                return new Item(def, amount);
            }
            return new Item(def);
        }
    }
}
