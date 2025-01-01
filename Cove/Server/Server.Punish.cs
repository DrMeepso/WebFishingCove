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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cove.Server.Actor;
using Steamworks;

namespace Cove.Server
{
    public partial class CoveServer
    {

        public void banPlayer(CSteamID id, bool saveToFile = false)
        {
            Dictionary<string, object> banPacket = new();
            banPacket["type"] = "ban";

            sendPacketToPlayer(banPacket, id);

            if (saveToFile)
                writeToBansFile(id);

            sendBlacklistPacketToAll(id.m_SteamID.ToString());
        }

        public bool isPlayerBanned(CSteamID id)
        {
            string fileDir = $"{AppDomain.CurrentDomain.BaseDirectory}bans.txt";

            string[] fileContent = File.ReadAllLines(fileDir);
            foreach (string line in fileContent)
            {
                if (line.Contains(id.m_SteamID.ToString()))
                {
                    return true;
                }
            }

            return false;
        }

        private void writeToBansFile(CSteamID id)
        {
            string fileDir = $"{AppDomain.CurrentDomain.BaseDirectory}bans.txt";
            WFPlayer player = AllPlayers.Find(p => p.SteamId == id);
            File.AppendAllLines(fileDir, [$"{id.m_SteamID} #{player.Username}"]);
        }

        public void kickPlayer(CSteamID id)
        {
            Dictionary<string, object> kickPacket = new();
            kickPacket["type"] = "kick";

            sendPacketToPlayer(kickPacket, id);
        }

    }
}
