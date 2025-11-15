using Cove.Server.Actor;
using Newtonsoft.Json.Linq;
using Steamworks;

namespace Cove.Server;

partial class CoveServer
{
    public void HandleMetaPacket(string ConnectionID, byte[] data)
    {
        // convert the reseaved data to a json object
        var jsonString = System.Text.Encoding.UTF8.GetString(data);
        var jsonObject = JObject.Parse(jsonString);

        // get the action string
        var action = jsonObject["action"]?.ToString();

        switch (action)
        {
            case "ping":
            {
                // send a pong back
                var response = new JObject
                {
                    ["action"] = "pong",
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                SendMetaPacket(ConnectionID, System.Text.Encoding.UTF8.GetBytes(response.ToString()));
                break;
            }
            case "authenticate":
            {
                var steamID = jsonObject["steam_id"]?.ToString();
                var username = jsonObject["username"]?.ToString();

                Log($"Client {ConnectionID} is authenticating as {username} with SteamID: {steamID}");
                // Here you would add your authentication logic
                // but because that would require the steam game server token we have to skip it for now

                var plrSocket = _playerSockets.Find(s => s.ConnectionID == ConnectionID);
                if (plrSocket != null)
                {
                    plrSocket.SteamID = new CSteamID(ulong.Parse(steamID));
                    plrSocket.DisplayName = username;
                }

                var authResponse = new JObject
                {
                    ["action"] = "authenticate_response",
                    ["status"] = "success"
                };

                SendMetaPacket(ConnectionID, System.Text.Encoding.UTF8.GetBytes(authResponse.ToString()));

                // make the player a wfplayer
                WFPlayer player = new WFPlayer(plrSocket.SteamID, plrSocket.DisplayName,
                    new SteamNetworkingIdentity());
                AllPlayers.Add(player);

                // if there is already a player with the same FisherID, remove them from the previous players list to prevent duplicates
                var sharedIDPrev = PreviousPlayers.Find(p => p.FisherID == player.FisherID);
                if (sharedIDPrev != null)
                {
                    PreviousPlayers.Remove(sharedIDPrev); // remove the previous player with the same FisherID
                }

                var prev = PreviousPlayers.Find(p => p.SteamId == plrSocket.SteamID);
                if (prev != null)
                {
                    PreviousPlayers.Remove(prev); // remove the previous player if they are already in the list
                }

                PreviousPlayers.Add(PreviousPlayer.FromWFPlayer(player)); // add the player to the previous players list

                Dictionary<string, object> joinedPacket = new();
                joinedPacket["type"] = "user_joined_weblobby";
                joinedPacket["user_id"] = (long)plrSocket.SteamID.m_SteamID;
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

                break;
            }
            case "members":
            {
                // return a array of current member's steamids and usernames
                var membersArray = new JArray();
                foreach (var plrSocket in _playerSockets)
                {
                    var memberObject = new JObject
                    {
                        ["steam_id"] = plrSocket.SteamID.m_SteamID,
                        ["username"] = plrSocket.DisplayName
                    };
                    membersArray.Add(memberObject);
                }

                // add a blank member to make the game load
                var blankMember = new JObject
                {
                    ["steam_id"] = 0,
                    ["username"] = "Cove"
                };
                membersArray.Add(blankMember);

                var membersResponse = new JObject
                {
                    ["action"] = "members_response",
                    ["members"] = membersArray
                };
                SendMetaPacket(ConnectionID, System.Text.Encoding.UTF8.GetBytes(membersResponse.ToString()));

                break;
            }

            default:
            {
                Log($"Unknown meta action: {action} from client: {ConnectionID}");
                break;
            }
        }
    }

    // Meta packets are json objects that have a "m" infrount of them
    public void SendMetaPacket(string ConnectionID, byte[] data)
    {
        byte[] metaPrefix = System.Text.Encoding.UTF8.GetBytes("M");
        byte[] packetData = new byte[metaPrefix.Length + data.Length];
        Buffer.BlockCopy(metaPrefix, 0, packetData, 0, metaPrefix.Length);
        Buffer.BlockCopy(data, 0, packetData, metaPrefix.Length, data.Length);

        // get the length of the packet data
        UInt32 packetLength = (UInt32)packetData.Length;

        // find the socket via the ConnectionID
        var socket = _playerSockets.Find(s => s.ConnectionID == ConnectionID);
        if (socket != null)
        {
            // send the packet length first
            byte[] lengthBytes = BitConverter.GetBytes(packetLength);
            // join the length bytes and the packet data
            byte[] finalPacket = new byte[lengthBytes.Length + packetData.Length];
            Buffer.BlockCopy(lengthBytes, 0, finalPacket, 0, lengthBytes.Length);
            Buffer.BlockCopy(packetData, 0, finalPacket, lengthBytes.Length, packetData.Length);
            socket.Stream.Write(finalPacket, 0, finalPacket.Length);
        }
        else
        {
            Log($"Failed to send meta packet to {ConnectionID}: Socket not found");
        }
    }
    
    public void SendWebfishingPacket(string ConnectionID, byte[] data)
    {
        byte[] webfishingPrefix = System.Text.Encoding.UTF8.GetBytes("W");
        byte[] packetData = new byte[webfishingPrefix.Length + data.Length];
        Buffer.BlockCopy(webfishingPrefix, 0, packetData, 0, webfishingPrefix.Length);
        Buffer.BlockCopy(data, 0, packetData, webfishingPrefix.Length, data.Length);

        // get the length of the packet data
        UInt32 packetLength = (UInt32)packetData.Length;

        // find the socket via the ConnectionID
        var socket = _playerSockets.Find(s => s.ConnectionID == ConnectionID);
        if (socket != null)
        {
            // send the packet length first
            byte[] lengthBytes = BitConverter.GetBytes(packetLength);
            // join the length bytes and the packet data
            byte[] finalPacket = new byte[lengthBytes.Length + packetData.Length];
            Buffer.BlockCopy(lengthBytes, 0, finalPacket, 0, lengthBytes.Length);
            Buffer.BlockCopy(packetData, 0, finalPacket, lengthBytes.Length, packetData.Length);
            socket.Stream.Write(finalPacket, 0, finalPacket.Length);
        }
        else
        {
            Log($"Failed to send webfishing packet to {ConnectionID}: Socket not found");
        }
    }
    
}