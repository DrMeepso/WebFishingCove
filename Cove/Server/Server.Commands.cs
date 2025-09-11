using Cove.Server.Actor;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Cove.Server
{

    public class RegisteredCommand
    {
        public string Command;
        public string Description;
        public Action<WFPlayer, string[]> Callback;
        public string[] Aliases;

        public RegisteredCommand(string command, string description, Action<WFPlayer, string[]> callback, string[]? aliases = null)
        {
            Command = command.ToLower(); // make sure its lower case to not mess anything up
            Description = description;
            Callback = callback;
            Aliases = aliases ?? [];
            Aliases = [.. Aliases.Select(alias => alias.ToLower())];
        }

        public void Invoke(WFPlayer player, string[] args)
        {
            Callback(player, args);
        }

    }

    public partial class CoveServer
    {
        List<RegisteredCommand> Commands = [];

        public WFPlayer GetPlayer(string playerIdent)
        {
            var selectedPlayer = AllPlayers.ToList().Find(p => p.Username.Equals(playerIdent, StringComparison.OrdinalIgnoreCase));
            // if there is no player with the username try to find someone with that fisher ID
            if (selectedPlayer == null)
                selectedPlayer = AllPlayers.ToList().Find(p => p.FisherID.Equals(playerIdent, StringComparison.OrdinalIgnoreCase));

            return selectedPlayer;
        }

        /// <summary>
        /// Registers the server's built-in chat/console commands (help, exit/shutdown, kick, ban, prev/recent).
        /// </summary>
        /// <remarks>
        /// Adds command handlers to the server's Commands collection:
        /// - help: lists available commands and their descriptions.
        /// - exit (alias: shutdown): host-only shutdown command.
        /// - kick: admin-only; accepts a username or SteamID and disconnects the target.
        /// - ban: admin-only; accepts a username, previous-player identifier (prefixed with '#'), or SteamID and bans the target. Supports an optional quoted ban reason (text between the first and last double quotes) which is forwarded to the ban mechanism; if a SteamID is provided the ban is applied directly. If a matching previous player is found by FisherID, the command can accept "#FisherID" to ban that previous player.
        /// - prev (alias: recent): admin-only; lists recently disconnected players (within ~10 minutes).
        ///
        /// These handlers perform permission checks (host/admin) and send feedback via the server messaging functions. They also call server actions such as Stop(), kickPlayer(...), and banPlayer(...).
        /// </remarks>
        public void RegisterDefaultCommands()
        {
            RegisterCommand("help", (player, args) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Commands:");
                foreach (var cmd in Commands)
                {
                    sb.AppendLine($"{cmd.Command} - {cmd.Description}");
                }
                messagePlayer(sb.ToString(), player.SteamId);
            });
            SetCommandDescription("help", "Shows all commands");

            RegisterCommand(command: "exit", aliases: ["shutdown"], cb: (player, args) =>
            {
                // make sure the player is the host
                if (player.SteamId != serverPlayer.SteamId)
                {
                    messagePlayer("You are not the host!", player.SteamId);
                    return;
                }
                messagePlayer("Server is shutting down!", player.SteamId);

                Stop(); // stop the server

            });
            SetCommandDescription("exit", "Shuts down the server (host only)");

            RegisterCommand("kick", (player, args) =>
            {
                if (!isPlayerAdmin(player.SteamId)) return;
                string playerIdent = string.Join(" ", args);
                // try find a user with the username first
                var kickedplayer = GetPlayer(playerIdent);

                if (kickedplayer == null && System.Text.RegularExpressions.Regex.IsMatch(playerIdent, @"^7656119\d{10}$"))
                {
                    // if it is a steam ID, try to find the player by steam ID
                    CSteamID steamId = new CSteamID(Convert.ToUInt64(playerIdent));
                    messagePlayer($"Kicked {playerIdent}", player.SteamId);
                    kickPlayer(steamId);
                    return;
                }

                if (kickedplayer == null)
                {
                    messagePlayer("That's not a player!", player.SteamId);
                }
                else
                {
                    messagePlayer($"Kicked {kickedplayer.Username}", player.SteamId);
                    kickPlayer(kickedplayer.SteamId);
                }
            });
            SetCommandDescription("kick", "Kicks a player from the server");

            RegisterCommand("ban", (player, args) =>
            {
                if (!isPlayerAdmin(player.SteamId)) return;
                // hacky fix,
                // Extract player name from the command message
                string playerIdent;// = string.Join(" ", args);
                
                string rawArgs = string.Join(" ", args);
                string banReason = string.Empty;

                var numQuotesInArgs = rawArgs.Count(c => c == '"');
                var hasBanReason = numQuotesInArgs >= 2;
                // While we'd hope admins use delimiters properly, it's actually totally fine for this case if they
                // e.g. use quotes inside quotes to quote the target's offending message within the banReason
                if (hasBanReason)
                {
                    var firstQuoteIndex = rawArgs.IndexOf('"');
                    var lastQuoteIndex = rawArgs.LastIndexOf('"');
                    banReason = rawArgs
                        .Substring(firstQuoteIndex + 1, lastQuoteIndex - firstQuoteIndex - 1)
                        .Trim();
                    rawArgs = rawArgs.Remove(
                        firstQuoteIndex,
                        lastQuoteIndex - firstQuoteIndex + 1
                    );
                }
                else if (rawArgs.Length > 2)
                {
                    messagePlayer("Error banning player: If you want to add a reason, please wrap it in quotes.", player.SteamId);
                }
                playerIdent = rawArgs.Trim();
                
                // try to find a user with the username first
                var playerToBan = GetPlayer(playerIdent);
                
                var previousPlayer = PreviousPlayers.ToList().Find(p => p.FisherID.Equals(playerIdent, StringComparison.OrdinalIgnoreCase));
                if (previousPlayer != null && playerToBan == null)
                {
                    messagePlayer($"There is a previous player with that name, if you meant to ban them add a # before the ID: #{playerIdent}", player.SteamId);
                    return;
                }
                    
                previousPlayer = PreviousPlayers.ToList().Find(p => $"#{p.FisherID}".Equals(playerIdent, StringComparison.OrdinalIgnoreCase));
                if (previousPlayer != null)
                {
                    playerToBan = new WFPlayer(previousPlayer.SteamId, previousPlayer.Username, new SteamNetworkingIdentity())
                    {
                        FisherID = previousPlayer.FisherID,
                        Username = previousPlayer.Username,
                    };
                }
                
                // use regex to check if its a steam ID
                if (playerToBan == null && System.Text.RegularExpressions.Regex.IsMatch(playerIdent, @"^7656119\d{10}$"))
                {
                    // if it is a steam ID, try to find the player by steam ID
                    CSteamID steamId = new CSteamID(Convert.ToUInt64(playerIdent));
                    if (isPlayerBanned(steamId))
                        banPlayer(steamId, false, banReason);
                    else
                        banPlayer(steamId, true, banReason); // save to file if they are not already in there!
                    
                    messagePlayer($"Banned player with Steam ID {playerIdent}", player.SteamId);
                    return;
                }
                
                if (playerToBan == null)
                {
                    messagePlayer("Player not found!", player.SteamId);
                }
                else
                {

                    if (isPlayerBanned(playerToBan.SteamId))
                        banPlayer(playerToBan.SteamId, false, banReason);
                    else
                        banPlayer(playerToBan.SteamId, true, banReason); // save to file if they are not already in there!

                    messagePlayer($"Banned {playerToBan.Username}", player.SteamId);
                    messageGlobal($"{playerToBan.Username} has been banned from the server.");
                }
            });
            SetCommandDescription("ban", "Bans a player from the server");

            RegisterCommand(command:"prev", aliases: ["recent"], cb: (player, args) =>
            {
                if (!isPlayerAdmin(player.SteamId)) return;
                var sb = new StringBuilder();
                sb.AppendLine("Previous Players:");
                foreach (var prevPlayer in PreviousPlayers)
                {
                    if (prevPlayer.State == PlayerState.InGame) continue;

                    // we dont want to show players that left more than 10 minutes ago
                    if ((DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(prevPlayer.leftTimestamp).UtcDateTime)
                            .TotalMinutes > 10)
                    {
                        continue;
                    }

                    // get the time since the player left in a human readable format
                    string timeLeft =
                        $"{Math.Round((DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(prevPlayer.leftTimestamp).UtcDateTime).TotalMinutes)} minutes ago";
                    sb.Append($"{prevPlayer.Username} ({prevPlayer.FisherID}) - Left: {timeLeft}\n");
                }
                messagePlayer(sb.ToString(), player.SteamId);
            });
            SetCommandDescription("prev", "Shows a list of previous players that were connected to the server");

        }

        public void RegisterCommand(string command, Action<WFPlayer, string[]> cb, string[]? aliases = null)
        {
            aliases ??= [];

            if (Commands.Any(c => c.Command == command))
            {
                Log($"Command '{command}' is already registerd!");
                return;
            }
            else if (aliases.Any(alias => Commands.Find(c => c.Aliases.Contains(alias)) != null))
            {
                Log($"'{command}' has an alias that is already registerd elsewhere!");
                return;
            }

            Commands.Add(new RegisteredCommand(command, "", cb, aliases));

        }

        public void UnregisterCommand(string command)
        {
            Commands.RemoveAll(c => c.Command == command);
        }

        public void SetCommandDescription(string command, string description)
        {
            var cmd = Commands.Find(c => c.Command == command);
            if (cmd == null)
            {
                Log($"Command '{command}' not found!");
                return;
            }
            cmd.Description = description;
        }

        public void InvokeCommand(WFPlayer player, string command, string[] args)
        {
            var cmd = FindCommand(command);
            if (cmd == null)
            {
                Log($"Command '{command}' not found!");
                return;
            }
            cmd.Invoke(player, args);
        }

        public bool DoseCommandExist(string command)
        {
            var cmd = FindCommand(command);
            if (cmd == null)
                return false;

            return true;
        }

        public RegisteredCommand? FindCommand(string name)
        {
            return Commands.Find(c =>
                c.Command == name.ToLower() || c.Aliases.Contains(name.ToLower())
            );
        }
    }
}
