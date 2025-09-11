using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;
using Steamworks;

public class ChatCommands : CovePlugin
{
    CoveServer Server { get; set; } // lol
    public ChatCommands(CoveServer server) : base(server)
    {
        Server = server;
    }

    // save the time the server was started
    public long serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();

    public override void onInit()
    {
        base.onInit();

        RegisterCommand(command: "users", aliases: ["players"], callback: (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            // Get the command arguments
            int pageNumber = 1;
            int pageSize = 10;

            // Arg[0] = page
            if (args.Length > 0 && (!int.TryParse(args[0], out pageNumber) || pageNumber < 1))
                pageNumber = 1;
            
            // Arg[1] = page size (optional)
            if (args.Length > 1 && int.TryParse(args[1], out int customSize) && customSize > 0 && customSize <= 100)
                pageSize = customSize;
            
            var allPlayers = GetAllPlayers()
                .OrderBy(p => p.Username, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.FisherID, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int totalPlayers = allPlayers.Count;
            if (totalPlayers == 0)
            {
                // server only response
                SendPlayerChatMessage(player, "No players online.");
                return;
            }
            
            int totalPages = (int)Math.Ceiling(totalPlayers / (double)pageSize);
            if (totalPages == 0) totalPages = 1; // safety
            
            // Ensure the page number is within the valid range
            if (pageNumber > totalPages) pageNumber = totalPages;
            if (pageNumber < 1) pageNumber = 1;

            int skip = (pageNumber - 1) * pageSize;
            var playersOnPage = allPlayers.Skip(skip).Take(pageSize);
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Players in the server:");
            foreach (var p in playersOnPage)
                sb.AppendLine($"{p.Username}: {p.FisherID}");

            sb.Append($"Page {pageNumber} of {totalPages} (Total: {totalPlayers})");
            SendPlayerChatMessage(player, sb.ToString());
        });
        SetCommandDescription("users", "Shows all players in the server");

        RegisterCommand("spawn", (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            if (args.Length == 0)
            {
                SendPlayerChatMessage(player, "You didn't add an argument!");
                return;
            }
            var actorType = args[0].ToLower();
            bool spawned = false;
            switch (actorType)
            {
                case "rain":
                    Server.spawnRainCloud();
                    spawned = true;
                    break;
                case "fish":
                    Server.spawnFish();
                    spawned = true;
                    break;
                case "meteor":
                case "meatball":
                    spawned = true;
                    Server.spawnFish("fish_spawn_alien");
                    break;
                case "portal":
                    Server.spawnVoidPortal();
                    spawned = true;
                    break;
                case "metal":
                    Server.spawnMetal();
                    spawned = true;
                    break;
            }
            if (spawned)
            {
                SendPlayerChatMessage(player, $"Spawned {actorType}");
            }
            else
            {
                SendPlayerChatMessage(player, $"\"{actorType}\" is not a spawnable actor!");
            }
        });
        SetCommandDescription("spawn", "Spawns an actor");

        RegisterCommand("setjoinable", (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            string arg = args[0].ToLower();
            if (arg == "true")
            {
                SteamMatchmaking.SetLobbyJoinable(Server.SteamLobby, true);
                SendPlayerChatMessage(player, $"Opened lobby!");
                if (!Server.codeOnly)
                {
                    SteamMatchmaking.SetLobbyData(Server.SteamLobby, "type", "public");
                    SendPlayerChatMessage(player, $"Unhid server from server list");
                }
            }
            else if (arg == "false")
            {
                SteamMatchmaking.SetLobbyJoinable(Server.SteamLobby, false);
                SendPlayerChatMessage(player, $"Closed lobby!");
                if (!Server.codeOnly)
                {
                    SteamMatchmaking.SetLobbyData(Server.SteamLobby, "type", "code_only");
                    SendPlayerChatMessage(player, $"Hid server from server list");
                }
            }
            else
            {
                SendPlayerChatMessage(player, $"\"{arg}\" is not true or false!");
            }
        });
        SetCommandDescription("setjoinable", "Sets the lobby to joinable or not");

        RegisterCommand("refreshadmins", (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            Server.readAdmins();
        });
        SetCommandDescription("refreshadmins", "Refreshes the admin list");

        RegisterCommand("uptime", (player, args) =>
        {
            long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            long uptime = currentTime - serverStartTime;
            TimeSpan time = TimeSpan.FromSeconds(uptime);
            int days = time.Days;
            int hours = time.Hours;
            int minutes = time.Minutes;
            int seconds = time.Seconds;
            string uptimeString = "";
            if (days > 0)
            {
                uptimeString += $"{days} Days, ";
            }
            if (hours > 0)
            {
                uptimeString += $"{hours} Hours, ";
            }
            if (minutes > 0)
            {
                uptimeString += $"{minutes} Minutes, ";
            }
            if (seconds > 0)
            {
                uptimeString += $"{seconds} Seconds";
            }
            SendPlayerChatMessage(player, $"Server uptime: {uptimeString}");
        });
        SetCommandDescription("uptime", "Shows the server uptime");

        RegisterCommand("say", (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            string message = string.Join(" ", args);
            SendGlobalChatMessage($"[Server] {message}");
        });
        SetCommandDescription("say", "Sends a message to all players");

        RegisterCommand(command: "chalkrecent", aliases: ["recentchalk"], callback: (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;

            // rearange the list so the most recent is first
            List<WFPlayer> reversedList = new List<WFPlayer>(lastToUseChalk);
            reversedList.Reverse();

            string message = "Most recent chalk users:";
            foreach (var p in reversedList)
            {
                message += $"\n{p.Username}: {p.FisherID}";
            }

            SendPlayerChatMessage(player, message);
        });
        SetCommandDescription("chalkrecent", "Shows the 10 most recent players to use chalk");

        RegisterCommand("reload", (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            
            SendPlayerChatMessage(player, "Reloading plugins, this can cause unintended behaviour in plugins that don't cleanup properly");

            foreach(PluginInstance plugin in Server.loadedPlugins)
            {
                plugin.plugin.onEnd();
                Log($"Unloaded plugin {plugin.plugin.GetType().Name}");
            }

            Server.loadedPlugins.Clear(); // clear the list

            Server.RegisterDefaultCommands(); // re-register default commands
            
            Server.loadAllPlugins(true); // reload all plugins

            SendPlayerChatMessage(player, "Plugins have been reloaded!");
        });
        SetCommandDescription("reload", "Reloads all plugins");

        RegisterCommand("plugins", (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            string message = "Loaded plugins:";
            foreach (PluginInstance plugin in Server.loadedPlugins)
            {
                message += $"\n{plugin.pluginName} - {plugin.pluginAuthor}";
            }
            SendPlayerChatMessage(player, message);
        });
        SetCommandDescription("plugins", "Shows all loaded plugins");
        
        RegisterCommand("steam", (player, args) =>
        {
            if (!IsPlayerAdmin(player)) return;
            // Get the command arguments
            if (args.Length == 0)
            {
                SendPlayerChatMessage(player, "Username or Fisher ID required!");
                return;
            }
            
            var playerIdent = string.Join(" ", args);
            var plr = Server.GetPlayer(playerIdent);
            if (plr == null)
            {
                SendPlayerChatMessage(player, $"No player found with username or Fisher ID \"{playerIdent}\"");
                return;
            }
            SendPlayerChatMessage(player, $"{plr.Username}: {plr.FisherID} - SteamID: {plr.SteamId.m_SteamID}");
        });
    }

    private List<WFPlayer> lastToUseChalk = new();
    public override void onNetworkPacket(WFPlayer sender, Dictionary<string, object> packet)
    {
        base.onNetworkPacket(sender, packet);

        object value;
        if (packet.TryGetValue("type", out value))
        {
            if (typeof(string) != value.GetType()) return;

            string type = value as string;
            if (type == "chalk_packet")
            {
                lastToUseChalk.Add(sender);
                if (lastToUseChalk.Count > 10)
                {
                    lastToUseChalk.RemoveAt(0);
                }
            }
        }
    }

    /// <summary>
    /// Performs plugin shutdown tasks and unregisters commands registered by this plugin.
    /// </summary>
    /// <remarks>
    /// Calls the base class shutdown logic, then removes all chat commands that were added during initialization
    /// so the plugin can be cleanly reloaded without leaving stale command handlers registered.
    /// </remarks>
    public override void onEnd()
    {
        base.onEnd();

        // unregister all commands
        // this is needed to allow for the plugin to be reloaded!
        UnregisterCommand("users");
        UnregisterCommand("spawn");
        UnregisterCommand("kick");
        UnregisterCommand("ban");
        UnregisterCommand("setjoinable");
        UnregisterCommand("refreshadmins");
        UnregisterCommand("uptime");
        UnregisterCommand("say");
        UnregisterCommand("chalkrecent");
        UnregisterCommand("reload");
        UnregisterCommand("plugins");
        UnregisterCommand("steam");
    }
}
