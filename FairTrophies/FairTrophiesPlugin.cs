using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FairTrophies
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class FairTrophiesPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.hampustoft.fairtrophies";
        public const string PluginName = "FairTrophies";
        public const string PluginVersion = "0.1.0";

        private Harmony harmony;

        private void Awake()
        {
            Log.Source = Logger;
            FairTrophiesConfig.Initialize(Config);

            harmony = new Harmony(PluginGUID);
            harmony.PatchAll(typeof(CharacterDropPatch));
            harmony.PatchAll(typeof(DropTableLogger));

            Logger.LogInfo($"{PluginName} {PluginVersion} initialized");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    /// <summary>
    /// Debug aid (LogDropTables): dumps every creature's trophy drop as the game actually ships it, since the
    /// chance and m_levelMultiplier flag live in prefab data rather than code.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class DropTableLogger
    {
        [HarmonyPostfix]
        private static void Postfix(ZNetScene __instance)
        {
            if (!FairTrophiesConfig.LogDropTables.Value) return;

            foreach (GameObject prefab in __instance.m_prefabs)
            {
                CharacterDrop characterDrop = prefab ? prefab.GetComponent<CharacterDrop>() : null;
                if (characterDrop == null) continue;

                foreach (CharacterDrop.Drop drop in characterDrop.m_drops)
                {
                    ItemDrop item = drop.m_prefab ? drop.m_prefab.GetComponent<ItemDrop>() : null;
                    if (item == null || item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy) continue;
                    Log.Info($"[DropTable] {prefab.name} -> {drop.m_prefab.name}: chance {drop.m_chance:0.###}, " +
                             $"levelMultiplier {drop.m_levelMultiplier}, amount {drop.m_amountMin}-{drop.m_amountMax}");
                }
            }
        }
    }
}
