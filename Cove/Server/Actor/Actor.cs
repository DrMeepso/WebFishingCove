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

namespace Cove.Server.Actor
{
    public class WFActor
    {
        public long InstanceID { get; set; }
        public string Type { get; }
        public DateTimeOffset SpawnTime = DateTimeOffset.UtcNow;

        public Vector3 pos { get; set; }
        public Vector3 rot { get; set; }

        public string zone = "main_zone";
        public int zoneOwner = -1;

        public int despawnTime = -1;
        public bool despawn = true;

        public CSteamID owner = new CSteamID(0); // 0 is the server

        public WFActor(long ID, string Type, Vector3 entPos, Vector3 entRot = null)
        {
            InstanceID = ID;
            this.Type = Type;
            pos = entPos;
            if (entRot != null)
                rot = entRot;
            else
                rot = Vector3.zero;
        }

        public virtual void onUpdate()
        {

        }
    }
}