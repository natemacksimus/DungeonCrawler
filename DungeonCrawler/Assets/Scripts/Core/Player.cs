namespace DungeonCrawler.Core
{
    /// <summary>The single v1 character: stats, equipment, pack, and a linear XP curve.</summary>
    public sealed class Player : Actor
    {
        public const int StartingMaxHp = 30;
        public const int StartingAttack = 6;
        public const int StartingDefense = 2;
        public const int SightRadius = 8;

        public Player()
        {
            Name = "you";
            Reset();
        }

        public int Level;
        public int Xp;
        public int Gold;
        public Inventory Pack = new Inventory(10);
        public Item Weapon;
        public Item Armor;

        /// <summary>Remaining turns on the attack buff from a Scroll of Might.</summary>
        public int AttackBuffTurns;
        public int AttackBuffPower;

        /// <summary>Remaining turns on the defense buff from a Scroll of Warding.</summary>
        public int DefenseBuffTurns;
        public int DefenseBuffPower;

        public override int TotalAttack
        {
            get
            {
                int total = Attack;
                if (Weapon != null) total += Weapon.Def.Power;
                if (AttackBuffTurns > 0) total += AttackBuffPower;
                return total;
            }
        }

        public override int TotalDefense
        {
            get
            {
                int total = Defense;
                if (Armor != null) total += Armor.Def.Power;
                if (DefenseBuffTurns > 0) total += DefenseBuffPower;
                return total;
            }
        }

        public int XpToNextLevel { get { return 20 + (Level - 1) * 15; } }

        /// <summary>Back to a fresh level-1 character. Permadeath means this is the whole reset path.</summary>
        public void Reset()
        {
            Level = 1;
            Xp = 0;
            Gold = 0;
            MaxHp = StartingMaxHp;
            Hp = StartingMaxHp;
            Attack = StartingAttack;
            Defense = StartingDefense;
            Weapon = null;
            Armor = null;
            AttackBuffTurns = 0;
            DefenseBuffTurns = 0;
            Pack.Clear();
        }

        /// <summary>Grants XP and returns how many levels were gained.</summary>
        public int GainXp(int amount)
        {
            if (amount <= 0) return 0;
            Xp += amount;
            int levels = 0;
            while (Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                LevelUp();
                levels++;
            }
            return levels;
        }

        void LevelUp()
        {
            Level++;
            MaxHp += 7;
            Attack += 1;
            // Defense climbs every other level so damage taken stays meaningful.
            if (Level % 2 == 0) Defense += 1;
            Heal(10);
        }

        public void ApplyBuff(BuffKind kind, int power, int turns)
        {
            if (kind == BuffKind.Attack)
            {
                AttackBuffPower = power;
                AttackBuffTurns = turns;
            }
            else if (kind == BuffKind.Defense)
            {
                DefenseBuffPower = power;
                DefenseBuffTurns = turns;
            }
        }

        /// <summary>Counts buffs down one turn. Returns a message when one expires, else null.</summary>
        public string TickBuffs()
        {
            string expired = null;
            if (AttackBuffTurns > 0)
            {
                AttackBuffTurns--;
                if (AttackBuffTurns == 0) expired = "Your surge of might fades.";
            }
            if (DefenseBuffTurns > 0)
            {
                DefenseBuffTurns--;
                if (DefenseBuffTurns == 0) expired = "Your ward flickers out.";
            }
            return expired;
        }

        /// <summary>
        /// Equips a weapon or armour, returning the piece it replaced so the caller can put it back
        /// in the pack.
        /// </summary>
        public Item Equip(Item item)
        {
            if (item == null) return null;
            if (item.Def.Kind == ItemKind.Weapon)
            {
                Item old = Weapon;
                Weapon = item;
                return old;
            }
            if (item.Def.Kind == ItemKind.Armor)
            {
                Item old = Armor;
                Armor = item;
                return old;
            }
            return null;
        }
    }
}
