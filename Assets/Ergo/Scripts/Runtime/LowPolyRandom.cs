namespace Ergo
{
    /// <summary>
    /// The seeded random generator used by aframe-low-poly 0.0.2: an xfnv1a hash of the seed string feeding
    /// mulberry32. It is reproduced with 32-bit unsigned arithmetic so a given seed produces exactly the same
    /// low-poly shapes as the original game (every lp-cone in Ergo uses the default seed, "apples").
    /// </summary>
    public sealed class LowPolyRandom
    {
        /// <summary>Seed used by every low-poly primitive in the original scene.</summary>
        public const string DefaultSeed = "apples";

        private uint state;

        /// <summary>Creates a generator for <paramref name="seed"/>.</summary>
        public LowPolyRandom(string seed)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < seed.Length; i++)
                {
                    hash = (hash ^ seed[i]) * 16777619u;
                }

                // First output of the xfnv1a generator becomes the mulberry32 state.
                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                state = hash;
            }
        }

        /// <summary>Returns the next value in [0, 1).</summary>
        public double NextDouble()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
