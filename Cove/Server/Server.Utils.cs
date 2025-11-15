/*
   Copyright 2024 DrMeepso

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/


using Steamworks;
using Cove.Server.Plugins;
using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Utils;

namespace Cove.Server
{
    partial class CoveServer
    {
        public void readAdmins()
        {
            Dictionary<string, string> config = ConfigReader.ReadConfig("admins.cfg");

            Admins.Clear();

            foreach (string key in config.Keys)
            {
                if (config[key].ToLower() == "true")
                {
                    Log($"Added {key} as admin!");
                    Admins.Add(key);
                    WFPlayer player = AllPlayers.Find(p => p.SteamId.m_SteamID.ToString() == key);
                    if (player != null)
                    {
                        messagePlayer("You are an admin on this server!", player.SteamId);
                    }
                }
            }
        }

        public WFActor spawnRainCloud()
        {
            Random rand = new Random();
            Dictionary<string, object> rainSpawnPacket = new Dictionary<string, object>();

            rainSpawnPacket["type"] = "instance_actor";

            int IId = new Random().Next();

            Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
            rainSpawnPacket["params"] = instanceSpacePrams;

            Vector3 pos = new Vector3(rand.Next(-100, 150), 42f, rand.Next(-150, 100));

            instanceSpacePrams["actor_type"] = "raincloud";
            instanceSpacePrams["at"] = pos;
            instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
            instanceSpacePrams["zone"] = "main_zone";
            instanceSpacePrams["zone_owner"] = -1;
            instanceSpacePrams["actor_id"] = IId;
            instanceSpacePrams["creator_id"] = (long)SteamUser.GetSteamID().m_SteamID;

            sendPacketToPlayers(rainSpawnPacket); // spawn the rain!
            RainCloud cloud = new RainCloud(IId, pos);
            cloud.despawn = true;

            lock (serverActorListLock)
                serverOwnedInstances.Add(cloud);

            allActors.Add(cloud);

            return cloud;
        }

        public WFActor spawnFish(string fishType = "fish_spawn")
        {
            Vector3 pos = fish_points[(new Random()).Next(fish_points.Count)] + new Vector3(0,.08f,0);
            WFActor actor = spawnGenericActor(fishType, pos);
            actor.despawn = true;
            actor.despawnTime = fishType == "fish_spawn" ? 80 : 120; // 80 for normal fish, 120 for alien fish
            return actor;
        }

        public WFActor spawnVoidPortal()
        {
            Vector3 pos = hidden_spot[(new Random()).Next(hidden_spot.Count)];
            WFActor actor = spawnGenericActor("void_portal", pos);
            actor.despawn = true;
            actor.despawnTime = 600;
            return actor;
        }

        public WFActor spawnMetal()
        {
            Vector3 pos = trash_points[(new Random()).Next(trash_points.Count)];
            if (new Random().NextSingle() < .15f)
            {
                pos = shoreline_points[(new Random()).Next(shoreline_points.Count)];
            }
            WFActor actor = spawnGenericActor("metal_spawn", pos);
            actor.despawn = false; // metal never despawns!
            return actor;
        }

        private WFActor findActorByID(long ID)
        {
            return serverOwnedInstances.Find(a => a.InstanceID == ID);
        }

        public WFActor spawnGenericActor(string type, Vector3 pos = null)
        {
            Dictionary<string, object> spawnPacket = new Dictionary<string, object>();

            spawnPacket["type"] = "instance_actor";

            long IId = new Random().NextInt64();
            while (findActorByID(IId) != null)
            {
                Log("Actor ID Collided!");
                IId = new Random().NextInt64();
            }

            Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
            spawnPacket["params"] = instanceSpacePrams;

            if (pos == null)
                pos = Vector3.zero;

            WFActor actor = new WFActor(IId, type, pos);
            lock (serverActorListLock)
                serverOwnedInstances.Add(actor);
            allActors.Add(actor);

            instanceSpacePrams["actor_type"] = type;
            instanceSpacePrams["at"] = pos;
            instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
            instanceSpacePrams["zone"] = "main_zone";
            instanceSpacePrams["zone_owner"] = -1;
            instanceSpacePrams["actor_id"] = IId;
            instanceSpacePrams["creator_id"] = (long)SteamUser.GetSteamID().m_SteamID;

            sendPacketToPlayers(spawnPacket);

            return actor;
        }

        public void removeServerActor(WFActor instance)
        {
            Dictionary<string, object> removePacket = new();
            removePacket["type"] = "actor_action";
            removePacket["actor_id"] = instance.InstanceID;
            removePacket["action"] = "queue_free";

            Dictionary<int, object> prams = new Dictionary<int, object>();
            removePacket["params"] = prams;

            sendPacketToPlayers(removePacket); // remove

            lock (serverActorListLock)
                serverOwnedInstances.Remove(instance);
        }

        private void sendPlayerAllServerActors(CSteamID id)
        {
            lock (serverActorListLock)
            {
                foreach (WFActor actor in serverOwnedInstances)
                {
                    Dictionary<string, object> spawnPacket = new Dictionary<string, object>();
                    spawnPacket["type"] = "instance_actor";

                    Dictionary<string, object> instanceSpacePrams = new Dictionary<string, object>();
                    spawnPacket["params"] = instanceSpacePrams;

                    instanceSpacePrams["actor_type"] = actor.Type;
                    instanceSpacePrams["at"] = actor.pos;
                    instanceSpacePrams["rot"] = new Vector3(0, 0, 0);
                    instanceSpacePrams["zone"] = "main_zone";
                    instanceSpacePrams["zone_owner"] = -1;
                    instanceSpacePrams["actor_id"] = actor.InstanceID;
                    instanceSpacePrams["creator_id"] = (long)SteamUser.GetSteamID().m_SteamID;

                    sendPacketToPlayer(spawnPacket, id);
                }
            }
        }

        public void sendBlacklistPacketToPlayer(string blacklistedSteamID, CSteamID receiving)
        {
            Dictionary<string, object> blacklistPacket = new();
            blacklistPacket["type"] = "force_disconnect_player";
            blacklistPacket["user_id"] = blacklistedSteamID; // gotta be a string
            sendPacketToPlayer(blacklistPacket, receiving);
        }

        public void sendBlacklistPacketToAll(string blacklistedSteamID)
        {
            Dictionary<string, object> blacklistPacket = new();
            blacklistPacket["type"] = "force_disconnect_player";
            blacklistPacket["user_id"] = blacklistedSteamID; // gotta be a string

            CSteamID blockedPlayer = new CSteamID(ulong.Parse(blacklistedSteamID));
            foreach (WFPlayer player in AllPlayers.ToList())
            {
                if (!player.blockedPlayers.Contains(blockedPlayer))
                {
                    sendBlacklistPacketToPlayer(blacklistedSteamID, player.SteamId);
                    player.blockedPlayers.Add(blockedPlayer);
                }
            }
        }

        // returns the letter id!
        int SendLetter(CSteamID to, CSteamID from, string header, string body, string closing, string user)
        {

            // dosent work atm
            // return -1;

            // Crashes the game lmao
            Dictionary<string, object> letterPacket = new();
            letterPacket["type"] = "letter_received";
            letterPacket["to"] = (string)to.m_SteamID.ToString();
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["to"] = (string)to.m_SteamID.ToString();
            data["from"] = (string)from.m_SteamID.ToString();
            data["header"] = header;
            data["body"] = body;
            data["closing"] = closing;
            data["user"] = user;
            data["letter_id"] = new Random().Next();
            data["items"] = new Dictionary<int, object>();
            letterPacket["data"] = data;

            //SteamNetworking.SendP2PPacket(to, writePacket(letterPacket), nChannel: 2);
            sendPacketToPlayer(letterPacket, to);

            return (int)data["letter_id"];
        }

        public void messageGlobal(string msg, string color = "ffffff")
        {
            Dictionary<string, object> chatPacket = new();
            chatPacket["type"] = "message";
            chatPacket["message"] = msg;
            chatPacket["color"] = color;
            chatPacket["local"] = false;
            chatPacket["position"] = new Vector3(0f, 0f, 0f);
            chatPacket["zone"] = "main_zone";
            chatPacket["zone_owner"] = 1;

            // get all players in the lobby
            foreach (CSteamID member in getAllPlayers())
            {
                if (member.m_SteamID == SteamUser.GetSteamID().m_SteamID) continue;
                sendPacketToPlayer(chatPacket, member);
            }
        }

        public void messagePlayer(string msg, CSteamID id, string color = "ffffff")
        {
            Dictionary<string, object> chatPacket = new();
            chatPacket["type"] = "message";
            chatPacket["message"] = msg;
            chatPacket["color"] = color;
            chatPacket["local"] = (bool)false;
            chatPacket["position"] = new Vector3(0f, 0f, 0f);
            chatPacket["zone"] = "main_zone";
            chatPacket["zone_owner"] = 1;

            if (id.m_SteamID == 0)
            {
                Log($"{msg}");
                return;
            }

            sendPacketToPlayer(chatPacket, id);
        }

        public void setActorZone(WFActor instance, string zoneName, int zoneOwner)
        {
            Dictionary<string, object> removePacket = new();
            removePacket["type"] = "actor_action";
            removePacket["actor_id"] = instance.InstanceID;
            removePacket["action"] = "_set_zone";

            Dictionary<int, object> prams = new Dictionary<int, object>();
            removePacket["params"] = prams;

            prams[0] = zoneName;
            prams[1] = zoneOwner;

            sendPacketToPlayers(removePacket); // remove
        }

        public void runActorReady(WFActor instance)
        {
            Dictionary<string, object> removePacket = new();
            removePacket["type"] = "actor_action";
            removePacket["actor_id"] = instance.InstanceID;
            removePacket["action"] = "_ready";

            Dictionary<int, object> prams = new Dictionary<int, object>();
            removePacket["params"] = prams;

            sendPacketToPlayers(removePacket); // remove
        }

        public bool isPlayerAdmin(CSteamID id)
        {
            // if the server is being checked, they are an admin
            if (id.m_SteamID == serverPlayer.SteamId.m_SteamID)
                return true;

            string adminSteamID = Admins.Find(a => long.Parse(a) == (long)id.m_SteamID);
            return adminSteamID is string;
        }

        void updatePlayercount()
        {
            return; // disabled for now
            string serverName = $"{ServerName}";

            Console.Title = $"Cove Dedicated Server, {AllPlayers.Count} players!";

            SteamMatchmaking.SetLobbyData(SteamLobby, "lobby_name", serverName);
            SteamMatchmaking.SetLobbyData(SteamLobby, "name", serverName);

            SteamMatchmaking.SetLobbyData(SteamLobby, "count", (AllPlayers.Count + 1).ToString());

            if (maskMaxPlayers && MaxPlayers > 12)
            {
                if (AllPlayers.Count + 1 >= 12)
                    SteamMatchmaking.SetLobbyData(SteamLobby, "cap", (AllPlayers.Count + 2).ToString());
                if (AllPlayers.Count + 1 < 12)
                    SteamMatchmaking.SetLobbyData(SteamLobby, "cap", "12+");
            }
            

        }

        public void disconnectAllPlayers()
        {
            Dictionary<string, object> closePacket = new();
            closePacket["type"] = "server_close";

            sendPacketToPlayers(closePacket);
        }

        public Dictionary<string, object> createRequestActorResponce()
        {
            Dictionary<string, object> createPacket = new();

            createPacket["type"] = "actor_request_send";

            Dictionary<int, object> actorArray = new();
            createPacket["list"] = actorArray;

            return createPacket;
        }

        public void printPluginLog(string message, CovePlugin caller)
        {

            PluginInstance pluginInfo = loadedPlugins.Find(i => i.plugin == caller);
            Log($"[{pluginInfo.pluginName}] {message}");
        }

        public void sendWebLobbyPacket(CSteamID player)
        {
            // we have to make sure this gets to the client or it will reject all packets from every other player...
            Dictionary<string, object> localMembersPacket = new();
            localMembersPacket["type"] = "receive_weblobby";
            Dictionary<int, object> localMembers = new();

            localMembers[0] = (long)serverPlayer.SteamId.m_SteamID;
            for (int i = 0; i < AllPlayers.Count; i++)
            {
                localMembers[i + 1] = (long)AllPlayers[i].SteamId.m_SteamID;
            }

            localMembersPacket["weblobby"] = localMembers;
            sendPacketToPlayer(localMembersPacket, player);
        }
    }
}
