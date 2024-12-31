﻿/*
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
using Cove.Server.Actor;
using Cove.Server.Utils;
using Microsoft.Extensions.Hosting;
using Cove.Server.HostedServices;
using Microsoft.Extensions.Logging;
using Vector3 = Cove.GodotFormat.Vector3;
using Serilog;
using System.Diagnostics;
using System.Text.Unicode;
using System.Text;
using System.Reflection;
using System.Threading.Channels;

namespace Cove.Server
{
    public partial class CoveServer
    {
        public Serilog.Core.Logger logger;

        public readonly string WebFishingGameVersion = "1.11"; // make sure to update this when the game updates!
        public int MaxPlayers = 20;
        public string ServerName = "A Cove Dedicated Server";
        public string LobbyCode = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
        public bool codeOnly = true;
        public bool ageRestricted = false;

        public string joinMessage = "This is a Cove dedicated server!\nPlease report any issues to the github (xr0.xyz/cove)";
        public bool displayJoinMessage = true;

        public float rainMultiplyer = 1f;
        public bool shouldSpawnMeteor = true;
        public bool shouldSpawnMetal = true;
        public bool shouldSpawnPortal = true;

        public bool showErrorMessages = true;
        public bool showBotRejoins = true;
        public bool friendsOnly = false;

        List<string> Admins = new();
        public CSteamID SteamLobby;

        public List<CSteamID> connectionsQueued = new();
        public List<WFPlayer> AllPlayers = new();
        public List<WFActor> serverOwnedInstances = new();
        public List<WFActor> allActors = new();

        private HSteamListenSocket listenSocket;

        public WFPlayer serverPlayer;

        Thread cbThread;
        Thread networkThread;

        List<Vector3> fish_points;
        List<Vector3> trash_points;
        List<Vector3> shoreline_points;
        List<Vector3> hidden_spot;

        Dictionary<string, IHostedService> services = new();
        public readonly object serverActorListLock = new();

        public void Init()
        {
            cbThread = new(runSteamworksUpdate);
            cbThread.Name = "Steamworks Callback Thread";

            networkThread = new(RunNetwork);
            networkThread.Name = "Network Thread";

            Log("Loading world!");
            string worldFile = $"{AppDomain.CurrentDomain.BaseDirectory}worlds/main_zone.tscn";
            if (!File.Exists(worldFile))
            {
                Log("-- ERROR --");
                Log("main_zone.tscn is missing!");
                Log("please put a world file in the /worlds folder so the server may load it!");
                Log("-- ERROR --");
                Log("Press any key to exit");

                Console.ReadKey();

                return;
            }

            string banFile = $"{AppDomain.CurrentDomain.BaseDirectory}bans.txt";
            if (!File.Exists(banFile))
            {
                FileStream f = File.Create(banFile);
                f.Close(); // close the file
            }

            // get all the spawn points for fish!
            string mapFile = File.ReadAllText(worldFile);
            fish_points = WorldFile.readPoints("fish_spawn", mapFile);
            trash_points = WorldFile.readPoints("trash_point", mapFile);
            shoreline_points = WorldFile.readPoints("shoreline_point", mapFile);
            hidden_spot = WorldFile.readPoints("hidden_spot", mapFile);

            Log("World Loaded!");

            Log("Reading server.cfg");

            Dictionary<string, string> config = ConfigReader.ReadConfig("server.cfg");
            foreach (string key in config.Keys)
            {
                switch (key)
                {
                    case "serverName":
                        ServerName = config[key];
                        break;

                    case "maxPlayers":
                        MaxPlayers = int.Parse(config[key]);
                        break;

                    case "code":
                        LobbyCode = config[key].ToUpper();
                        break;

                    case "rainSpawnMultiplyer":
                        rainMultiplyer = float.Parse(config[key]);
                        break;

                    case "codeOnly":
                        codeOnly = getBoolFromString(config[key]);
                        break;

                    case "gameVersion":
                        //WebFishingGameVersion = config[key];
                        break;

                    case "ageRestricted":
                        ageRestricted = getBoolFromString(config[key]);
                        break;

                    case "pluginsEnabled":
                        arePluginsEnabled = getBoolFromString(config[key]);
                        break;

                    case "joinMessage":
                        joinMessage = config[key].Replace("\\n", "\n");
                        break;

                    case "spawnMeteor":
                        shouldSpawnMeteor = getBoolFromString(config[key]);
                        break;

                    case "spawnMetal":
                        shouldSpawnMetal = getBoolFromString(config[key]);
                        break;

                    case "spawnPortal":
                        shouldSpawnPortal = getBoolFromString(config[key]);
                        break;

                    case "showErrors":
                        showErrorMessages = getBoolFromString(config[key]);
                        break;

                    case "friendsOnly":
                        friendsOnly = getBoolFromString(config[key]);
                        break;

                    case "hideJoinMessage":
                        displayJoinMessage = !getBoolFromString(config[key]);
                        break;

                    case "showBotRejoins":
                        showBotRejoins = getBoolFromString(config[key]);
                        break;

                    default:
                        Log($"\"{key}\" is not a supported config option!");
                        continue;
                }

                Log($"Set \"{key}\" to \"{config[key]}\"");

            }

            Log("Server setup based on config!");

            Log("Reading admins.cfg");
            readAdmins();
            Log("Setup finished, starting server!");

            RegisterDefaultCommands(); // register the default commands

            if (Directory.Exists($"{AppDomain.CurrentDomain.BaseDirectory}plugins"))
            {
                loadAllPlugins();
            }
            else
            {
                Directory.CreateDirectory($"{AppDomain.CurrentDomain.BaseDirectory}plugins");
                Log("Created plugins folder!");
            }

            if (!SteamAPI.Init())
            {
                Log("SteamAPI_Init() failed.");
                Log("Is Steam running?");
                return;
            }

            serverPlayer = new WFPlayer(SteamUser.GetSteamID(), SteamFriends.GetPersonaName(), new SteamNetworkingIdentity());

            // thread for running steamworks callbacks
            cbThread.IsBackground = true;
            cbThread.Start();

            // thread for getting network packets from steam
            // i wish this could be a service, but when i tried it the packets got buffered and it was a mess
            // like 10 minutes of delay within 30 seconds
            networkThread.IsBackground = true;
            networkThread.Start();

            // Create a logger for the server
            Serilog.Log.Logger = logger;

            bool LogServices = false;
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                if (LogServices)
                    builder.AddConsole();

                builder.AddSerilog(logger);
            });

            // Create a logger for each service that we need to run.
            Logger<ActorUpdateService> actorServiceLogger = new Logger<ActorUpdateService>(loggerFactory);
            Logger<HostSpawnService> hostSpawnServiceLogger = new Logger<HostSpawnService>(loggerFactory);
            Logger<HostSpawnMetalService> hostSpawnMetalServiceLogger = new Logger<HostSpawnMetalService>(loggerFactory);

            // Create the services that we need to run.
            IHostedService actorUpdateService = new ActorUpdateService(actorServiceLogger, this);
            IHostedService hostSpawnService = new HostSpawnService(hostSpawnServiceLogger, this);
            IHostedService hostSpawnMetalService = new HostSpawnMetalService(hostSpawnMetalServiceLogger, this);

            // add them to the services dictionary so we can access them later if needed
            services["actor_update"] = actorUpdateService;
            services["host_spawn"] = hostSpawnService;
            services["host_spawn_metal"] = hostSpawnMetalService;

            Callback<LobbyCreated_t>.Create((LobbyCreated_t param) =>
            {
                SteamLobby = new CSteamID(param.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(SteamLobby, "ref", "webfishing_gamelobby");
                SteamMatchmaking.SetLobbyData(SteamLobby, "version", WebFishingGameVersion);
                SteamMatchmaking.SetLobbyData(SteamLobby, "code", LobbyCode);
                SteamMatchmaking.SetLobbyData(SteamLobby, "type", codeOnly ? "code_only" : "public");
                SteamMatchmaking.SetLobbyData(SteamLobby, "public", "true");
                SteamMatchmaking.SetLobbyData(SteamLobby, "banned_players", "");
                SteamMatchmaking.SetLobbyData(SteamLobby, "age_limit", ageRestricted ? "true" : "false");
                SteamMatchmaking.SetLobbyData(SteamLobby, "cap", MaxPlayers.ToString());
                SteamMatchmaking.SetLobbyData(SteamLobby, "server_browser_value", "0");
                Log("Lobby Created!");
                Log($"Lobby Code: {LobbyCode}");
                // set the player count in the title
                updatePlayercount();

                // Start the services.
                actorUpdateService.StartAsync(CancellationToken.None);
                hostSpawnService.StartAsync(CancellationToken.None);
                hostSpawnMetalService.StartAsync(CancellationToken.None);

            });

            Callback<LobbyChatUpdate_t>.Create((LobbyChatUpdate_t param) =>
            {
                CSteamID lobbyID = new CSteamID(param.m_ulSteamIDLobby);

                CSteamID userChanged = new CSteamID(param.m_ulSteamIDUserChanged);
                CSteamID userMakingChange = new CSteamID(param.m_ulSteamIDMakingChange);

                EChatMemberStateChange stateChange = (EChatMemberStateChange)param.m_rgfChatMemberStateChange;
                if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeEntered))
                {
                    string Username = SteamFriends.GetFriendPersonaName(userChanged);

                    connectionsQueued.Add(userChanged);
                }

                if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) || stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
                {
                    string Username = SteamFriends.GetFriendPersonaName(userChanged);

                    // if player is in connectionsQueued, remove them
                    if (connectionsQueued.Contains(userChanged))
                        connectionsQueued.Remove(userChanged);

                    // if player is in AllPlayers, remove them
                    if (!connectionsQueued.Contains(userChanged))
                    {
                        WFPlayer player = AllPlayers.Find(p => p.SteamId == userChanged);
                        if (player != null)
                        {
                            AllPlayers.Remove(player);
                            Log($"{player.Username} disconnected!");
                        }
                    }
                }
            });

            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, []);

            Callback<SteamNetworkingMessagesSessionRequest_t>.Create((SteamNetworkingMessagesSessionRequest_t param) =>
            {
                Log($"Accepting session request from {param.m_identityRemote.GetSteamID64().ToString()}");
                SteamNetworkingMessages.AcceptSessionWithUser(ref param.m_identityRemote);
                // get the players WFPlayer object
                WFPlayer player = AllPlayers.Find(p => p.SteamId == param.m_identityRemote.GetSteamID());
                if (player == null)
                {
                    Log("Player not found!");
                    return;
                }

                player.identity = param.m_identityRemote; // update the players identity
            });

            Callback<LobbyChatMsg_t>.Create((LobbyChatMsg_t callback) =>
            {

                CSteamID userId = (CSteamID)callback.m_ulSteamIDUser;
                byte[] data = new byte[4096]; // Max size for a message
                EChatEntryType chatEntryType;

                // Retrieve the message
                int messageLength = SteamMatchmaking.GetLobbyChatEntry(
                    (CSteamID)callback.m_ulSteamIDLobby,
                    (int)callback.m_iChatID,
                    out userId,
                    data,
                    data.Length,
                    out chatEntryType
                );

                if (messageLength > 0)
                {
                    string lobbyMessage = Encoding.UTF8.GetString(data, 0, messageLength);
                    Log($"Message from {userId}: {lobbyMessage}");
                    //Log($"\"{lobbyMessage}\"");

                    if (String.Compare("$weblobby_join_request", lobbyMessage) == 0)
                    {
                        Log($"Player {userId} is trying to join the lobby!");
                        // check if the user is banned 
                        if (isPlayerBanned(userId))
                        {
                            Log($"Player {userId} tried to join, but they are banned!");
                            // send a steamlobby chat message to the player
                            var rejectMessage = $"$weblobby_request_denied_deny-{userId.m_SteamID}";
                            var rejectData = Encoding.UTF8.GetBytes(rejectMessage);
                            SteamMatchmaking.SendLobbyChatMsg(new CSteamID(callback.m_ulSteamIDLobby), rejectData, rejectData.Count());
                            return;
                        }

                        var acceptMessage = $"$weblobby_request_accepted-{userId.m_SteamID}";
                        var acceptData = Encoding.UTF8.GetBytes(acceptMessage);
                        bool suc = SteamMatchmaking.SendLobbyChatMsg(new CSteamID(callback.m_ulSteamIDLobby), acceptData, acceptData.Count());
                        Log($"sent?: {suc.ToString()}");

                        // make the player a wfplayer
                        WFPlayer player = new WFPlayer(userId, SteamFriends.GetFriendPersonaName(userId), new SteamNetworkingIdentity());
                        AllPlayers.Add(player);

                        Dictionary<string, object> joinedPacket = new();
                        joinedPacket["type"] = "user_joined_weblobby";
                        joinedPacket["user_id"] = (long)userId.m_SteamID;
                        sendPacketToPlayers(joinedPacket);

                        Dictionary<string, object> membersPacket = new();
                        membersPacket["type"] = "receive_weblobby";
                        Dictionary<int, object> members = new();

                        members[0] = (long)serverPlayer.SteamId.m_SteamID;
                        for (int i = 0; i < AllPlayers.Count; i++)
                        {
                            members[i + 1] = (long)AllPlayers[i].SteamId.m_SteamID;
                        }

                        membersPacket["weblobby"] = userId.m_SteamID.ToString();
                        sendPacketToPlayers(membersPacket);
                    }
                }
            });

            if (friendsOnly)
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers);
            else
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxPlayers);
        }
        private bool getBoolFromString(string str)
        {
            if (str.ToLower() == "true")
                return true;
            else if (str.ToLower() == "false")
                return false;
            else
                return false;
        }

        void runSteamworksUpdate()
        {
            while (true)
            {
                Thread.Sleep(1000/24); // 24hz
                SteamAPI.RunCallbacks();
            }
        }

        void RunNetwork()
        {
            while (true)
            {
                bool didWork = false;
                try
                {
                    for (int i = 0; i < 6; i++)
                    {
                        List<Dictionary<string, object>> messages = ReceiveMessagesOnChannel(i, 10);
                        if (messages.Count == 0)
                            break;

                        Log($"Received {messages.Count} messages on channel {i}");
                    }
                }
                catch (Exception e)
                {
                    if (!showErrorMessages)
                        return;

                    Log("-- Error responding to packet! --");
                    Log(e.ToString());
                }

                if (!didWork)
                    Thread.Sleep(10);
            }
        }

        public void Stop()
        {
            Log("Server was told to stop.");
            Dictionary<string, object> closePacket = new();
            closePacket["type"] = "server_close";

            loadedPlugins.ForEach(plugin => plugin.plugin.onEnd()); // tell all plugins that the server is closing!

            disconnectAllPlayers();
            SteamMatchmaking.LeaveLobby(SteamLobby);
            SteamAPI.Shutdown();
            Environment.Exit(0);
        }

        void OnPlayerChat(string message, CSteamID id)
        {

            WFPlayer sender = AllPlayers.Find(p => p.SteamId == id);
            if (sender == null)
            {
                Log($"[UNKNOWN] {id}: {message}");
                // should probbaly kick the player here
                return;
            }

            Log($"[{sender.FisherID}] {sender.Username}: {message}");

            // check if the first char is !, if so its a command
            if (message.StartsWith("!"))
            {
                string command = message.Split(' ')[0].Substring(1);
                string[] args = message.Split(' ').Skip(1).ToArray();
                if (DoseCommandExist(command))
                {
                    InvokeCommand(sender, command, args);
                }
                else
                {
                    messagePlayer("Command not found!", sender.SteamId);
                    Log("Command not found!");
                }
            }

            foreach (PluginInstance plugin in loadedPlugins)
            {
                plugin.plugin.onChatMessage(sender, message);
            }
        }

        void Log(string value)
        {
            logger.Information(value);
        }

        void Error(string value)
        {
            logger.Error(value);
        }
    }
}
