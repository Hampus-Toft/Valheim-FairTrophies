using BepInEx.Logging;

namespace FairTrophies
{
    internal static class Log
    {
        internal static ManualLogSource Source;

        /// <summary>Per-kill counter output; silent unless LogCounters is on.</summary>
        internal static void Counter(string message)
        {
            if (FairTrophiesConfig.LogCounters != null && FairTrophiesConfig.LogCounters.Value)
            {
                Source?.LogInfo(message);
            }
        }

        internal static void Info(string message) => Source?.LogInfo(message);

        internal static void Warning(string message) => Source?.LogWarning(message);
    }
}
