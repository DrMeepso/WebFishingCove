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

namespace Cove.GodotFormat
{
    public class GodotWriter
    {

        public static byte[] WriteGodotPacket(Dictionary<string, object> packet)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(stream);

            writeDictionary(packet, bw);

            return stream.ToArray();
        }

        private static void writeAny(object packet, BinaryWriter bw)
        {
            if (packet == null)
            {
                bw.Write(0);
            }
            else if (packet is Dictionary<string, object>)
            {
                writeDictionary((Dictionary<string, object>)packet, bw);
            }
            else if (packet is string)
            {
                writeString((string)packet, bw);
            }
            else if (packet is int)
            {
                writeInt((int)packet, bw);
            }
            else if (packet is long)
            {
                writeLong((long)packet, bw);
            }
            else if (packet is Single)
            {
                writeSingle((Single)packet, bw);
            }
            else if (packet is Double)
            {
                writeDouble((Double)packet, bw);
            }
            else if (packet is bool)
            {
                writeBool((bool)packet, bw);
            }
            else if (packet is Dictionary<int, object>)
            {
                writeArray((Dictionary<int, object>)packet, bw);
            }
            else if (packet is Vector3)
            {
                writeVector3((Vector3)packet, bw);
            } else if (packet is Vector2)
            {
                writeVector2((Vector2)packet, bw);
            } else
            {
                throw new Exception("Unknown type: " + packet.GetType());
            }
        }

        private static void writeVector2(Vector2 packet, BinaryWriter bw)
        {
            bw.Write((int)5); // write v2 header

            bw.Write((Single)packet.x);
            bw.Write((Single)packet.y);
        }

        private static void writeVector3(Vector3 packet, BinaryWriter bw)
        {
            bw.Write((int)7); // write v3 header

            bw.Write((Single)packet.x);
            bw.Write((Single)packet.y);
            bw.Write((Single)packet.z);
        }

        private static void writeBool(bool packet, BinaryWriter bw)
        {
            bw.Write((int)1);
            bw.Write(packet ? 1 : 0);
        }

        private static void writeInt(int packet, BinaryWriter writer)
        {
            writer.Write((int)GodotTypes.intValue); // write the int value header!
            writer.Write((int)packet);
        }

        private static void writeLong(long packet, BinaryWriter writer)
        {
            writer.Write(65538); // write the int value header! this is the same as above but with the 64 bit header!
            writer.Write((long)packet);
        }

        private static void writeSingle(Single packet, BinaryWriter writer)
        {
            writer.Write((int)3);
            writer.Write((Single)packet);
        }

        private static void writeDouble(Double packet, BinaryWriter writer)
        {
            writer.Write((int)65539);// write the float value header! this is the same as above but with the 64 bit header!
            writer.Write((Double)packet);
        }

        private static void writeString(string packet, BinaryWriter bw)
        {
            bw.Write((int)4); // remeber to write the string header!

            byte[] bytes = Encoding.UTF8.GetBytes(packet);

            bw.Write((int)bytes.Length);
            // get the ammount to pad by!

            // Step 3: Write the actual bytes of the string
            bw.Write(bytes);

            // Step 4: Calculate padding needed to make the total length a multiple of 4
            int padding = (4 - (bytes.Length % 4)) % 4; // Calculate padding

            // Step 5: Write padding bytes (if needed)
            for (int i = 0; i < padding; i++)
            {
                bw.Write((byte)0); // Write padding as zero bytes
            }
        }

        private static void writeArray(Dictionary<int, object> packet, BinaryWriter writer)
        {
            // because we have a dic we need to write the correct byte info!
            writer.Write((int)19); // make sure these are 4 bits as that is what godot is exspecting!
            writer.Write((int)packet.Count);

            for (int i = 0; i < packet.Count; i++)
            {
                writeAny(packet[i], writer);
            }
        }

        private static void writeDictionary(Dictionary<string, object> packet, BinaryWriter writer)
        {
            // because we have a dic we need to write the correct byte info!
            writer.Write((int)18); // make sure these are 4 bits as that is what godot is exspecting!
            writer.Write((int)packet.Count);

            foreach (KeyValuePair<string, object> pair in packet)
            {
                writeAny(pair.Key, writer);
                writeAny(pair.Value, writer);
            }
        }
    }
}
