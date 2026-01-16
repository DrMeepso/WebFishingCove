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

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Unicode;
using System.Threading.Channels;
using Cove.Server.Actor;
using Cove.Server.HostedServices;
using Cove.Server.Plugins;
using Cove.Server.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Steamworks;
using Vector3 = Cove.GodotFormat.Vector3;

namespace Cove.Server
{
    public partial class CoveServer
    {
        public Serilog.Core.Logger logger;

        public string WebFishingGameVersion = "1.12"; // make sure to update this when the game updates!
        public int MaxPlayers = 20;
        public string ServerName = "A Cove Dedicated Server";
        public string LobbyCode = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
        public bool codeOnly = true;
        public bool ageRestricted = false;
        public bool maskMaxPlayers = false;

        public string joinMessage = "This is a Cove dedicated server!\nPlease report any issues to the github (xr0.xyz/cove)";
        public bool displayJoinMessage = true;

        public float rainMultiplyer = 1f;
        public bool shouldSpawnMeteor = true;
        public bool shouldSpawnMetal = true;
        public bool shouldSpawnPortal = true;

        public bool showErrorMessages = true;
        public bool showBotRejoins = true;
        public bool friendsOnly = false;

        public bool playersCanSpawnCanvas = false;

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

        public List<Vector3> fish_points;
        public List<Vector3> trash_points;
        public List<Vector3> shoreline_points;
        public List<Vector3> hidden_spot;

        Dictionary<string, IHostedService> services = new();
        public readonly object serverActorListLock = new();

        public List<string> WantedTags = new();

        public List<PreviousPlayer> PreviousPlayers = new();
        
        public async void Init()
        {
            cbThread = new(runSteamworksUpdate);
            cbThread.Name = "Steamworks Callback Thread";

            networkThread = new(RunNetwork);
            networkThread.Name = "Network Thread";

            Log("Loading world!");
            string worldFile = $"{AppDomain.CurrentDomain.BaseDirectory}worlds/main_zone.tscn";
            if (!File.Exists(worldFile))
            {
                var res = await DownloadWorldFile();
                if (!res)
                {
                    Error("Failed to download main_zone.tscn!");
                }
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
                        WebFishingGameVersion = config[key];
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

                    case "tags":
                        var tags = config[key].Split(',');
                        // remove trailing whitespace from the tags
                        for (int i = 0; i < tags.Length; i++)
                        {
                            tags[i] = tags[i].Trim().ToLower();
                        }
                        WantedTags = tags.ToList();
                        break;

                    case "skibidi":
                        Log("Dop dop dop, yes yes");
                        break;

                    case "maskMaxPlayers":
                        maskMaxPlayers = getBoolFromString(config[key]);
                        break;

                    case "playersCanSpawnCanvas":
                        playersCanSpawnCanvas = getBoolFromString(config[key]);
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
                SteamMatchmaking.SetLobbyJoinable(SteamLobby, true);
                SteamMatchmaking.SetLobbyData(SteamLobby, "ref", "webfishing_gamelobby");
                SteamMatchmaking.SetLobbyData(SteamLobby, "version", WebFishingGameVersion);
                SteamMatchmaking.SetLobbyData(SteamLobby, "code", LobbyCode);
                SteamMatchmaking.SetLobbyData(SteamLobby, "type", "0");
                SteamMatchmaking.SetLobbyData(SteamLobby, "public", codeOnly ? "false" : "true");
                SteamMatchmaking.SetLobbyData(SteamLobby, "request", "false"); // make this a option later down the line
                if (maskMaxPlayers && MaxPlayers > 12)
                    SteamMatchmaking.SetLobbyData(SteamLobby, "cap", $"12");
                else
                    SteamMatchmaking.SetLobbyData(SteamLobby, "cap", $"{MaxPlayers}");

                Log("Lobby Created!");
                Log($"Lobby Code: {LobbyCode}");
                // set the player count in the title
                updatePlayercount();

                // Start the services.
                actorUpdateService.StartAsync(CancellationToken.None);
                hostSpawnService.StartAsync(CancellationToken.None);
                hostSpawnMetalService.StartAsync(CancellationToken.None);

                SteamMatchmaking.SetLobbyData(SteamLobby, "server_browser_value", "0");

                string[] LOBBY_TAGS = ["talkative", "quiet", "grinding", "chill", "silly", "hardcore", "mature", "modded"];
                for (int i = 0; i < LOBBY_TAGS.Length; i++)
                {
                    bool wantedTag = WantedTags.Contains(LOBBY_TAGS[i]);
                    SteamMatchmaking.SetLobbyData(SteamLobby, $"tag_{LOBBY_TAGS[i]}", wantedTag ? "1" : "0");
                    if (wantedTag)
                        Log($"Added tag: {LOBBY_TAGS[i]}");
                }

                ulong epoch = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                SteamMatchmaking.SetLobbyData(SteamLobby, "timestamp", epoch.ToString());

                /*
                int dataCount = SteamMatchmaking.GetLobbyDataCount(SteamLobby);
                for (int j = 0; j < dataCount; j++)
                {
                    string key, value;
                    SteamMatchmaking.GetLobbyDataByIndex(SteamLobby, j, out key, 100, out value, 100);
                    Log($"{key}: {value}");
                }
                */

            });

            Callback<LobbyChatUpdate_t>.Create((LobbyChatUpdate_t param) =>
            {
                CSteamID lobbyID = new CSteamID(param.m_ulSteamIDLobby);

                CSteamID userChanged = new CSteamID(param.m_ulSteamIDUserChanged);
                CSteamID userMakingChange = new CSteamID(param.m_ulSteamIDMakingChange);

                EChatMemberStateChange stateChange = (EChatMemberStateChange)param.m_rgfChatMemberStateChange;
                // Player joined the lobby
                if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeEntered))
                {
                    string Username = SteamFriends.GetFriendPersonaName(userChanged);
                    ulong steamID = param.m_ulSteamIDMakingChange;

                    Log($"[{steamID}] {Username} is attempting to join the lobby.");

                    connectionsQueued.Add(userChanged);
                }

                // Player left the lobby
                if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) || stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
                {
                    string Username = SteamFriends.GetFriendPersonaName(userChanged);

                    // if player is in connectionsQueued, remove them
                    if (connectionsQueued.Contains(userChanged))
                        connectionsQueued.Remove(userChanged);

                    // if player is in AllPlayers, remove them
                    if (!connectionsQueued.Contains(userChanged))
                    {
                        var player = AllPlayers.Find(p => p.SteamId == userChanged);
                        if (player != null)
                        {
                            AllPlayers.Remove(player);
                            Log($"[{player.FisherID}] {player.Username} left. [{AllPlayers.Count}/{MaxPlayers}]");

                            Dictionary<string, object> leftPacket = new();
                            leftPacket["type"] = "user_left_weblobby";
                            leftPacket["user_id"] = (long)player.SteamId.m_SteamID;
                            leftPacket["reason"] = (int)0;
                            sendPacketToPlayers(leftPacket);

                            SteamNetworkingMessages.CloseSessionWithUser(ref player.identity);
                            updatePlayercount();

                            // tell all plugins that the player left
                            loadedPlugins.ForEach(plugin => plugin.plugin.onPlayerLeave(player));

                            // find the previous player and update their state
                            var previousPlayer = PreviousPlayers.Find(p => p.SteamId == player.SteamId);
                            if (previousPlayer != null)
                            {
                                previousPlayer.leftTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                previousPlayer.State = PlayerState.Left;
                            }
                            else
                            {
                                // if the player is not in the previous players list, add them
                                var p = PreviousPlayer.FromWFPlayer(player);
                                PreviousPlayers.Add(p);
                                p.leftTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                p.State = PlayerState.Left;
                            }
                        }
                    }
                }
            });

            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, []);

            Callback<SteamNetworkingMessagesSessionRequest_t>.Create((SteamNetworkingMessagesSessionRequest_t param) =>
            {

                // if the player is banned reject the session request
                if (isPlayerBanned(new CSteamID(param.m_identityRemote.GetSteamID64())))
                {
                    SteamNetworkingMessages.CloseSessionWithUser(ref param.m_identityRemote);
                    return;
                }

                // get the players WFPlayer object
                WFPlayer player = AllPlayers.Find(p => p.SteamId == param.m_identityRemote.GetSteamID());
                if (player == null)
                {
                    //Log("Player not found!");
                    SteamNetworkingMessages.CloseSessionWithUser(ref param.m_identityRemote);
                    return;
                }

                SteamNetworkingMessages.AcceptSessionWithUser(ref param.m_identityRemote);
                sendWebLobbyPacket(new CSteamID(param.m_identityRemote.GetSteamID64()));

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

                    // man i dont fucking know anymore
                    if (String.Compare("$weblobby_join_request", lobbyMessage) == 0 || lobbyMessage.Trim() == "$weblobby_join_request")
                    {
                        if (AllPlayers.Contains(AllPlayers.Find(p => p.SteamId == userId)))
                        {
                            sendWebLobbyPacket(userId);
                            //Log("User: " + userId + " is already in the lobby!");
                            //Log("If player is stuck on loading screen please tell me (Meepso)");
                            return;
                        }

                        // check if the user is banned 
                        if (isPlayerBanned(userId))
                        {
                            //Log($"Player {userId} tried to join, but they are banned!");
                            // send a steamlobby chat message to the player
                            var rejectMessage = $"$weblobby_request_denied_deny-{userId.m_SteamID}";
                            var rejectData = Encoding.UTF8.GetBytes(rejectMessage);
                            SteamMatchmaking.SendLobbyChatMsg(new CSteamID(callback.m_ulSteamIDLobby), rejectData, rejectData.Count());
                            return;
                        }

                        var acceptMessage = $"$weblobby_request_accepted-{userId.m_SteamID}";
                        var acceptData = Encoding.UTF8.GetBytes(acceptMessage);
                        bool suc = SteamMatchmaking.SendLobbyChatMsg(new CSteamID(callback.m_ulSteamIDLobby), acceptData, acceptData.Count());

                        // make the player a wfplayer
                        WFPlayer player = new WFPlayer(userId, SteamFriends.GetFriendPersonaName(userId), new SteamNetworkingIdentity());
                        AllPlayers.Add(player);
                        
                        // if there is already a player with the same FisherID, remove them from the previous players list to prevent duplicates
                        var sharedIDPrev = PreviousPlayers.Find(p => p.FisherID == player.FisherID);
                        if (sharedIDPrev != null)
                        {
                            PreviousPlayers.Remove(sharedIDPrev); // remove the previous player with the same FisherID
                        }
                        
                        var prev = PreviousPlayers.Find(p => p.SteamId == userId);
                        if (prev != null)
                        {
                            PreviousPlayers.Remove(prev); // remove the previous player if they are already in the list
                        }
                        
                        PreviousPlayers.Add( PreviousPlayer.FromWFPlayer(player) ); // add the player to the previous players list

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

                        membersPacket["weblobby"] = members;
                        sendPacketToPlayers(membersPacket);
                    }
                }
            });

            // add two to the max because the server counts as player and add one overflow player
            if (friendsOnly)
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers + 2);
            else
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxPlayers + 2);
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
                    for (int i = 0; i < 7; i++)
                    {
                        List<NetworkingMessage> messages = ReceiveMessagesOnChannel(i, 50);
                        if (messages.Count == 0)
                            // Skip processing this channel if no messages were received.
                            continue;

                        didWork = true;
                        for (int j = 0; j < messages.Count; j++)
                        {
                            if (i == 3 && messages[j].size > 50000)
                            {
                                string UserName = SteamFriends.GetFriendPersonaName(new CSteamID(messages[j].identity));
                                Log($"[{UserName}] Sent a chalk packet of size {messages[j].size} bytes");
                                Log($"Due to the size of this packet, there is a chance it will not be processed correctly.");
                            }
                            OnNetworkPacket(messages[j].payload, new CSteamID(messages[j].identity));
                        }
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
            sendPacketToPlayers(closePacket);

            loadedPlugins.ForEach(plugin => plugin.plugin.onEnd()); // tell all plugins that the server is closing!

            disconnectAllPlayers();
            SteamMatchmaking.LeaveLobby(SteamLobby);
            SteamAPI.Shutdown();
            Environment.Exit(0);
        }

        /// <summary>
        ///  Handler & parser for player chat messages, calling plugins' onChatMessage hooks.
        /// </summary>
        /// <param name="message">
        /// Chat contents
        /// </param>
        /// <param name="from">
        /// SteamID of the sender
        /// </param>
        /// <param name="local">
        /// Whether the message was marked as by sender
        /// </param>
        /// <param name="position">
        /// Vector3 Position reported by sender
        /// </param>
        /// <param name="zone">Name of zone reported by sender</param>
        /// <returns></returns>
        void OnPlayerChat(
            string message,
            CSteamID from,
            bool local = false,
            Vector3? position = null,
            string zone = "global"
        )
        {
            WFPlayer sender = AllPlayers.Find(p => p.SteamId == from);
            if (sender == null)
            {
                Log($"[UNKNOWN] {from}: {message}");
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

            foreach (PluginInstance p in loadedPlugins)
            {
              p.plugin.handleChatMessage(sender, message, local, position, zone);
            }
        }

        async Task<bool> DownloadWorldFile()
        {
            
            Log("Downloading main_zone.tscn file");

            string downloadURL =
                "https://raw.githubusercontent.com/Blizzardfur-Maxxx/Webfishing-Decompiled/refs/heads/main/Scenes/Map/Zones/main_zone.tscn";
            string worldDir = $"{AppDomain.CurrentDomain.BaseDirectory}worlds";
            HttpClient client = new();

            Directory.CreateDirectory(worldDir); // make the directory just in case.
            
            try
            {
                var resp = await client.GetAsync(downloadURL);
                var fstream = File.Create(worldDir+"/main_zone.tscn");
                var body = await resp.Content.ReadAsStringAsync();
                
                await fstream.WriteAsync(Encoding.UTF8.GetBytes(body));
                fstream.Flush();
                fstream.Close();
                
                Log("zone file downloaded!");
                return true;
            }
            catch (HttpRequestException e)
            {
                Error(e.ToString());
                return false;
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
