using HarmonyLib;

namespace FairTrophies
{
    /// <summary>
    /// Tags a creature with the last player who damaged it. That player is credited with the kill even when the
    /// creature then dies to something else (fall, fire, drowning, another creature). Untagged kills go to the player
    /// whose game simulated the creature - see KillRouting.RPC_Kill for the full order.
    /// </summary>
    internal static class KillAttribution
    {
        // Stored on the creature's ZDO rather than in memory so it survives the creature changing owner between hits.
        private static readonly int LastPlayerKey = "FairTrophies_lastPlayer".GetStableHashCode();

        /// <summary>PlayerID of the last player to damage <paramref name="character"/>, or 0 if no player did.</summary>
        internal static long LastPlayer(Character character)
        {
            ZNetView view = character ? character.GetComponent<ZNetView>() : null;
            return view != null && view.IsValid() ? view.GetZDO().GetLong(LastPlayerKey) : 0L;
        }

        // RPC_Damage runs on the creature's owner once per hit; record the attacker whenever it is a player.
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class DamagePatch
        {
            [HarmonyPostfix]
            private static void Postfix(Character __instance, HitData hit)
            {
                if (hit == null || __instance.IsPlayer()) return;
                if (!(hit.GetAttacker() is Player player)) return;

                ZNetView view = __instance.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) return;

                long playerId = player.GetPlayerID();
                if (playerId != 0) view.GetZDO().Set(LastPlayerKey, playerId);
            }
        }
    }
}
