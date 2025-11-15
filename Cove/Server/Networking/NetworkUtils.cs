using System.Net.Sockets;

namespace Cove.Server.Networking;

public class NetworkUtils
{

    public static byte[] ReadPacket(NetworkStream stream)
    {
        // read all data from the stream, assuming that each packet is prefixed with its length as a 4-byte integer
        // first, read the 4-byte length prefix
        Span<byte> lengthBuffer = stackalloc byte[4];
        int read = 0;
        while (read < 4)
        {
            int r = stream.Read(lengthBuffer.Slice(read));
            if (r == 0)
            {
                // connection closed
                throw new IOException("Connection closed while reading packet length");
            }

            read += r;
        }

        int length = BitConverter.ToInt32(lengthBuffer);
        if (length <= 0)
        {
            throw new IOException($"Invalid packet length: {length}");
        }

        byte[] buffer = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int r = stream.Read(buffer, offset, length - offset);
            if (r == 0)
            {
                // connection closed
                throw new IOException("Connection closed while reading packet body");
            }
            offset += r;
        }

        return buffer;
    }
    
}