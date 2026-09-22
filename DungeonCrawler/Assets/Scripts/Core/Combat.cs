namespace DungeonCrawler.Core
{
    /// <summary>What happened in one exchange, so callers can log it without recomputing anything.</summary>
    public struct AttackResult
    {
        public int Damage;
        public bool KilledDefender;
        public string AttackerName;
        public string DefenderName;
        public bool Ranged;

        public string Describe()
        {
            bool playerAttacking = IsPlayer(AttackerName);
            string verb = Ranged ? "shoot" : "hit";
            if (!playerAttacking) verb += "s";

            string line = Capitalize(Subject(AttackerName)) + " " + verb + " " + Subject(DefenderName) +
                          " for " + Damage + " damage.";

            if (KilledDefender)
            {
                line += IsPlayer(DefenderName)
                    ? " You die!"
                    : " " + Capitalize(Subject(DefenderName)) + " dies!";
            }
            return line;
        }

        /// <summary>"you" stays "you"; a monster becomes "the goblin".</summary>
        static string Subject(string name)
        {
            return IsPlayer(name) ? name : "the " + name;
        }

        static bool IsPlayer(string name)
        {
            return name == "you";
        }

        static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }

    /// <summary>
    /// One damage formula for every attack in the game: attack minus defense, jittered by +/-2, and
    /// never less than 1 so a fight can always end.
    /// </summary>
    public static class Combat
    {
        public const int MinDamage = 1;

        public static int RollDamage(int attack, int defense, Rng rng)
        {
            int jitter = rng.RangeInclusive(-2, 2);
            int damage = attack - defense + jitter;
            return damage < MinDamage ? MinDamage : damage;
        }

        public static AttackResult Resolve(Actor attacker, Actor defender, Rng rng, bool ranged = false)
        {
            int damage = RollDamage(attacker.TotalAttack, defender.TotalDefense, rng);
            int dealt = defender.TakeDamage(damage);

            return new AttackResult
            {
                Damage = dealt,
                KilledDefender = !defender.IsAlive,
                AttackerName = attacker.Name,
                DefenderName = defender.Name,
                Ranged = ranged
            };
        }
    }
}
