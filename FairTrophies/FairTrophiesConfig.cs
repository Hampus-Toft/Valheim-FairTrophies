using BepInEx.Configuration;

namespace FairTrophies
{
    /// <summary>
    /// Deliberately only a debug switch: drop chances always come from the game's own drop tables.
    /// </summary>
    internal static class FairTrophiesConfig
    {
        public static ConfigEntry<bool> LogCounters;

        public static void Initialize(ConfigFile config)
        {
            LogCounters = config.Bind("Debug", "LogCounters", false,
                "Log every counted kill: who was credited, the item, whether it dropped and how many kills are left.");
        }
    }
}
