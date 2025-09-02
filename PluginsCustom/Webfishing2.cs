using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;
using Steamworks;

namespace PluginsCustom
{
    public sealed class Webfishing2 : CovePlugin
    {
    CoveServer Server { get; set; }
        public Webfishing2(CoveServer server) : base(server)
        {
            Server = server;
        }

        public override void onInit()
        {
            base.onInit();
            Log("Plugin loaded!");
        }

        public override void onEnd()
        {
            base.onEnd();
            // make sure you unload anything or stop any threads here!
            // this is called when !reload is called or when the server ends
            // if you wanna make sure your plugin reloads properly, make sure to clean up here!
            Log("Plugin unloaded!");
        }

        public override void onChatMessage(WFPlayer sender, string message)
        {
            base.onChatMessage(sender, message);
            Log($"{sender.Username}: {message}");
        }
    }
}
