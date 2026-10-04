using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using FairTrophies;
using Xunit;

namespace FairTrophies.Tests.Drops
{
    public class SharedDropCounterTests
    {
        private static SharedDropCounter Fixed(params float[] rolls)
        {
            var queue = new Queue<float>(rolls);
            return new SharedDropCounter(() => queue.Dequeue());
        }

        [Fact]
        public void CountsDownBaseKillsUntilDrop()
        {
            // Roll 0.5 * Spread = 1.0 expected drops; at 10% that is 10 base kills.
            var counter = Fixed(0.5f, 0.5f);
            for (int i = 0; i < 9; i++)
            {
                Assert.False(counter.RegisterKill("TrophyBjorn", 0.1f, 1f));
            }
            Assert.True(counter.RegisterKill("TrophyBjorn", 0.1f, 1f));
        }

        [Fact]
        public void StarredKillDoesNotResetProgress()
        {
            // Vanilla re-rolls when a kill with a different effective chance comes along; here 9 base kills plus one
            // weight-2 kill (= 11 base kills) passes the 10-kill interval.
            var counter = Fixed(0.5f, 0.5f);
            for (int i = 0; i < 9; i++)
            {
                counter.RegisterKill("MoldArmorGoldChest", 0.1f, 1f);
            }
            Assert.True(counter.RegisterKill("MoldArmorGoldChest", 0.1f, 2f));
        }

        [Fact]
        public void OvershootCarriesIntoNextInterval()
        {
            var counter = Fixed(0.05f, 0.5f);
            // 0.1 expected drops left; a weight-4 kill at 10% subtracts 0.4 and overshoots by 0.3.
            Assert.True(counter.RegisterKill("Item", 0.1f, 4f));
            Assert.Equal(0.7f, counter.GetRemaining("Item").Value, 4);
        }

        [Fact]
        public void DebtBeyondOneIntervalMeansTheNextKillDropsToo()
        {
            var counter = Fixed(0f, 0.05f, 0.5f);
            // 0 left, -0.8 after a 0.8 step; a roll of 0.1 leaves -0.7 of debt.
            Assert.True(counter.RegisterKill("Item", 0.2f, 4f));
            Assert.Equal(-0.7f, counter.GetRemaining("Item").Value, 4);
            Assert.True(counter.RegisterKill("Item", 0.2f, 1f));
        }

        [Fact]
        public void EffectiveChanceOfOneOrMoreAlwaysDropsWithoutTouchingTheCounter()
        {
            var counter = Fixed();
            Assert.True(counter.RegisterKill("Coins", 0.25f, 4f));
            Assert.True(counter.RegisterKill("Coins", 0.25f, 16f));
            Assert.Null(counter.GetRemaining("Coins"));
        }

        [Fact]
        public void ItemsAreIndependent()
        {
            var counter = Fixed(0.5f, 0.5f);
            counter.RegisterKill("A", 0.1f, 1f);
            counter.RegisterKill("B", 0.1f, 1f);
            Assert.Equal(0.9f, counter.GetRemaining("A").Value, 4);
            Assert.Equal(0.9f, counter.GetRemaining("B").Value, 4);
        }

        [Fact]
        public void ZeroChanceNeverDropsOrRolls()
        {
            var counter = Fixed();
            Assert.False(counter.RegisterKill("Item", 0f, 1f));
            Assert.Equal(0, counter.Count);
        }

        [Theory]
        [InlineData(0.1f, 1f)]
        [InlineData(0.1f, 2f)]
        [InlineData(0.1f, 4f)]
        [InlineData(0.03f, 4f)]
        [InlineData(0.2f, 4f)]
        [InlineData(0.05f, 1f)]
        public void LongRunRateMatchesVanillaEffectiveChance(float chance, float weight)
        {
            var rng = new Random(1234);
            var counter = new SharedDropCounter(() => (float)rng.NextDouble());
            const int kills = 200_000;
            int drops = 0;
            for (int i = 0; i < kills; i++)
            {
                if (counter.RegisterKill("Item", chance, weight)) drops++;
            }

            double expected = Math.Min(1.0, chance * weight);
            Assert.InRange((double)drops / kills, expected * 0.98, expected * 1.02);
        }

        [Fact]
        public void MixedStarLevelsAverageToTheirCombinedChance()
        {
            var rng = new Random(99);
            var counter = new SharedDropCounter(() => (float)rng.NextDouble());
            float[] weights = { 1f, 2f, 4f };
            double expected = 0;
            int drops = 0;
            const int kills = 300_000;
            for (int i = 0; i < kills; i++)
            {
                float w = weights[i % 3];
                expected += 0.03 * w;
                if (counter.RegisterKill("MoldArmorGoldChest", 0.03f, w)) drops++;
            }

            Assert.InRange(drops, expected * 0.98, expected * 1.02);
        }

        [Fact]
        public void NoKillIsEverMoreThanTwiceTheExpectedIntervalAwayFromADrop()
        {
            // The guarantee vanilla's counter gives: at 10% you never go more than 20 kills without the drop.
            var rng = new Random(7);
            var counter = new SharedDropCounter(() => (float)rng.NextDouble());
            int dry = 0;
            for (int i = 0; i < 100_000; i++)
            {
                dry = counter.RegisterKill("TrophyBjorn", 0.1f, 1f) ? 0 : dry + 1;
                Assert.True(dry < 20);
            }
        }

        [Theory]
        [InlineData(1, true, 1f)]
        [InlineData(2, true, 2f)]
        [InlineData(3, true, 4f)]
        [InlineData(3, false, 1f)]
        [InlineData(0, true, 1f)]
        public void StarWeightMatchesVanillaMultiplier(int level, bool levelMultiplier, float expected)
        {
            Assert.Equal(expected, SharedDropCounter.StarWeight(level, levelMultiplier));
        }

        [Theory]
        [InlineData(0.05f, true)]
        [InlineData(0.3f, true)]
        [InlineData(0.33f, false)]
        [InlineData(0.5f, false)]
        [InlineData(0f, false)]
        public void GovernsExactlyVanillasPseudoRandomDrops(float chance, bool governed)
        {
            Assert.Equal(governed, SharedDropCounter.IsGoverned(chance));
        }

        [Fact]
        public void SerializeRoundTrips()
        {
            var counter = Fixed(0.5f, 0.25f);
            counter.RegisterKill("TrophyBjorn", 0.1f, 1f);
            counter.RegisterKill("MoldArmorGoldChest", 0.03f, 4f);
            counter.Set("Debt", -0.123456789f);

            SharedDropCounter copy = SharedDropCounter.Parse(counter.Serialize(), () => 0f);

            Assert.Equal(3, copy.Count);
            foreach (KeyValuePair<string, float> item in counter.Snapshot())
            {
                Assert.Equal(item.Value, copy.GetRemaining(item.Key).Value);
            }
        }

        [Fact]
        public void SerializeIgnoresTheCurrentCulture()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("sv-SE");
                var counter = Fixed();
                counter.Set("TrophyBjorn", 0.5f);
                Assert.Equal("1|TrophyBjorn=0.5", counter.Serialize());
                Assert.Equal(0.5f, SharedDropCounter.Parse("1|TrophyBjorn=0.5", () => 0f).GetRemaining("TrophyBjorn"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("2|TrophyBjorn=0.5")]
        [InlineData("garbage")]
        public void ParseOfMissingOrUnknownDataStartsFresh(string text)
        {
            Assert.Equal(0, SharedDropCounter.Parse(text, () => 0f).Count);
        }

        [Fact]
        public void ParseSkipsMalformedEntries()
        {
            SharedDropCounter counter = SharedDropCounter.Parse("1|A=0.5;broken;=1;B=nope;C=NaN;D=-0.25", () => 0f);
            Assert.Equal(2, counter.Count);
            Assert.Equal(0.5f, counter.GetRemaining("A"));
            Assert.Equal(-0.25f, counter.GetRemaining("D"));
        }
    }
}
