using BepInEx.Logging;

namespace FairTrophies
{
    internal static class Log
    {
        internal static ManualLogSource Source;

        /// <summary>Routine per-kill output; silent unless DiagnosticLogging is on.</summary>
        internal static void Diag(string message)
        {
            if (FairTrophiesConfig.DiagnosticLogging != null && FairTrophiesConfig.DiagnosticLogging.Value)
            {
                Source?.LogInfo(message);
            }
        }

        internal static void Info(string message) => Source?.LogInfo(message);
    }
}
