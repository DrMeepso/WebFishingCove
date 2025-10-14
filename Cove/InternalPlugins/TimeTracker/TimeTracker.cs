using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;
using LiteDB;

namespace Cove.InternalPlugins.TimeTracker;
public class PlayerPlayTime
{
    [BsonId]
    public long SteamID { get; set; }          // primary key
    public long TotalPlayTime { get; set; }     // in seconds
    public long LastSessionStart { get; set; }  // Unix timestamp of the last session start
}

public class TimeTracker : CovePlugin
{
    public TimeTracker(CoveServer server) : base(server)
    {
        _steamIdPlayTime = new LiteDatabase(@"TimeTracker.db");
        _playerJoinTime = new Dictionary<ulong, DateTimeOffset>();
        Server = server;
    }
    
    CoveServer Server { get; set; } // lol
    private LiteDatabase _steamIdPlayTime;
    private Dictionary<ulong, DateTimeOffset> _playerJoinTime;
    
    public override void onInit()
    {
        base.onInit();
        
        // print out all existing records for debugging
        var col = _steamIdPlayTime.GetCollection<PlayerPlayTime>("playtime");
        var records = col.FindAll();
        foreach (var record in records)
        {
            Log($"Existing record: SteamID {record.SteamID}, TotalPlayTime {record.TotalPlayTime} seconds, LastSessionStart {record.LastSessionStart}");
        }
        
        RegisterCommand(command:"playtime", aliases:[], callback: (player, args) =>
        {
            var totalPlayTime = GetPlayerTotalPlayTime(player.SteamId.m_SteamID);
            
            // Format the playtime into hours, minutes, seconds
            TimeSpan timeSpan = TimeSpan.FromSeconds(totalPlayTime);
            string formattedTime = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", 
                timeSpan.Hours + (timeSpan.Days * 24), // include days in hours
                timeSpan.Minutes, 
                timeSpan.Seconds);
            SendPlayerChatMessage(player, formattedTime);
        });
        SetCommandDescription("playtime", "Check your total playtime on this server.");
        
        Log("TimeTracker plugin initialized ⌚");
    }

    public override void onEnd()
    {
        base.onEnd();
        UnregisterCommand("playtime");
        
        
        // save all currently online players' playtime
        var col = _steamIdPlayTime.GetCollection<PlayerPlayTime>("playtime");
        foreach (var player in Server.getAllPlayers())
        {
            var playTime = new PlayerPlayTime
            {
                SteamID = (long)player.m_SteamID,
                TotalPlayTime = GetPlayerTotalPlayTime(player.m_SteamID),
                LastSessionStart = _playerJoinTime[player.m_SteamID].ToUnixTimeSeconds()
            };
            playTime.LastSessionStart = _playerJoinTime[player.m_SteamID].ToUnixTimeSeconds();
            col.Upsert(playTime); // insert or update the record
        }
        
        Log("TimeTracker plugin shutting down, saved all online players' playtime.");
        
        _steamIdPlayTime?.Dispose();
        _steamIdPlayTime = null;
        _playerJoinTime.Clear();
        _playerJoinTime = null;
        
    }

    public override void onPlayerJoin(WFPlayer player)
    {
        base.onPlayerJoin(player);
        if (player.SteamId.m_SteamID == 0) return; // not logged in with steam
        
        // Record the join time
        _playerJoinTime[player.SteamId.m_SteamID] = DateTimeOffset.UtcNow;
    }

    public override void onPlayerLeave(WFPlayer player)
    {
        base.onPlayerLeave(player);
        if (player.SteamId.m_SteamID == 0) return; // not logged in with steam
        
        
        var playTime = new PlayerPlayTime
        {
            SteamID = (long)player.SteamId.m_SteamID,
            TotalPlayTime = GetPlayerTotalPlayTime(player.SteamId.m_SteamID),
            LastSessionStart = _playerJoinTime[player.SteamId.m_SteamID].ToUnixTimeSeconds()
        };
        playTime.LastSessionStart = _playerJoinTime[player.SteamId.m_SteamID].ToUnixTimeSeconds();
        
        var col = _steamIdPlayTime.GetCollection<PlayerPlayTime>("playtime");
        col.Upsert(playTime); // insert or update the record
        
        _playerJoinTime.Remove(player.SteamId.m_SteamID); // clean up
    }
    
    public long GetPlayerTotalPlayTime(ulong steamId)
    {
        if (steamId == 0) return 0; // not logged in with steam
        
        var dbKey = (long)steamId;
        var col = _steamIdPlayTime.GetCollection<PlayerPlayTime>("playtime");
        PlayerPlayTime record = col.FindById(dbKey);
        
        // if the player is currently online, add the current session time
        long currentSessionTime = _playerJoinTime.ContainsKey(steamId) ? (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _playerJoinTime[steamId].ToUnixTimeSeconds()) : 0;
        return record?.TotalPlayTime + currentSessionTime ?? 0;
    }
    
}