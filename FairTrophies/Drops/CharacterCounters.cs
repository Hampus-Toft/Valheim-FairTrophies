using System.Collections.Generic;
using UnityEngine;

namespace FairTrophies
{
    /// <summary>
    /// A character's counters, stored in Player.m_customData so they are saved in the character file (.fch) and follow
    /// the character across sessions, worlds and servers.
    /// </summary>
    internal static class CharacterCounters
    {
        internal const string CustomDataKey = "FairTrophies_counters";

        /// <summary>
        /// Drains <paramref name="player"/>'s counters for every governed drop of <paramref name="table"/> and returns the
        /// drops won as (item prefab hash, amount).
        /// </summary>
        internal static List<KeyValuePair<int, int>> RegisterKill(Player player, string creatureName, CharacterDrop table, int level)
        {
            player.m_customData.TryGetValue(CustomDataKey, out string saved);
            SharedDropCounter counter = SharedDropCounter.Parse(saved, () => Random.value);

            var drops = new List<KeyValuePair<int, int>>();
            foreach (CharacterDrop.Drop drop in table.m_drops)
            {
                if (drop.m_prefab == null || !SharedDropCounter.IsGoverned(drop.m_chance)) continue;

                string item = drop.m_prefab.name;
                ItemDrop itemDrop = drop.m_prefab.GetComponent<ItemDrop>();
                bool isTrophy = itemDrop != null && itemDrop.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
                StarScaling stars = StarScaling.For(level, drop.m_levelMultiplier, item, isTrophy);
                bool dropped = counter.RegisterKill(item, drop.m_chance, stars.Weight);

                float? left = counter.GetRemaining(item);
                Log.Counter($"{player.GetPlayerName()} killed {creatureName} (level {level}) -> {item} " +
                            $"chance {drop.m_chance:0.###} rate x{stars.Weight} amount x{stars.AmountMultiplier}: " +
                            $"{(dropped ? "DROP" : "no drop")}, " +
                            $"{(left.HasValue ? (left.Value / drop.m_chance).ToString("0.0") : "-")} base kills to next");
                if (!dropped) continue;

                int amount = VanillaAmount(drop, stars.AmountMultiplier);
                if (amount > 0) drops.Add(new KeyValuePair<int, int>(item.GetStableHashCode(), amount));
            }

            player.m_customData[CustomDataKey] = counter.Serialize();
            return drops;
        }

        // Mirrors the amount half of vanilla CharacterDrop.GenerateDropList, except the star multiplier comes from
        // StarScaling (1 for unique rare items, whose star bonus is the higher drop rate instead).
        private static int VanillaAmount(CharacterDrop.Drop drop, int amountMultiplier)
        {
            int amount = drop.m_dontScale
                ? Random.Range(drop.m_amountMin, drop.m_amountMax)
                : Game.instance.ScaleDrops(drop.m_prefab, drop.m_amountMin, drop.m_amountMax);
            amount *= amountMultiplier;
            if (drop.m_onePerPlayer) amount = ZNet.instance.GetNrOfPlayers();
            return Mathf.Min(amount, 100);
        }
    }
}
