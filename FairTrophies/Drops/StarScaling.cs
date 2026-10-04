using System;

namespace FairTrophies
{
    /// <summary>
    /// How a creature's star level boosts one of its rare drops. Vanilla multiplies an m_levelMultiplier drop's chance
    /// AND its amount by 2^(level-1), so a 2-star creature gives up to 16x the loot of a 0-star. FairTrophies applies the
    /// star multiplier exactly once, so the total is the intended 1x/2x/4x:
    /// - unique rare items (trophies, Ancient Gemstones, armor molds) become more common, one per drop;
    /// - resources (coins, meat, hide, Memorial Coal...) keep vanilla's chance and drop more per drop.
    /// Pure logic (no Unity) so it is unit tested.
    /// </summary>
    public readonly struct StarScaling
    {
        /// <summary>How many base kills this kill counts for on the item's counter.</summary>
        public float Weight { get; }

        /// <summary>Multiplier on the amount when the drop is won.</summary>
        public int AmountMultiplier { get; }

        private StarScaling(float weight, int amountMultiplier)
        {
            Weight = weight;
            AmountMultiplier = amountMultiplier;
        }

        /// <param name="level">Character level: 1 = 0 stars, 2 = 1 star, 3 = 2 stars.</param>
        /// <param name="levelMultiplier">CharacterDrop.Drop.m_levelMultiplier - off means stars don't affect the drop.</param>
        /// <param name="itemName">The dropped item's prefab name.</param>
        /// <param name="isTrophy">The item's type is Trophy.</param>
        public static StarScaling For(int level, bool levelMultiplier, string itemName, bool isTrophy)
        {
            if (!levelMultiplier || level <= 1) return new StarScaling(1f, 1);

            int multiplier = (int)Math.Pow(2, level - 1);
            return IsUniqueRareItem(itemName, isTrophy)
                ? new StarScaling(multiplier, 1)
                : new StarScaling(1f, multiplier);
        }

        /// <summary>Items whose star bonus is "drops more often", never "drops more of".</summary>
        public static bool IsUniqueRareItem(string itemName, bool isTrophy)
        {
            if (isTrophy) return true;
            if (string.IsNullOrEmpty(itemName)) return false;
            return itemName.StartsWith("AncientGemstone", StringComparison.Ordinal)
                || itemName.StartsWith("MoldArmor", StringComparison.Ordinal);
        }
    }
}
