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
using System.Numerics;

namespace Cove.Server.Actor
{
    public class RainCloud : WFActor
    {

        public Vector3 toCenter;
        public float wanderDirection;

        public bool isStaic = false;

        public RainCloud(int ID, Vector3 entPos) : base(ID, "raincloud", Vector3.Zero)
        {
            pos = entPos;

            toCenter = Vector3.Normalize(pos - new Vector3(30, 40, -50));
            wanderDirection = MathF.Atan2(toCenter.X, toCenter.Z);
            despawn = true;
            despawnTime = 540;
        }

        public override void onUpdate()
        {
            if (isStaic) return; // for rain that wont move
            
            float newX = -MathF.Cos(wanderDirection);
            float newY = -MathF.Sin(wanderDirection);
            pos -= new Vector3(newX, 0, newY) * (0.17f / 6f);
        }
    }
}
