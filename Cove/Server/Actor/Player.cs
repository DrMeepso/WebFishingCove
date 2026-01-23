/*
   Copyright 2024 DrMeepso

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/


using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cove.GodotFormat;

namespace Cove.Server.Actor
{
    public class WFPlayer : WFActor
    {
        public CSteamID SteamId { get; set; }
        public string FisherID { get; set; }

        public string Username
        {
            get
            {
                if (overrideUsername != "")
                    return overrideUsername;
                return SteamFriends.GetFriendPersonaName(SteamId);
            }
            init => overrideUsername = value;
        }

        private string overrideUsername = ""; // for if we need to override a players username
        public List<CSteamID> blockedPlayers = new List<CSteamID>();
        public SteamNetworkingIdentity identity;
        public WFPlayer(CSteamID id, SteamNetworkingIdentity identity) : base(0, "player", Vector3.zero)
        {
            SteamId = id;
            string randomID = new string(Enumerable.Range(0, 3).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
            FisherID = randomID;

            owner = id;

            pos = new Vector3(0, 0, 0);
            despawn = false; // players down despawn!
            this.identity = identity;
        }
    };

    public enum PlayerState
    {
        InGame,
        Left,
    }
    
    // Represents a player that was previously connected to the server
    public class PreviousPlayer
    {
        public CSteamID SteamId { get; set; }
        public string FisherID { get; set; }
        public string Username { get; set; }
        
        public uint leftTimestamp { get; set; } = 0; // Timestamp when the player left, 0 if they are still connected
        public PlayerState State { get; set; } = PlayerState.InGame; // Current state of the player
        
        public PreviousPlayer(CSteamID id, string fisherName, string FishID)
        {
            SteamId = id;
            FisherID = FishID;
            Username = fisherName;
        }
        
        public static PreviousPlayer FromWFPlayer(WFPlayer player)
        {
            return new PreviousPlayer(
                player.SteamId,
                player.Username,
                player.FisherID
            );
        }
    }
    
}
