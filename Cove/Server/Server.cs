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
using Cove.Server.Actor;
using Cove.Server.Utils;
using Microsoft.Extensions.Hosting;
using Cove.Server.HostedServices;
using Microsoft.Extensions.Logging;
using Vector3 = Cove.GodotFormat.Vector3;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Unicode;
using System.Text;
using System.Reflection;
using System.Threading.Channels;
using Cove.Server.Networking;

namespace Cove.Server
{
    public partial class CoveServer
    {
        public Serilog.Core.Logger logger;

        public string WebFishingGameVersion = "1.12"; // make sure to update this when the game updates!
        public int MaxPlayers = 20;
        public string ServerName = "A Cove Dedicated Server";

        public string LobbyCode = new string(Enumerable.Range(0, 5)
            .Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());

        public bool codeOnly = true;
        public bool ageRestricted = false;
        public bool maskMaxPlayers = false;

        public string joinMessage =
            "This is a Cove dedicated server!\nPlease report any issues to the github (xr0.xyz/cove)";

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

        public TcpListener TCPServer;
        private List<PlayerSocket> _playerSockets = new();

        public void Init()
        {
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
                loadAllPlugins(true);
            }
            else
            {
                Directory.CreateDirectory($"{AppDomain.CurrentDomain.BaseDirectory}plugins");
                Log("Created plugins folder!");
            }


            serverPlayer = new WFPlayer(new CSteamID(0), "CoveServer", new SteamNetworkingIdentity());

            TCPServer = TcpListener.Create(6767); // make this a config option later
            TCPServer.Start();

            Log("TCP Server started on port 6767");

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
            Logger<HostSpawnMetalService> hostSpawnMetalServiceLogger =
                new Logger<HostSpawnMetalService>(loggerFactory);

            // Create the services that we need to run.
            IHostedService actorUpdateService = new ActorUpdateService(actorServiceLogger, this);
            IHostedService hostSpawnService = new HostSpawnService(hostSpawnServiceLogger, this);
            IHostedService hostSpawnMetalService = new HostSpawnMetalService(hostSpawnMetalServiceLogger, this);

            // add them to the services dictionary so we can access them later if needed
            services["actor_update"] = actorUpdateService;
            services["host_spawn"] = hostSpawnService;
            services["host_spawn_metal"] = hostSpawnMetalService;

            while (true)
            {
                TcpClient client = TCPServer.AcceptTcpClient();
                PlayerSocket ps = new PlayerSocket(client);
                lock (_playerSockets)
                    _playerSockets.Add(ps);
            }
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

        async void RunNetwork()
        {
            while (true)
            {
                bool didWork = false;
                try
                {
                    // loop though all player sockets and check for packets
                    // iterate backwards so we can safely remove sockets while iterating
                    // take a snapshot of the socket list to avoid locking while doing IO
                    PlayerSocket[] socketSnapshot;
                    lock (_playerSockets)
                    {
                        socketSnapshot = _playerSockets.ToArray();
                    }

                    foreach (PlayerSocket ps in socketSnapshot)
                    {
                        // if the socket is no longer connected, handle disconnect and continue
                        if (!ps.IsConnected())
                        {
                            HandlePlayerDisconnect(ps, "socket disconnected");
                            continue;
                        }

                        NetworkStream stream = ps.Stream;

                        // if the stream has already been disposed/closed, skip it
                        if (stream == null || !stream.CanRead)
                        {
                            HandlePlayerDisconnect(ps, "stream closed");
                            continue;
                        }

                        try
                        {
                            // Accessing DataAvailable on a disposed stream can throw, so guard it
                            bool hasData;
                            try
                            {
                                hasData = stream.DataAvailable;
                            }
                            catch (ObjectDisposedException)
                            {
                                HandlePlayerDisconnect(ps, "stream disposed");
                                continue;
                            }

                            while (hasData)
                            {
                                didWork = true;
                                try
                                {
                                    // each packet is prefixed with a 4 byte int for the length and a 'W' or 'M' byte
                                    byte[] packet = NetworkUtils.ReadPacket(stream);

                                    if (packet.Length == 0)
                                    {
                                        Log($"[TCP] Connection {ps.ConnectionID}: empty packet");
                                        break;
                                    }

                                    char packetType = (char)packet[0];
                                    byte[] payload = packet.Skip(1).ToArray();

                                    switch (packetType)
                                    {
                                        case 'W':
                                            if (ps.IsAuthenticated)
                                            {
                                                var pkt = readPacket(payload);

                                                if (pkt == null)
                                                {
                                                    Log($"[TCP] Connection {ps.ConnectionID}: readPacket returned null");
                                                    break;
                                                }

                                                Dictionary<string, object>? payloadDict = null;
                                                if (pkt.ContainsKey("payload"))
                                                    payloadDict = pkt["payload"] as Dictionary<string, object>;

                                                if (payloadDict == null)
                                                {
                                                    Log($"[TCP] Connection {ps.ConnectionID}: packet payload missing or invalid");
                                                    break;
                                                }

                                                long identity = pkt.ContainsKey("identity") ? Convert.ToInt64(pkt["identity"]) : 0;

                                                object targetObj = pkt.ContainsKey("target") ? pkt["target"] : "all";
                                                string targetStr = targetObj?.ToString() ?? "all";

                                                switch (targetStr)
                                                {
                                                    case "steamlobby":
                                                    case "all":
                                                        foreach (var player in _playerSockets)
                                                        {
                                                            sendPacketToPlayer(payloadDict, player.SteamID, identity);
                                                        }
                                                        OnNetworkPacket(payloadDict, ps.SteamID);
                                                        break;
                                                    case "peers":
                                                        foreach (var player in _playerSockets)
                                                        {
                                                            if (player.SteamID != ps.SteamID)
                                                            {
                                                                sendPacketToPlayer(payloadDict, player.SteamID, identity);
                                                            }
                                                        }
                                                        OnNetworkPacket(payloadDict, ps.SteamID);
                                                        break;
                                                    default:
                                                        if (UInt64.TryParse(targetStr, out ulong targetSteamID))
                                                        {
                                                            sendPacketToPlayer(payloadDict, new CSteamID(targetSteamID), identity);
                                                        }
                                                        else
                                                        {
                                                            Log($"[TCP] Connection {ps.ConnectionID}: unknown target '{targetStr}' in 'W' packet");
                                                        }

                                                        break;
                                                }
                                            }
                                            else
                                            {
                                                Log(
                                                    $"[TCP] Connection {ps.ConnectionID}: received 'W' packet from unauthenticated connection, ignoring.");
                                            }

                                            break;

                                        case 'M':
                                            HandleMetaPacket(ps.ConnectionID, payload);
                                            break;

                                        default:
                                            Log($"[TCP] Connection {ps.ConnectionID}: unknown packet type '{packetType}'");
                                            break;
                                    }
                                }
                                catch (System.IO.IOException ioex)
                                {
                                    Log($"[TCP] Connection {ps.ConnectionID}: stream IO error, treating as disconnect: {ioex.Message}");
                                    HandlePlayerDisconnect(ps, ioex.Message);
                                    break;
                                }
                                catch (SocketException sex)
                                {
                                    Log($"[TCP] Connection {ps.ConnectionID}: socket error, treating as disconnect: {sex.Message}");
                                    HandlePlayerDisconnect(ps, sex.Message);
                                    break;
                                }
                                catch (ObjectDisposedException)
                                {
                                    HandlePlayerDisconnect(ps, "stream disposed during read");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Log($"[TCP] Error while reading/handling packet from connection {ps.ConnectionID}: {ex}");
                                    break;
                                }

                                // re-check DataAvailable safely at the end of the loop
                                try
                                {
                                    hasData = stream.DataAvailable;
                                }
                                catch (ObjectDisposedException)
                                {
                                    HandlePlayerDisconnect(ps, "stream disposed after read");
                                    break;
                                }
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            HandlePlayerDisconnect(ps, "stream disposed (outer)");
                            continue;
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

        // Called when a TCP PlayerSocket disconnects or its stream fails.
        void HandlePlayerDisconnect(PlayerSocket ps, string reason = "disconnected")
        {
            try
            {
                // remove socket from list
                lock (_playerSockets)
                {
                    _playerSockets.Remove(ps);
                }

                // close resources
                try { ps.Stream?.Close(); } catch { }
                try { ps.Socket?.Close(); } catch { }

                // notify server logic if we have an associated player
                if (ps.SteamID.m_SteamID != 0)
                {
                    WFPlayer p = AllPlayers.Find(x => x.SteamId == ps.SteamID);
                    if (p != null)
                    {
                        Log($"[{p.FisherID}] {p.Username} disconnected ({reason})");

                        // notify plugins
                        foreach (var plugin in loadedPlugins)
                        {
                            plugin.plugin.onPlayerLeave(p);
                        }

                        // inform other players
                        Dictionary<string, object> leftPacket = new();
                        leftPacket["type"] = "peer_left"; // client-side can use this to remove player
                        leftPacket["user_id"] = (long)p.SteamId.m_SteamID;
                        sendPacketToPlayers(leftPacket);

                        // remove player from server lists
                        AllPlayers.Remove(p);

                        // remove player from actor lists if present
                        allActors.RemoveAll(a => a is WFPlayer wp && wp.SteamId == p.SteamId);

                        updatePlayercount();
                    }
                    else
                    {
                        Log($"[TCP] Disconnected socket for SteamID {ps.SteamID.m_SteamID} but no WFPlayer found.");
                    }
                }
                else
                {
                    Log($"[TCP] Disconnected unauthenticated socket {ps.ConnectionID}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error handling disconnected player/socket: {ex}");
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

        public bool ModeratePacket(CSteamID senderId, Dictionary<string, object> packet)
        {
            return true;
        }
    }
}
