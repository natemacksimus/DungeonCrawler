namespace DungeonCrawler.Core
{
    /// <summary>
    /// Deterministic xorshift32 generator. Seeded explicitly so a run (and any bug in it)
    /// can be reproduced exactly from its seed.
    /// </summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(int seed)
        {
            Seed = seed;
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public int Seed { get; private set; }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        /// <summary>Uniform integer in [minInclusive, maxInclusive].</summary>
        public int RangeInclusive(int minInclusive, int maxInclusive)
        {
            return Range(minInclusive, maxInclusive + 1);
        }

        public double NextDouble()
        {
            return (NextUInt() >> 8) / (double)(1 << 24);
        }

        /// <summary>True with probability <paramref name="chance"/> (0..1).</summary>
        public bool Chance(double chance)
        {
            return NextDouble() < chance;
        }

        public T Pick<T>(System.Collections.Generic.IList<T> items)
        {
            return items[Range(0, items.Count)];
        }
    }
}
