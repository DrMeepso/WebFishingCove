using System.Numerics;
using Vector2 = Cove.GodotFormat.Vector2;

namespace Cove.Server.Chalk
{
    public class ChalkCanvas
    {
        public long canvasID;
        public CoveServer server;
        public Dictionary<Vector2, long> chalkImage = new Dictionary<Vector2, long>();

        public ChalkCanvas(long canvasID, CoveServer server)
        {
            this.canvasID = canvasID;
            this.server = server;
        }

        private void drawChalk(Dictionary<int, object> transformations)
        {
            int[] allowedCanvas = { 0, 1, 2, 3 };
            if (!Array.Exists(allowedCanvas, element => element == canvasID))
            {
                // TODO: Should not silently error this...
                return;
            }

            var chalkData = new Dictionary<int, object>();
            for (int i = 0; i < transformations.Count; i++)
            {
                var transformation = (Dictionary<int, object>)transformations[i];
                var coords = (Vector2)transformation[0];
                var color = (long)transformation[1];
                chalkImage[coords] = color;
                chalkData[i] = new Dictionary<int, object> { { 0, coords }, { 1, color } };
            }

            server.sendPacketToPlayers(
                new Dictionary<string, object>
                {
                    { "type", "chalk_packet" },
                    { "canvas_id", canvasID },
                    { "data", chalkData },
                    { "channel", 3 },
                }
            );
        }

        /// <summary>
        /// Generate a packet containing the canvas chalk data
        /// </summary>
        /// <returns>A dictionary containing the chalk data.</returns>
        public Dictionary<int, object> getChalkPacket()
        {
            Dictionary<int, object> packet = new Dictionary<int, object>();
            ulong i = 0;
            foreach (KeyValuePair<Vector2, long> entry in chalkImage.ToList())
            {
                Dictionary<int, object> arr = new();
                arr[0] = entry.Key;
                arr[1] = entry.Value;
                packet[(int)i] = arr;
                i++;
            }

            return packet;
        }

        /// <summary>
        /// Update the chalk image on the server.
        /// </summary>
        /// <param name="chalkData">A dictionary containing the chalk data, where each key is an index and each value is a dictionary containing a position (Vector2) and a color (long).</param>
        public void chalkUpdate(Dictionary<int, object> chalkData)
        {
            this.drawChalk(chalkData);
        }

        /// <summary>
        /// Update a single chalk canvas cell
        /// </summary>
        /// <param name="chalkData">A dictionary containing the chalk data, where each key is an index and each value is a dictionary containing a position (Vector2) and a color (long).</param>
        public void chalkUpdate(Vector2 position, long color)
        {
            this.drawChalk(
                new Dictionary<int, object>
                {
                    {
                        0,
                        new Dictionary<int, object> { { 0, position }, { 1, color } }
                    },
                }
            );
        }

        public void clearCanvas()
        {
            chalkImage.Clear();
            // TODO: Emit changes
        }
    }
}
