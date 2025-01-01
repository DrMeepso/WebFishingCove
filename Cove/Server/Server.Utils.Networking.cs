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
using Cove.GodotFormat;
using Cove.Server.Utils;
using Cove.Server.Actor;

namespace Cove.Server
{
    partial class CoveServer
    {
        Dictionary<string, object> readPacket(byte[] packetBytes)
        {
            return (new GodotReader(packetBytes, logger)).readPacket();
        }

        byte[] writePacket(Dictionary<string, object> packet)
        {
            byte[] godotBytes = GodotWriter.WriteGodotPacket(packet);
            return GzipHelper.CompressGzip(godotBytes);
        }

        public void sendPacketToPlayers(Dictionary<string, object> packet)
        {
            byte[] packetBytes = writePacket(packet);
            
            foreach (CSteamID player in getAllPlayers())
            {
                if (player == SteamUser.GetSteamID())
                    continue;
                
                SteamNetworking.SendP2PPacket(player, packetBytes, (uint)packetBytes.Length, EP2PSend.k_EP2PSendReliable, nChannel: 2);
            }
        }

        public void sendPacketToPlayer(Dictionary<string, object> packet, CSteamID id)
        {
            byte[] packetBytes = writePacket(packet);
            SteamNetworking.SendP2PPacket(id, packetBytes, (uint)packetBytes.Length, EP2PSend.k_EP2PSendReliable, nChannel: 2);
        }

        public CSteamID[] getAllPlayers()
        {
            WFPlayer[] AllPlayersNow = AllPlayers.ToArray();

            int playerCount = AllPlayersNow.Length;
            CSteamID[] players = new CSteamID[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                players[i] = AllPlayersNow[i].SteamId;
            }

            return players;
        }
    }
}
