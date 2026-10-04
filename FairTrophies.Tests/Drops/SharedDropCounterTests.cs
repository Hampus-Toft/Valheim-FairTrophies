using System;
using System.Collections.Generic;
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
                Assert.False(counter.RegisterKill("TrophyWolf", 0.1f, 1f));
            }
            Assert.True(counter.RegisterKill("TrophyWolf", 0.1f, 1f));
        }

        [Fact]
        public void StarredKillDoesNotResetProgress()
        {
            // Vanilla re-rolls when a kill with a different effective chance comes along; here 9 base kills plus one
            // weight-2 kill (=11 base kills) passes the 10-kill interval.
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
        public void AtMostOneDropPerKill()
        {
            var counter = Fixed(0f, 0f, 0.5f);
            Assert.True(counter.RegisterKill("Item", 0.3f, 4f));
            Assert.Equal(0f, counter.GetRemaining("Item").Value);
            Assert.True(counter.RegisterKill("Item", 0.3f, 1f));
        }

        [Fact]
        public void KeysAreIndependent()
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
        [InlineData(0.03f, 4f)]
        [InlineData(0.05f, 1f)]
        public void LongRunRateMatchesEffectiveChance(float chance, float weight)
        {
            var rng = new Random(1234);
            var counter = new SharedDropCounter(() => (float)rng.NextDouble());
            const int kills = 200_000;
            int drops = 0;
            for (int i = 0; i < kills; i++)
            {
                if (counter.RegisterKill("Item", chance, weight)) drops++;
            }

            double expected = chance * weight;
            Assert.InRange((double)drops / kills, expected * 0.97, expected * 1.03);
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

            Assert.InRange(drops, expected * 0.97, expected * 1.03);
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
    }
}
