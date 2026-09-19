namespace BikeFitness.Shared.SecondRider
{
    /// <summary>
    /// Tiny deterministic PRNG for POC logic (xorshift64* with a SplitMix64 seed scramble).
    /// <para>
    /// Deliberately not <see cref="System.Random"/>: its algorithm is not contractually stable across
    /// .NET versions, so a seeded instance can silently produce a different stream after a runtime
    /// upgrade — which would break the determinism/golden tests these POCs rely on. This is ~10 lines
    /// and stable forever.
    /// </para>
    /// </summary>
    public sealed class PocRandom
    {
        private ulong _state;

        public PocRandom(int seed)
        {
            // SplitMix64 finaliser so small/adjacent seeds produce unrelated streams.
            ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            // xorshift requires a non-zero state.
            _state = z == 0 ? 0x2545F4914F6CDD1DUL : z;
        }

        /// <summary>Next raw 64-bit value.</summary>
        public ulong NextULong()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble()
        {
            return (NextULong() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Uniform double in [min, max).</summary>
        public double NextRange(double min, double max)
        {
            return min + (NextDouble() * (max - min));
        }
    }
}
