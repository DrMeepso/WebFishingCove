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
        byte[] metaPrefix = System.Text.Encoding.UTF8.GetBytes("m");
        byte[] packetData = new byte[metaPrefix.Length + data.Length];
        Buffer.BlockCopy(metaPrefix, 0, packetData, 0, metaPrefix.Length);
        Buffer.BlockCopy(data, 0, packetData, metaPrefix.Length, data.Length);
        
        // get the length of the packet data
        int packetLength = packetData.Length;
        
        // find the socket via the ConnectionID
        var socket = _playerSockets.Find(s => s.ConnectionID == ConnectionID);
        if (socket != null)
        {
            // send the packet length first
            byte[] lengthBytes = BitConverter.GetBytes(packetLength);
            socket.Stream.Write(lengthBytes, 0, lengthBytes.Length);
            // send the packet data
            socket.Stream.Write(packetData, 0, packetData.Length);
        } else
        {
            Log($"Failed to send meta packet to {ConnectionID}: Socket not found");
        }
    }
    
}