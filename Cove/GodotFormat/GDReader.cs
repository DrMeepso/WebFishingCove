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

namespace Cove.GodotFormat
{

    public class GodotReader
    {

        public byte[] data;
        private BinaryReader reader;
        public Serilog.Core.Logger logger;

        public GodotReader(byte[] data, Serilog.Core.Logger serverLogger)
        {
            this.data = data;
            reader = new BinaryReader(new MemoryStream(data), System.Text.Encoding.UTF8);
            logger = serverLogger;
        }

        public Dictionary<string, object> readPacket()
        {
            int type = reader.ReadInt32();

            if (type != (int)GodotTypes.dictionaryValue)
            {
                //throw new Exception("Unable to decode a non-dictionary godot packet!");
            }

            Dictionary<string, object> dic = new Dictionary<string, object>();

            try
            {
                dic = readDictionary();
            }
            catch (Exception e)
            {
                logger.Error("-- Error reading packet! --"); // incase we do have a error!
                logger.Error(e.ToString()); // incase we do have a error!
            }

            return dic;
        }

        private Object readNext()
        {
            int v = reader.ReadInt32();

            int type = v & 0xFFFF;
            int flags = v >> 16;

            switch (type)
            {
                case (int)GodotTypes.nullValue:
                    return null;

                case (int)GodotTypes.dictionaryValue:
                    return readDictionary();

                case (int)GodotTypes.arrayValue:
                    return readArray();

                case (int)GodotTypes.stringValue:
                    return readString();

                case (int)GodotTypes.intValue:
                    return readInt(flags);

                case (int)GodotTypes.vector3Value:
                    return readVector3();

                case (int)GodotTypes.quatValue:
                    return readQuat();

                case (int)GodotTypes.boolValue:
                    return readBool();

                case (int)GodotTypes.floatValue:
                    return readFloat(flags);

                case (int)GodotTypes.planeValue:
                    return readPlane();

                case (int)GodotTypes.vector2Value:
                    return readVector2();

                default:
                    logger.Error($"Unable to handel object of type: {type}");
                    return new ReadError();
            }
        }

        private Plane readPlane()
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float dist = reader.ReadSingle();

            return new Plane(x, y, z, dist);
        }

        private double readFloat(int flags)
        {
            if ((flags & 1) == 1)
            {
                return reader.ReadDouble();
            }
            else
            {
                return reader.ReadSingle();
            }
        }

        private bool readBool()
        {
            return reader.ReadInt32() != 0;
        }

        private Quat readQuat()
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float w = reader.ReadSingle();
            Quat quat = new Quat(x, y, z, w);

            return quat;
        }

        private System.Numerics.Vector2 readVector2()
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            System.Numerics.Vector2 newVec = new(x, y);

            return newVec;
        }

        private System.Numerics.Vector3 readVector3()
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            System.Numerics.Vector3 newVec = new(x, y, z);

            return newVec;
        }

        private long readInt(int flags)
        {
            if ((flags & 1) == 1)
            {
                return reader.ReadInt64();
            }
            else
            {
                int v = reader.ReadInt32();
                return v;
            }
        }

        private string readString()
        {
            int stringLength = reader.ReadInt32();
            char[] stringValue = reader.ReadChars(stringLength);

            // this feild is padded to 4 bytes
            if (4 - ((int)reader.BaseStream.Position % 4) != 4)
            {
                reader.ReadBytes(4 - ((int)reader.BaseStream.Position % 4));
            }

            return new string(stringValue);
        }

        private Dictionary<int, object> readArray()
        {
            Dictionary<int, object> array = new Dictionary<int, object>();

            int elementCount = reader.ReadInt32() & 0x7FFFFFFF;

            for (int i = 0; i < elementCount; i++)
            {
                object ins = readNext(); // read the next thing
                array[i] = ins;
            }

            return array;
        }

        private Dictionary<string, object> readDictionary()
        {

            Dictionary<string, object> dic = new Dictionary<string, object>();

            int elementCount = reader.ReadInt32() & 0x7FFFFFFF;

            for (int i = 0; i < elementCount; i++)
            {
                object keyValue = readNext();
                var key = "NullValue";

                if (keyValue == null || !(keyValue is String)) // if the value is not a string (bad read) break the loop.
                {
                    logger.Error("READ ERROR, KEY PROVIDED IS NOT A STRING!");
                    break; //break from the loop to save the server!
                }
                else
                {
                    key = keyValue.ToString(); // ITS A STRING I SWEAR TO GOD! PLEASEEE
                }

                object value = readNext();

                dic[key] = value;
            }

            return dic;
        }

    }

}