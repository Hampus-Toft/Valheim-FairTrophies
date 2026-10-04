using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FairTrophies
{
    /// <summary>
    /// Takes the drops FairTrophies governs out of vanilla's CharacterDrop.GenerateDropList and decides them with
    /// <see cref="SharedDropCounter"/> instead. Every other drop still goes through vanilla untouched, so the patch
    /// survives game updates to the rest of the method.
    ///
    /// GenerateDropList runs on whichever peer owns the creature's ZDO when it dies (Character.CustomFixedUpdate
    /// -> CheckDeath is owner-only; Ragdoll.Setup calls it on the same machine), which is a client in multiplayer -
    /// not the dedicated server. See docs/VANILLA_DROPS.md.
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    internal static class CharacterDropPatch
    {
        internal static readonly SharedDropCounter Counter = new SharedDropCounter(() => UnityEngine.Random.value);

        internal sealed class State
        {
            public List<CharacterDrop.Drop> Original;
            public List<CharacterDrop.Drop> Governed;
        }

        [HarmonyPrefix]
        private static void Prefix(CharacterDrop __instance, out State __state)
        {
            __state = null;
            if (!FairTrophiesConfig.Enabled.Value) return;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPseudoDrops)) return;

            List<CharacterDrop.Drop> governed = null;
            foreach (CharacterDrop.Drop drop in __instance.m_drops)
            {
                if (!IsGoverned(drop)) continue;
                (governed ??= new List<CharacterDrop.Drop>()).Add(drop);
            }
            if (governed == null) return;

            __state = new State { Original = __instance.m_drops, Governed = governed };
            List<CharacterDrop.Drop> rest = new List<CharacterDrop.Drop>(__instance.m_drops.Count);
            foreach (CharacterDrop.Drop drop in __instance.m_drops)
            {
                if (!governed.Contains(drop)) rest.Add(drop);
            }
            __instance.m_drops = rest;
        }

        [HarmonyPostfix]
        private static void Postfix(CharacterDrop __instance, State __state, List<KeyValuePair<GameObject, int>> __result)
        {
            if (__state == null || __result == null) return;

            Character character = __instance.GetComponent<Character>();
            int level = character ? character.GetLevel() : 1;
            int levelAmountMultiplier = character ? Mathf.Max(1, (int)Mathf.Pow(2f, level - 1)) : 1;
            foreach (CharacterDrop.Drop drop in __state.Governed)
            {
                // Keyed by item like vanilla: every Skeleton variant feeds the one TrophySkeleton counter.
                string key = drop.m_prefab.name;
                float weight = SharedDropCounter.StarWeight(level, drop.m_levelMultiplier);
                bool dropped = Counter.RegisterKill(key, drop.m_chance, weight);
                Log.Diag($"{Utils.GetPrefabName(__instance.gameObject)} level {level} -> {key} weight {weight}: " +
                         $"{(dropped ? "DROP" : "no drop")}, {Counter.GetRemaining(key) / drop.m_chance:0.0} base kills left");
                if (!dropped) continue;

                int amount = VanillaAmount(drop, levelAmountMultiplier);
                if (amount > 0) __result.Add(new KeyValuePair<GameObject, int>(drop.m_prefab, amount));
            }
        }

        // Runs even if vanilla threw, so the creature's drop table is never left filtered.
        [HarmonyFinalizer]
        private static void Finalizer(CharacterDrop __instance, State __state)
        {
            if (__state != null) __instance.m_drops = __state.Original;
        }

        private static bool IsGoverned(CharacterDrop.Drop drop)
        {
            if (drop?.m_prefab == null) return false;
            // Same eligibility as vanilla's pseudo-random drops, but on the unscaled chance so every star level of a
            // creature shares one counter.
            if (drop.m_chance <= 0f || drop.m_chance > 0.3f) return false;
            if (FairTrophiesConfig.AllRareDrops.Value) return true;

            ItemDrop item = drop.m_prefab.GetComponent<ItemDrop>();
            return item != null && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
        }

        // Mirrors the amount half of vanilla CharacterDrop.GenerateDropList.
        private static int VanillaAmount(CharacterDrop.Drop drop, int levelAmountMultiplier)
        {
            int amount = drop.m_dontScale
                ? UnityEngine.Random.Range(drop.m_amountMin, drop.m_amountMax)
                : Game.instance.ScaleDrops(drop.m_prefab, drop.m_amountMin, drop.m_amountMax);
            if (drop.m_levelMultiplier) amount *= levelAmountMultiplier;
            if (drop.m_onePerPlayer) amount = ZNet.instance.GetNrOfPlayers();
            return Math.Min(amount, 100);
        }
    }
}
