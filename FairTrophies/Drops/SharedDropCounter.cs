using System;
using System.Collections.Generic;

namespace FairTrophies
{
    /// <summary>
    /// Bad-luck counter that every kill dropping the same item drains, whatever its star level. Pure logic (no Unity)
    /// so it is unit tested.
    ///
    /// Vanilla keeps one countdown per item, but tags it with the star-scaled chance and re-rolls it from scratch
    /// whenever a kill with a different effective chance comes along (see docs/VANILLA_DROPS.md). Here the countdown
    /// is measured in "expected drops" instead of kills: it is rolled uniformly in [0, 2) (mean 1, the same spread
    /// vanilla uses) and each kill subtracts its own effective chance, <c>baseChance * weight</c>. A 2-star kill of a
    /// star-scaled drop therefore fills the counter four times as fast as a 0-star kill, but never resets it.
    /// </summary>
    public sealed class SharedDropCounter
    {
        /// <summary>Width of the interval roll; uniform [0, Spread) has mean 1 drop.</summary>
        public const float Spread = 2f;

        private readonly Dictionary<string, float> remaining = new Dictionary<string, float>();
        private readonly Func<float> random01;

        /// <param name="random01">Uniform random value in [0, 1); injected so tests are deterministic.</param>
        public SharedDropCounter(Func<float> random01)
        {
            this.random01 = random01 ?? throw new ArgumentNullException(nameof(random01));
        }

        public int Count => remaining.Count;

        /// <summary>
        /// Registers one kill and returns whether the item drops.
        /// </summary>
        /// <param name="key">Counter identity - the dropped item's prefab name, like vanilla.</param>
        /// <param name="baseChance">Unscaled drop chance of the item (CharacterDrop.Drop.m_chance).</param>
        /// <param name="weight">Star multiplier for this kill (<see cref="StarWeight"/>), 1 for a 0-star.</param>
        public bool RegisterKill(string key, float baseChance, float weight)
        {
            float step = baseChance * weight;
            if (step <= 0f) return false;

            if (!remaining.TryGetValue(key, out float left))
            {
                left = RollInterval();
            }

            left -= step;
            bool dropped = left <= 0f;
            if (dropped)
            {
                // Carry the overshoot into the next interval so the long-run rate is exactly `step` drops per kill;
                // at most one drop per kill even when the overshoot exceeds a whole interval.
                left = Math.Max(left + RollInterval(), 0f);
            }

            remaining[key] = left;
            return dropped;
        }

        /// <summary>Expected drops' worth of kills left before the next drop, or null if the key was never seen.</summary>
        public float? GetRemaining(string key) => remaining.TryGetValue(key, out float left) ? left : (float?)null;

        public void Set(string key, float left) => remaining[key] = Math.Max(left, 0f);

        public IReadOnlyDictionary<string, float> Snapshot() => remaining;

        public void Clear() => remaining.Clear();

        private float RollInterval() => random01() * Spread;

        /// <summary>
        /// The vanilla star multiplier (CharacterDrop.GenerateDropList): 2^(level-1), level 1 = 0 stars. Drops with
        /// m_levelMultiplier off (almost every trophy) have the same chance at every star level, so the weight is 1.
        /// </summary>
        public static float StarWeight(int level, bool levelMultiplier)
        {
            if (!levelMultiplier || level <= 1) return 1f;
            return (float)Math.Pow(2, level - 1);
        }
    }
}
