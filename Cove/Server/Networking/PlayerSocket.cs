using System.Net.Sockets;
using Steamworks;

namespace Cove.Server.Networking;

public class PlayerSocket
{
    public TcpClient Socket;
    public NetworkStream Stream;
    
    public CSteamID SteamID;
    public string DisplayName;
    public bool IsAuthenticated = false;
    
    public string ConnectionID;
    
    public PlayerSocket(TcpClient socket)
    {
        Socket = socket;
        Stream = socket.GetStream();
        
        ConnectionID = Guid.NewGuid().ToString();
    }

    // Returns true if the socket appears to still be connected.
    // This avoids relying on TcpClient.Connected, which is not reliable.
    public bool IsConnected()
    {
        try
        {
            if (Socket == null || !Socket.Connected)
                return false;

            if (!Socket.Client.Poll(0, SelectMode.SelectRead))
                return true; // no data yet and not marked readable -> assume connected

            // If Poll says data is available, check if a zero-length read would occur
            return Socket.Client.Available != 0;
        }
        catch
        {
            return false;
        }
    }
}