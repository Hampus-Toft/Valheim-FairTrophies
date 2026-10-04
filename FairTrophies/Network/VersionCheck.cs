using System.Collections.Generic;
using HarmonyLib;

namespace FairTrophies
{
    /// <summary>
    /// FairTrophies needs the same version on the server and every client (drops are decided by the server routing kills
    /// between clients). Both sides send their version as soon as the connection opens - before vanilla's PeerInfo
    /// exchange, which is where vanilla checks its own network version - and refuse PeerInfo on a mismatch, using
    /// vanilla's own "incompatible version" error.
    /// </summary>
    internal static class VersionCheck
    {
        private const string VersionRpc = "FairTrophies_Version";

        private static readonly HashSet<ZRpc> ValidatedClients = new HashSet<ZRpc>();
        private static string serverVersion;
        private static string failureMessage;

        private static readonly AccessTools.FieldRef<ZNet.ConnectionStatus> ConnectionStatus =
            AccessTools.StaticFieldRefAccess<ZNet.ConnectionStatus>(AccessTools.Field(typeof(ZNet), "m_connectionStatus"));

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        private static class OnNewConnectionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                if (!__instance.IsServer())
                {
                    serverVersion = null;
                    failureMessage = null;
                }
                peer.m_rpc.Register<string>(VersionRpc, (rpc, version) => RPC_Version(__instance, rpc, version));
                peer.m_rpc.Invoke(VersionRpc, FairTrophiesPlugin.PluginVersion);
            }
        }

        private static void RPC_Version(ZNet znet, ZRpc rpc, string version)
        {
            if (znet.IsServer())
            {
                if (version == FairTrophiesPlugin.PluginVersion)
                {
                    ValidatedClients.Add(rpc);
                }
                else
                {
                    Log.Warning($"Client {rpc.GetSocket().GetEndPointString()} runs FairTrophies {version}, server runs {FairTrophiesPlugin.PluginVersion}");
                }
                return;
            }

            serverVersion = version;
        }

        [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
        private static class PeerInfoPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ZNet __instance, ZRpc rpc)
            {
                if (__instance.IsServer())
                {
                    if (ValidatedClients.Contains(rpc)) return true;

                    Log.Warning($"Rejecting {rpc.GetSocket().GetEndPointString()}: FairTrophies {FairTrophiesPlugin.PluginVersion} is required on clients");
                    rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);
                    return false;
                }

                if (serverVersion == FairTrophiesPlugin.PluginVersion) return true;

                failureMessage = serverVersion == null
                    ? $"This server does not run FairTrophies.\nRemove FairTrophies {FairTrophiesPlugin.PluginVersion} to join it."
                    : $"FairTrophies version mismatch.\nServer: {serverVersion}, you: {FairTrophiesPlugin.PluginVersion}.";
                Log.Warning(failureMessage.Replace('\n', ' '));
                ConnectionStatus() = ZNet.ConnectionStatus.ErrorVersion;
                return false;
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        private static class DisconnectPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ZNetPeer peer)
            {
                if (peer?.m_rpc != null) ValidatedClients.Remove(peer.m_rpc);
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnDestroy")]
        private static class ZNetDestroyPatch
        {
            [HarmonyPostfix]
            private static void Postfix() => ValidatedClients.Clear();
        }

        // Replace vanilla's generic "incompatible version" text with which FairTrophies versions clashed.
        [HarmonyPatch(typeof(FejdStartup), "ShowConnectError")]
        private static class ShowConnectErrorPatch
        {
            [HarmonyPostfix]
            private static void Postfix(FejdStartup __instance)
            {
                if (failureMessage == null || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.ErrorVersion) return;
                __instance.m_connectionFailedError.text = failureMessage;
            }
        }
    }
}
