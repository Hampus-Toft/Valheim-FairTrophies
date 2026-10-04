using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FairTrophies
{
    /// <summary>
    /// One player's bad-luck counters, one per item, shared by every star level. Pure logic (no Unity) so it is unit
    /// tested.
    ///
    /// Vanilla keeps one countdown per item, but tags it with the star-scaled chance and re-rolls it from scratch
    /// whenever a kill with a different effective chance comes along (see docs/VANILLA_DROPS.md). Here the countdown
    /// is measured in "expected drops" instead of kills: it is rolled uniformly in [0, 2) (mean 1, the same spread
    /// vanilla uses) and each kill subtracts its own effective chance, <c>baseChance * weight</c>. A 2-star kill of a
    /// star-scaled drop fills it four times as fast as a 0-star kill, but never resets it, and the long-run drop rate
    /// is exactly vanilla's intended chance.
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
        /// <param name="item">The dropped item's prefab name - one counter per item, like vanilla.</param>
        /// <param name="baseChance">Unscaled drop chance of the item (CharacterDrop.Drop.m_chance).</param>
        /// <param name="weight">How many base kills this kill counts for (<see cref="StarScaling.Weight"/>), 1 for a 0-star.</param>
        public bool RegisterKill(string item, float baseChance, float weight)
        {
            float step = baseChance * weight;
            if (step <= 0f) return false;
            // Vanilla treats an effective chance of 100%+ as a guaranteed drop; so do we, without touching the counter.
            if (step >= 1f) return true;

            if (!remaining.TryGetValue(item, out float left))
            {
                left = RollInterval();
            }

            left -= step;
            bool dropped = left <= 0f;
            if (dropped)
            {
                // Carry the overshoot into the next interval (it may stay negative, meaning the next kill drops too) so
                // the long-run rate is exactly `step` drops per kill, with at most one drop per kill.
                left += RollInterval();
            }

            remaining[item] = left;
            return dropped;
        }

        /// <summary>Expected drops' worth of kills left before the next drop, or null if the item was never seen.</summary>
        public float? GetRemaining(string item) => remaining.TryGetValue(item, out float left) ? left : (float?)null;

        public void Set(string item, float left) => remaining[item] = left;

        public IReadOnlyDictionary<string, float> Snapshot() => remaining;

        private float RollInterval() => random01() * Spread;

        private const string FormatPrefix = "1|";

        /// <summary>
        /// Compact text for Player.m_customData (string values only): <c>1|Item=left;Item=left</c>, invariant culture.
        /// The leading format version lets a later release migrate the data.
        /// </summary>
        public string Serialize()
        {
            var text = new StringBuilder(FormatPrefix);
            bool first = true;
            foreach (KeyValuePair<string, float> item in remaining)
            {
                if (!first) text.Append(';');
                first = false;
                text.Append(item.Key).Append('=').Append(item.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        /// <summary>Reads <see cref="Serialize"/> output; null, empty, unknown-format or malformed entries are skipped.</summary>
        public static SharedDropCounter Parse(string text, Func<float> random01)
        {
            var counter = new SharedDropCounter(random01);
            if (string.IsNullOrEmpty(text) || !text.StartsWith(FormatPrefix, StringComparison.Ordinal)) return counter;

            foreach (string entry in text.Substring(FormatPrefix.Length).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int split = entry.LastIndexOf('=');
                if (split <= 0) continue;
                if (float.TryParse(entry.Substring(split + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float left)
                    && !float.IsNaN(left) && !float.IsInfinity(left))
                {
                    counter.remaining[entry.Substring(0, split)] = left;
                }
            }
            return counter;
        }

        /// <summary>
        /// Drops vanilla puts behind its bad-luck counter: base chance at or below 30% (vanilla checks the star-scaled
        /// chance, but checking the base chance keeps every star level of a drop on the same counter).
        /// </summary>
        public static bool IsGoverned(float baseChance) => baseChance > 0f && baseChance <= PseudoDropThreshold;


        public const float PseudoDropThreshold = 0.3f;
    }
}
