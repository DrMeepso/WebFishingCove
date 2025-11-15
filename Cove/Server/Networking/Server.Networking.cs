namespace Cove.Server;

partial class CoveServer
{
    
    public void HandleMetaPacket(string clientId, byte[] data)
    {
        // meta packets are used for server-client communication that is not game related
        // like ping, server info, etc.
        // this is just a placeholder for now
    }
    
}