using System.Net.Sockets;

namespace Cove.Server.Networking;

public class NetworkUtils
{

    public static byte[] ReadPacket(NetworkStream stream)
    {
        // read all data from the stream, assuming that each packet is prefixed with its length as a 4-byte integer
        return new byte[0]; //TODO: implement packet reading
    }
    
}