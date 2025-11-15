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
    
    public PlayerSocket(TcpClient socket)
    {
        Socket = socket;
        Stream = socket.GetStream();
    }
}