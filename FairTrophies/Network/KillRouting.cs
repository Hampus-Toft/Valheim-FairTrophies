using System.Collections.Generic;
using UnityEngine;

namespace FairTrophies
{
    /// <summary>
    /// Counters live on each character (<see cref="CharacterCounters"/>); the server is only the mediator that decides
    /// which character a kill belongs to.
    ///
    /// 1. Kill  - creature owner -> server: the dying creature's peer (a client, the host, or the dedicated server near
    ///            world spawn) reports the kill and who it thinks should be credited.
    /// 2. Count - server -> credited player's peer: that player's client drains its own counters against the vanilla
    ///            drop table and decides the governed drops.
    /// 3. Drop  - credited player's peer -> creature owner: the owner spawns the drops, adding them to the corpse's loot
    ///            when the creature's loot drops from its ragdoll.
    /// </summary>
    internal static class KillRouting
    {
        private const string KillRpc = "FairTrophies_Kill";
        private const string CountRpc = "FairTrophies_Count";
        private const string DropRpc = "FairTrophies_Drop";

        internal static void Register(ZRoutedRpc rpc)
        {
            rpc.Register<ZPackage>(KillRpc, RPC_Kill);
            rpc.Register<ZPackage>(CountRpc, RPC_Count);
            rpc.Register<ZPackage>(DropRpc, RPC_Drop);
        }

        /// <param name="creditedPlayer">Last player to damage the creature, or 0.</param>
        internal static void ReportKill(int creatureHash, int level, long creditedPlayer, Vector3 dropPoint, ZDOID ragdoll, bool cheated)
        {
            long ownerPlayer = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;

            var pkg = new ZPackage();
            pkg.Write(creatureHash);
            pkg.Write(level);
            pkg.Write(creditedPlayer);
            pkg.Write(ownerPlayer);
            pkg.Write(dropPoint);
            pkg.Write(ragdoll);
            pkg.Write(cheated);
            // No target = the server (handled locally when this machine is the server).
            ZRoutedRpc.instance.InvokeRoutedRPC(KillRpc, pkg);
        }

        // Server: pick the character to credit and forward the kill to the peer playing it.
        private static void RPC_Kill(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            int creatureHash = pkg.ReadInt();
            int level = pkg.ReadInt();
            long creditedPlayer = pkg.ReadLong();
            long ownerPlayer = pkg.ReadLong();
            Vector3 dropPoint = pkg.ReadVector3();
            ZDOID ragdoll = pkg.ReadZDOID();
            bool cheated = pkg.ReadBool();

            // Tagged by a player -> that player. Otherwise the player whose game simulated the creature (mob farms).
            // A creature only the dedicated server simulated has no such player: credit the nearest one.
            ZDO character = FindCharacter(creditedPlayer) ?? FindCharacter(ownerPlayer) ?? NearestCharacter(dropPoint);
            if (character == null)
            {
                Log.Counter($"Kill of {creatureHash} at {dropPoint} has no online player to credit; rare drops skipped");
                return;
            }

            var forward = new ZPackage();
            forward.Write(creatureHash);
            forward.Write(level);
            forward.Write(character.GetLong(ZDOVars.s_playerID));
            forward.Write(sender);
            forward.Write(dropPoint);
            forward.Write(ragdoll);
            forward.Write(cheated);
            // A player's character ZDO is owned by that player's peer.
            ZRoutedRpc.instance.InvokeRoutedRPC(character.GetOwner(), CountRpc, forward);
        }

        // Credited player's client: drain this character's counters and decide the drops.
        private static void RPC_Count(long sender, ZPackage pkg)
        {
            if (!IsFromServer(sender)) return;

            int creatureHash = pkg.ReadInt();
            int level = pkg.ReadInt();
            long playerId = pkg.ReadLong();
            long creatureOwnerPeer = pkg.ReadLong();
            Vector3 dropPoint = pkg.ReadVector3();
            ZDOID ragdoll = pkg.ReadZDOID();
            bool cheated = pkg.ReadBool();

            Player player = Player.m_localPlayer;
            if (player == null || player.GetPlayerID() != playerId) return;

            GameObject prefab = ZNetScene.instance.GetPrefab(creatureHash);
            CharacterDrop table = prefab ? prefab.GetComponent<CharacterDrop>() : null;
            if (table == null) return;

            List<KeyValuePair<int, int>> drops = CharacterCounters.RegisterKill(player, prefab.name, table, level);
            if (drops.Count == 0) return;

            var reply = new ZPackage();
            reply.Write(dropPoint);
            reply.Write(ragdoll);
            reply.Write(cheated);
            reply.Write(drops.Count);
            foreach (KeyValuePair<int, int> drop in drops)
            {
                reply.Write(drop.Key);
                reply.Write(drop.Value);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(creatureOwnerPeer, DropRpc, reply);
        }

        // Creature owner: spawn the drops, or add them to the ragdoll if it hasn't dropped its loot yet.
        private static void RPC_Drop(long sender, ZPackage pkg)
        {
            Vector3 dropPoint = pkg.ReadVector3();
            ZDOID ragdoll = pkg.ReadZDOID();
            bool cheated = pkg.ReadBool();
            int count = pkg.ReadInt();

            var hashes = new int[count];
            var amounts = new int[count];
            for (int i = 0; i < count; i++)
            {
                hashes[i] = pkg.ReadInt();
                amounts[i] = pkg.ReadInt();
            }

            if (!ragdoll.IsNone())
            {
                // Singleplayer/host: the whole Kill -> Count -> Drop chain ran synchronously inside this ragdoll's
                // SaveLootList, which is about to write its own loot list over ours - add ours after it.
                if (ragdoll == RagdollContext.Current)
                {
                    RagdollContext.Defer(hashes, amounts);
                    return;
                }

                ZDO ragdollZdo = ZDOMan.instance.GetZDO(ragdoll);
                if (ragdollZdo != null && ragdollZdo.IsOwner())
                {
                    AppendRagdollLoot(ragdollZdo, hashes, amounts);
                    return;
                }
            }

            var drops = new List<KeyValuePair<GameObject, int>>(count);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(hashes[i]);
                if (prefab != null) drops.Add(new KeyValuePair<GameObject, int>(prefab, amounts[i]));
            }
            CharacterDrop.DropItems(drops, dropPoint, 0.5f, cheated);
        }

        /// <summary>Adds to a ragdoll's loot list, using the keys Ragdoll.SaveLootList writes and Ragdoll.SpawnLoot reads.</summary>
        internal static void AppendRagdollLoot(ZDO ragdollZdo, IList<int> hashes, IList<int> amounts)
        {
            int n = ragdollZdo.GetInt(ZDOVars.s_drops);
            for (int i = 0; i < hashes.Count; i++, n++)
            {
                ragdollZdo.Set("drop_hash" + n, hashes[i]);
                ragdollZdo.Set("drop_amount" + n, amounts[i]);
            }
            ragdollZdo.Set(ZDOVars.s_drops, n);
        }

        private static bool IsFromServer(long sender)
        {
            if (ZNet.instance.IsServer()) return sender == ZDOMan.GetSessionID();
            ZNetPeer server = ZNet.instance.GetServerPeer();
            return server != null && sender == server.m_uid;
        }

        private static ZDO FindCharacter(long playerId)
        {
            if (playerId == 0) return null;
            foreach (ZDO zdo in ZNet.instance.GetAllCharacterZDOS())
            {
                if (zdo.GetLong(ZDOVars.s_playerID) == playerId) return zdo;
            }
            return null;
        }

        private static ZDO NearestCharacter(Vector3 point)
        {
            ZDO best = null;
            float bestDistance = float.MaxValue;
            foreach (ZDO zdo in ZNet.instance.GetAllCharacterZDOS())
            {
                float distance = Vector3.Distance(zdo.GetPosition(), point);
                if (zdo.GetLong(ZDOVars.s_playerID) != 0 && distance < bestDistance)
                {
                    best = zdo;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
