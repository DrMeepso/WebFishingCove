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

using Cove.GodotFormat;
using Cove.Server.Actor;
using Cove.Server.Plugins;
using Cove.Server.Utils;
using Steamworks;

namespace Cove.Server
{
    partial class CoveServer
    {
        // TODO: Make this a switch statement
        void OnNetworkPacket(byte[] packet, CSteamID sender)
        {
            Dictionary<string, object> packetInfo = readPacket(GzipHelper.DecompressGzip(packet));

            // just in case!
            if (isPlayerBanned(sender))
                banPlayer(sender);

            var fromPlayer = AllPlayers.Find(p => p.SteamId.m_SteamID == sender.m_SteamID);
            foreach (var loadedPlugin in loadedPlugins)
            {
                if (fromPlayer != null)
                    loadedPlugin.plugin.onNetworkPacket(fromPlayer, packetInfo);
            }

            switch ((string)packetInfo["type"])
            {
                case "handshake_request":
                    {
                        Dictionary<string, object> handshakePacket = new();
                        handshakePacket["type"] = "handshake";
                        handshakePacket["user_id"] = SteamUser.GetSteamID().m_SteamID.ToString();
                        sendPacketToPlayer(handshakePacket, sender);
                    }
                    break;

                case "new_player_join":
                    {
                        if (displayJoinMessage)
                        {
                            messagePlayer(joinMessage, sender);
                        }
                        Dictionary<string, object> hostPacket = new();
                        hostPacket["type"] = "recieve_host";
                        hostPacket["host_id"] = SteamUser.GetSteamID().m_SteamID.ToString();
                        sendPacketToPlayers(hostPacket);

                        if (isPlayerAdmin(sender))
                            messagePlayer("You're an admin on this server!", sender);

                        Thread ChalkInformer = new Thread(() => SendStagedChalkPackets(sender));
                        ChalkInformer.Start(); // send the player all the chalk data
                    }

                    break;

                case "instance_actor":
                    {
                        string type = (string)((Dictionary<string, object>)packetInfo["params"])["actor_type"];
                        long actorID = (long)((Dictionary<string, object>)packetInfo["params"])["actor_id"];

                        // all actor types that should not be spawned by anyone but the server!
                        List<string> illegalTypes = ["fish_spawn_alien", "fish_spawn", "raincloud", "ambient_bird", "void_portal", "metal_spawn"];

                        // if the server owner dosent want players to spawn canvas's then add it to the list
                        if (!playersCanSpawnCanvas)
                            illegalTypes.Add("canvas");

                        if (Array.LastIndexOf(illegalTypes.ToArray(), type) > -1 && !isPlayerAdmin(sender))
                        {
                            kickPlayer(sender);

                            WFPlayer offendingPlayer = AllPlayers.Find(p => p.SteamId.m_SteamID == sender.m_SteamID);
                            messageGlobal($"{offendingPlayer.Username} was kicked for spawning illegal actors");
                        }

                        if (type == "player")
                        {
                            WFPlayer thisPlayer = AllPlayers.Find(p => p.SteamId.m_SteamID == sender.m_SteamID);
                            if (thisPlayer == null)
                                Log("No fisher found for player instance!");
                            else
                            {
                                thisPlayer.InstanceID = actorID;
                                allActors.Add(thisPlayer); // add the player to the actor list

                                // print out a join message
                                Log($"[{thisPlayer.FisherID}] {thisPlayer.Username} joined. [{AllPlayers.Count}/{MaxPlayers}]");
                                sendWebLobbyPacket(thisPlayer.SteamId);
                                updatePlayercount();

                                loadedPlugins.ForEach(p => p.plugin.onPlayerJoin(thisPlayer));
                            }

                        } else {
                            WFActor cActor = new WFActor(actorID, type, Vector3.zero, Vector3.zero);
                            cActor.owner = sender;
                            allActors.Add(cActor);
                        }

                    }
                    break;

                case "actor_update":
                    {
                        WFPlayer thisPlayer = AllPlayers.Find(p => p.InstanceID == (long)packetInfo["actor_id"]);
                        if (thisPlayer != null)
                        {
                            Vector3 position = (Vector3)packetInfo["pos"];
                            thisPlayer.pos = position;
                        }
                    }
                    break;

                case "request_ping":
                    {
                        Dictionary<string, object> pongPacket = new();
                        pongPacket["type"] = "send_ping";
                        pongPacket["time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                        pongPacket["from"] = SteamUser.GetSteamID().m_SteamID.ToString();

                        sendPacketToPlayer(pongPacket, sender);
                    }
                    break;

                case "actor_action":
                    {
                        if ((string)packetInfo["action"] == "_wipe_actor")
                        {
                            long actorToWipe = (long)((Dictionary<int, object>)packetInfo["params"])[0];
                            WFActor serverInst = serverOwnedInstances.Find(i => i.InstanceID == actorToWipe);
                            if (serverInst != null)
                            {
                                // stop players from removing rain clouds
                                if (serverInst.Type == "raincloud")
                                    return;

                                //Log($"Player asked to remove {serverInst.Type} actor");

                                // the sever owns the instance
                                removeServerActor(serverInst);
                            }
                        }
                    }
                    break;

                case "request_actors":
                    {
                        sendPlayerAllServerActors(sender);
                        sendPacketToPlayer(createRequestActorResponce(), sender); // this is empty because otherwise all the server actors are invisible!
                    }
                    break;

                case "chalk_packet":
                    {
                        long canvasID = (long)packetInfo["canvas_id"];
                        Chalk.ChalkCanvas? canvas = chalkCanvas.Find(c => c.canvasID == canvasID);
                        if (canvas == null)
                        {
                            canvas = new Chalk.ChalkCanvas(canvasID, this);
                            chalkCanvas.Add(canvas);
                        }

                        canvas.chalkUpdate((Dictionary<int, object>)packetInfo["data"]);
                        WFPlayer? from = AllPlayers.Find(p =>
                            p.SteamId.m_SteamID == sender.m_SteamID
                        );
                        foreach (PluginInstance p in loadedPlugins)
                        {
                            p.plugin.onChalkUpdate(
                                from,
                                canvasID,
                                (Dictionary<int, object>)packetInfo["data"]
                            );
                        }
                    }
                    break;

                case "message":
                    {
                        string Message = (string)packetInfo["message"];
                        if (Message.StartsWith("%u:")) // Format for a normal chat message
                        {
                            string playerMessage = Message.Replace("%u: ", "");
                            OnPlayerChat(playerMessage, sender);
                        }

                        if (Message.StartsWith("(%u")) // Format for a /me message
                        {
                            // Because it's easier I'm just gonna replace the %u with the player's name
                            // and use that as the message content
                            var thisPlayer = AllPlayers.Find(p => p.SteamId.m_SteamID == sender.m_SteamID);
                            if (thisPlayer == null) return;
                            string playerMessage = Message.Replace("%u", thisPlayer.Username);
                            OnPlayerChat(playerMessage, sender);
                        }
                    }
                    break;
            }
        }

        internal void SendStagedChalkPackets(CSteamID recipient)
        {
            try
            {
                // send the player all the canvas data
                foreach (Chalk.ChalkCanvas canvas in chalkCanvas)
                {
                    Dictionary<int, object> allChalk = canvas.getChalkPacket();

                    // split the dictionary into chunks of 100
                    List<Dictionary<int, object>> chunks = new List<Dictionary<int, object>>();
                    Dictionary<int, object> chunk = new Dictionary<int, object>();

                    int i = 0;
                    foreach (var kvp in allChalk)
                    {
                        if (i >= 1000)
                        {
                            chunks.Add(chunk);
                            chunk = new Dictionary<int, object>();
                            i = 0;
                        }
                        chunk.Add(i, kvp.Value);
                        i++;
                    }

                    chunks.Add(chunk);

                    for (int index = 0; index < chunks.Count; index++)
                    {
                        Dictionary<string, object> chalkPacket = new Dictionary<string, object>
                        {
                            { "type", "chalk_packet" },
                            { "canvas_id", canvas.canvasID },
                            { "data", chunks[index] },
                        };
                        sendPacketToPlayer(chalkPacket, recipient);
                        Thread.Sleep(10);
                    }
                }
            }
            catch (Exception e)
            {
                Error(e.ToString());
            }
        }
    }
}
