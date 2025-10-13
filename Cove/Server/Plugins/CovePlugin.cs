using Cove.Server.Actor;

namespace Cove.Server.Plugins
{
    public class CovePlugin
    {

        public CoveServer ParentServer;

        public CovePlugin(CoveServer parent)
        {
            ParentServer = parent;
        }

        // triggered when the plugin is started
        public virtual void onInit() { }
        // triggered when the plugin is stopped or the server is stopped
        public virtual void onEnd() { }
        // triggered 12/s
        public virtual void onUpdate() { }
        // triggered when a player speaks in anyway (exluding / commands)
        public virtual void onChatMessage(WFPlayer sender, string message) { }
        // triggered when a player enters the server
        public virtual void onPlayerJoin(WFPlayer player) { }
        // triggered when a player leaves the server
        public virtual void onPlayerLeave(WFPlayer player) { }
        /// <summary>
        /// Triggered when a player is banned from the server
        /// </summary>
        /// <param name="player">If unknown, this will be an empty string</param>
        /// <param name="reason">If not given, this will be an empty string</param>
        public virtual void onPlayerBanned(WFPlayer player, string reason) { }
		// triggered when a packet arrives
		public virtual void onNetworkPacket(WFPlayer sender, Dictionary<string, object> packet) { }

        public WFPlayer[] GetAllPlayers()
        {
            return ParentServer.AllPlayers.ToArray();
        }

        public void SendPlayerChatMessage(WFPlayer receiver, string message)
        {
            // remove a # in case its given
            ParentServer.messagePlayer(message, receiver.SteamId);
        }

        public void SendGlobalChatMessage(string message)
        {
            ParentServer.messageGlobal(message);
        }

        public WFActor[] GetAllServerActors()
        {
            return ParentServer.serverOwnedInstances.ToArray();
        }

        public WFActor? GetActorFromID(int id)
        {
            return ParentServer.serverOwnedInstances.Find(a => a.InstanceID == id);
        }

        // please make sure you use the correct actorname or the game freaks out!
        public WFActor SpawnServerActor(string actorType)
        {
            return ParentServer.spawnGenericActor(actorType);
        }

        public void RemoveServerActor(WFActor actor)
        {
            ParentServer.removeServerActor(actor);
        }

        // i on god dont know what this dose to the actual actor but it works in game, so if you nee this its here
        public void SetServerActorZone(WFActor actor, string zoneName, int zoneOwner)
        {
            ParentServer.setActorZone(actor, zoneName, zoneOwner);
        }

        public void KickPlayer(WFPlayer player)
        {
            ParentServer.kickPlayer(player.SteamId);
        }

        public void BanPlayer(WFPlayer player, string reason = "")
        {
            ParentServer.banPlayer(player.SteamId, !ParentServer.isPlayerBanned(player.SteamId), reason);
        }

        public void Log(string message)
        {
            ParentServer.printPluginLog(message, this);
        }

        public bool IsPlayerAdmin(WFPlayer player)
        {
            return ParentServer.isPlayerAdmin(player.SteamId);
        }

        public void SendPacketToPlayer(Dictionary<string, object> packet, WFPlayer player)
        {
            ParentServer.sendPacketToPlayer(packet, player.SteamId);
        }

        public void SendPacketToAll(Dictionary<string, object> packet)
        {
            ParentServer.sendPacketToPlayers(packet);
        }

        public void RegisterCommand(string command, Action<WFPlayer, string[]> callback, string[]? aliases = null)
        {
            ParentServer.RegisterCommand(command, callback, aliases);
        }

        /// Back-compat overload for existing plugins compiled against the older API
        public void RegisterCommand(string command, Action<WFPlayer, string[]> callback)
        {
            ParentServer.RegisterCommand(command, callback);
        }

        public void SetCommandDescription(string command, string description)
        {
            ParentServer.SetCommandDescription(command, description);
        }

        public void UnregisterCommand(string command)
        {
            ParentServer.UnregisterCommand(command);
        }

        public bool DoesCommandExist(string command)
        {
            return ParentServer.DoseCommandExist(command);
        }
    }
}
