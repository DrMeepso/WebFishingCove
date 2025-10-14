using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;

namespace Cove.InternalPlugins;

// Change the namespace and class name!
public class EntityPlugin : CovePlugin
{
    public EntityPlugin(CoveServer server) : base(server) { }

    private WFActor testActor;
    
    public override void onInit()
    {
        base.onInit();
        Log("We are so back");
    }

    public override void onNetworkPacket(WFPlayer sender, Dictionary<string, object> packet)
    {
        base.onNetworkPacket(sender, packet);


        var wantedTypes = new List<string>();
            
        var packetType = (string)packet["type"];
        if (packetType != "instance_actor") return;
        //CoveServer.printStringDict(packet);
    }

    public override void onEnd()
    {
        base.onEnd();
    }
}