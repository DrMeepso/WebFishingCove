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
using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            foreach (CSteamID player in getAllPlayers())
            {
                if (player == SteamUser.GetSteamID())
                    continue;


                sendPacketToPlayer(packet, player);
            }
        }

        public void sendPacketToPlayer(Dictionary<string, object> packet, CSteamID id)
        {
            byte[] packetBytes = writePacket(packet);


            // get the wfPlayer object
            var player = AllPlayers.Find(p => p.SteamId.m_SteamID == id.m_SteamID);
            if (player == null) return;

            if (player.identity.GetSteamID64() == 0)
            {
                return;
            }

            GCHandle handle = GCHandle.Alloc(packetBytes, GCHandleType.Pinned);
            IntPtr dataPointer = handle.AddrOfPinnedObject();

            // enum CHANNELS { 
            //     ACTOR_UPDATE , 
            //     ACTOR_ACTION , 
            //     GAME_STATE , 
            //     CHALK , 
            //     GUITAR , 
            //     ACTOR_ANIMATION , 
            //     SPEECH , 
            // } 
            int channel = packet.ContainsKey("channel") ? (int)packet["channel"] : 2;

            SteamNetworkingMessages.SendMessageToUser(
                ref player.identity,
                dataPointer,
                (uint)packetBytes.Length,
                8,
                channel
            );


            handle.Free(); // free the handle
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

        public class NetworkingMessage
        {
            public byte[] payload { get; set; }
            public int size { get; set; }
            //public CSteamID connection { get; set; }
            public ulong identity { get; set; }
            public ulong receiver_user_data { get; set; }
            public ulong time_received { get; set; }
            public ulong message_number { get; set; }
            public int channel { get; set; }
            public int flags { get; set; }
            public ulong sender_user_data { get; set; }
        }

        public List<NetworkingMessage> ReceiveMessagesOnChannel(int channel, int maxMessages)
        {
            List<NetworkingMessage> messages = new List<NetworkingMessage>();

            nint[] messagePointers = new nint[maxMessages];

            int availableMessages = SteamNetworkingMessages.ReceiveMessagesOnChannel(channel, messagePointers, maxMessages);

            for (int i = 0; i < availableMessages; i++)
            {
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messagePointers[i]);

                byte[] data = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);

                /*
                Dictionary<string, object> msgDict = new Dictionary<string, object>
                {
                    { "payload", data }, // Message payload
                    { "size", message.m_cbSize },
                    { "connection", message.m_conn.ToString() },
                    { "identity", GetSteamIDFromIdentity(message.m_identityPeer) },
                    { "receiver_user_data", (ulong)message.m_nConnUserData },
                    { "time_received", (ulong)message.m_usecTimeReceived.m_SteamNetworkingMicroseconds },
                    { "message_number", (ulong)message.m_nMessageNumber },
                    { "channel", message.m_nChannel },
                    { "flags", message.m_nFlags },
                    { "sender_user_data", (ulong)message.m_nUserData }
                }
                */

                NetworkingMessage msgDict = new()
                {
                    payload = data,
                    size = message.m_cbSize,
                    //connection = ((uint)message.m_conn),
                    identity = GetSteamIDFromIdentity(message.m_identityPeer),
                    receiver_user_data = (ulong)message.m_nConnUserData,
                    time_received = (ulong)message.m_usecTimeReceived.m_SteamNetworkingMicroseconds,
                    message_number = (ulong)message.m_nMessageNumber,
                    channel = message.m_nChannel,
                    flags = message.m_nFlags,
                    sender_user_data = (ulong)message.m_nUserData
                };

                messages.Add(msgDict);
                SteamNetworkingMessage_t.Release(messagePointers[i]);
            }

            return messages;
        }

        // Helper function to get SteamID from identity
        private ulong GetSteamIDFromIdentity(SteamNetworkingIdentity identity)
        {
            return identity.GetSteamID64(); // Return 0 if no SteamID found
        }

    }

}
