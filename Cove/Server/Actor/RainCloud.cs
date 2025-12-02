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
using Cove.GodotFormat;

namespace Cove.Server.Actor
{
    public class RainCloud : WFActor
    {

        public Vector3 toCenter;
        public float wanderDirection;

        public bool isStaic = false;

        public RainCloud(int ID, Vector3 entPos) : base(ID, "raincloud", Vector3.zero)
        {
            pos = entPos;

            toCenter = (pos - new Vector3(30, 40, -50)).Normalized();
            wanderDirection = new Vector2(toCenter.x, toCenter.z).Angle();
            despawn = true;
            despawnTime = 540;
        }

        public override void onUpdate()
        {
            if (isStaic) return; // for rain that dont move

            Vector2 dir = new Vector2(-1, 0).Rotate(wanderDirection) * (0.17f / 6f);
            pos += new Vector3(dir.x, 0, dir.y);
        }
    }
}
