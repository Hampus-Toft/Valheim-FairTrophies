using BepInEx;
using HarmonyLib;

namespace FairTrophies
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class FairTrophiesPlugin : BaseUnityPlugin
    {
        // Author "Hampus Toft"; the ID follows the Thunderstore namespace (HampusToft) + package name.
        public const string PluginGUID = "HampusToft.FairTrophies";
        public const string PluginName = "FairTrophies";
        public const string PluginVersion = "1.0.2";

        private Harmony harmony;

        private void Awake()
        {
            Log.Source = Logger;
            FairTrophiesConfig.Initialize(Config);

            harmony = new Harmony(PluginGUID);
            harmony.PatchAll(typeof(FairTrophiesPlugin).Assembly);

            Logger.LogInfo($"{PluginName} {PluginVersion} initialized");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    // ZNet creates a fresh ZRoutedRpc for every session; register the kill-routing RPCs on each one.
    [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, typeof(bool))]
    internal static class RegisterRpcsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ZRoutedRpc __instance) => KillRouting.Register(__instance);
    }
}
