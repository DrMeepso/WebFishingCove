using Cove.GodotFormat;
using Serilog;
using System;

namespace Cove.Server.Chalk
{
    public class ChalkCanvas
    {

        public long canvasID;
        public Dictionary<Vector2, int> chalkImage = new Dictionary<Vector2, int>();

        public ChalkCanvas(long canvasID)
        {
            this.canvasID = canvasID;
        }

        public void drawChalk(Vector2 position, int color)
        {
            int[] allowedCanvas = { 0, 1, 2, 3 };
            if (!Array.Exists(allowedCanvas, element => element == canvasID))
            {
                return;
            }

            chalkImage[position] = color;
        }

        public Dictionary<int, object> getChalkPacket()
        {

            Dictionary<int, object> packet = new Dictionary<int, object>();
            ulong i = 0;
            foreach (KeyValuePair<Vector2, int> entry in chalkImage.ToList())
            {
                Dictionary<int, object> arr = new();
                arr[0] = entry.Key;
                arr[1] = entry.Value;
                packet[(int)i] = arr;
                i++;
            }

            return packet;
        }

        // The ammount of chalk pixels needed to trigger the fallback process
        const int TRIGGER_FALLBACK_CHALK_SIZE = 100000; // around .3mb worth of chalk data

        public void chalkUpdate(Dictionary<int, object> packet)
        {
            // This callback is made when there is too much chalk data
            // it runs under the assumption that the newest chalk data is at the end of the packet
            // Webfishing will set pixels multiple times in the chalk data
            // assuming that the chalk data is in order, we can just take the last value
            // Because the newest data is at the end of the packet we update till we reach the point
            // of a pixel being set multiple times
            if (packet.Count >= TRIGGER_FALLBACK_CHALK_SIZE)
            {
                List<Vector2> expendedPixels = new List<Vector2>(chalkImage.Keys);
                for (int i = packet.Count; i >= 0; i--)
                {
                    Dictionary<int, object> arr = (Dictionary<int, object>)packet[i-1];
                    Vector2 vector2 = (Vector2)arr[0];
                    Vector2 pos = new Vector2((int)Math.Round(vector2.x), (int)Math.Round(vector2.y));

                    if (expendedPixels.Contains(pos))
                    {
                        break;
                    }

                    expendedPixels.Add(pos);
                    chalkImage[pos] = (int)((Int64)arr[1]);
                }

                Console.WriteLine("A packet that was passed to the Fallback function was processed successfully");

            }
            else
            {
                for (int i = 0; i < packet.Count; i++)
                {
                    Dictionary<int, object> arr = (Dictionary<int, object>)packet[i];
                    Vector2 vector2 = (Vector2)arr[0];
                    Vector2 pos = new Vector2((int)Math.Round(vector2.x), (int)Math.Round(vector2.y));

                    chalkImage[pos] = (int)((Int64)arr[1]);
                }
            }
        }

        public void clearCanvas()
        {
            chalkImage.Clear();
        }
    }
}
