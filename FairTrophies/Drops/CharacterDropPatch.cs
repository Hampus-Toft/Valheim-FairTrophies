using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FairTrophies
{
    /// <summary>
    /// Runs on whichever peer owns the dying creature (see docs/VANILLA_DROPS.md). Takes the governed drops out of
    /// vanilla's CharacterDrop.GenerateDropList so vanilla's own counter never sees them, and reports the kill to the
    /// server, which routes it to the credited character (<see cref="KillRouting"/>). Every other drop stays vanilla.
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    internal static class CharacterDropPatch
    {
        [HarmonyPrefix]
        private static void Prefix(CharacterDrop __instance, out List<CharacterDrop.Drop> __state)
        {
            __state = null;
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return;
            // The world modifier turns vanilla's bad-luck counter off; respect it by leaving everything to vanilla.
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPseudoDrops)) return;

            List<CharacterDrop.Drop> rest = null;
            foreach (CharacterDrop.Drop drop in __instance.m_drops)
            {
                if (drop?.m_prefab != null && SharedDropCounter.IsGoverned(drop.m_chance))
                {
                    if (rest == null)
                    {
                        rest = new List<CharacterDrop.Drop>(__instance.m_drops.Count);
                        foreach (CharacterDrop.Drop kept in __instance.m_drops)
                        {
                            if (kept?.m_prefab == null || !SharedDropCounter.IsGoverned(kept.m_chance)) rest.Add(kept);
                        }
                    }
                }
            }
            if (rest == null) return;

            __state = __instance.m_drops;
            __instance.m_drops = rest;
        }

        [HarmonyPostfix]
        private static void Postfix(CharacterDrop __instance, List<CharacterDrop.Drop> __state)
        {
            if (__state == null) return;

            Character character = __instance.GetComponent<Character>();
            int level = character ? character.GetLevel() : 1;
            Vector3 dropPoint = character
                ? character.GetCenterPoint() + __instance.transform.TransformVector(__instance.m_spawnOffset)
                : __instance.transform.position;
            int creatureHash = Utils.GetPrefabName(__instance.gameObject).GetStableHashCode();

            KillRouting.ReportKill(creatureHash, level, KillAttribution.LastPlayer(character), dropPoint,
                RagdollContext.Current, __instance.m_cheated);
        }

        // Runs even if vanilla threw, so the creature's drop table is never left filtered.
        [HarmonyFinalizer]
        private static void Finalizer(CharacterDrop __instance, List<CharacterDrop.Drop> __state)
        {
            if (__state != null) __instance.m_drops = __state;
        }
    }

    /// <summary>
    /// Ragdoll.Setup -> SaveLootList is the other caller of GenerateDropList (creatures whose loot drops when the corpse
    /// fades). Remember which ragdoll is saving its loot so the server's answer can be added to the same corpse.
    /// </summary>
    [HarmonyPatch(typeof(Ragdoll), "SaveLootList")]
    internal static class RagdollContext
    {
        internal static ZDOID Current = ZDOID.None;

        private static readonly List<int> DeferredHashes = new List<int>();
        private static readonly List<int> DeferredAmounts = new List<int>();

        /// <summary>Drops decided while <see cref="Current"/> is still inside SaveLootList; appended once it is done.</summary>
        internal static void Defer(IList<int> hashes, IList<int> amounts)
        {
            DeferredHashes.AddRange(hashes);
            DeferredAmounts.AddRange(amounts);
        }

        [HarmonyPrefix]
        private static void Prefix(Ragdoll __instance)
        {
            ZNetView view = __instance.GetComponent<ZNetView>();
            Current = view != null && view.IsValid() ? view.GetZDO().m_uid : ZDOID.None;
        }

        [HarmonyPostfix]
        private static void Postfix(Ragdoll __instance)
        {
            if (DeferredHashes.Count == 0) return;
            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view != null && view.IsValid()) KillRouting.AppendRagdollLoot(view.GetZDO(), DeferredHashes, DeferredAmounts);
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            Current = ZDOID.None;
            DeferredHashes.Clear();
            DeferredAmounts.Clear();
        }
    }
}
