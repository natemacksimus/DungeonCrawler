namespace DungeonCrawler.Core
{
    /// <summary>Anything that occupies a tile and takes turns.</summary>
    public abstract class Actor
    {
        public string Name = "actor";
        public Vec2I Position;
        public int Hp;
        public int MaxHp;

        /// <summary>Base attack before equipment and buffs.</summary>
        public int Attack;

        /// <summary>Base defense before equipment and buffs.</summary>
        public int Defense;

        public bool IsAlive { get { return Hp > 0; } }

        /// <summary>Attack used by combat, including equipment and temporary effects.</summary>
        public virtual int TotalAttack { get { return Attack; } }

        /// <summary>Defense used by combat, including equipment and temporary effects.</summary>
        public virtual int TotalDefense { get { return Defense; } }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            Hp += amount;
            if (Hp > MaxHp) Hp = MaxHp;
        }

        /// <summary>Applies damage and returns the amount actually taken.</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0) return 0;
            int before = Hp;
            Hp -= amount;
            if (Hp < 0) Hp = 0;
            return before - Hp;
        }
    }
}
