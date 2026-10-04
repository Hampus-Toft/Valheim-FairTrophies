using BepInEx.Configuration;

namespace FairTrophies
{
    internal static class FairTrophiesConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> AllRareDrops;
        public static ConfigEntry<bool> LogDropTables;
        public static ConfigEntry<bool> DiagnosticLogging;

        public static void Initialize(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true,
                "Replace vanilla's per-star-level bad-luck counter with one shared counter per creature.");
            AllRareDrops = config.Bind("General", "AllRareDrops", false,
                "Also apply the shared counter to every other drop with a base chance of 30% or less, not just trophies.");
            LogDropTables = config.Bind("Debug", "LogDropTables", false,
                "On world load, log every creature's trophy chance and whether it scales with star level.");
            DiagnosticLogging = config.Bind("Debug", "DiagnosticLogging", false,
                "Log every governed kill: counter key, weight, drop result and kills left.");
        }
    }
}
