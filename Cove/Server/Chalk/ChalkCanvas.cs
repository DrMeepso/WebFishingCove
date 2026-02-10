using System.Numerics;
using Steamworks;
using Vector2 = Cove.GodotFormat.Vector2;

namespace Cove.Server.Chalk
{
    public enum COLOR : long
    {
        BLACK,
        WHITE,
        RED,
        BLUE,
        YELLOW,
        SPECIAL,
        GREEN,
        NONE = -1,
    };

    public class ChalkCanvas
    {
        public long canvasID;
        private readonly CoveServer server;
        public readonly Dictionary<Vector2, long> chalkImage = new Dictionary<Vector2, long>();

        public ChalkCanvas(long canvasID, CoveServer server)
        {
            this.canvasID = canvasID;
            this.server = server;
        }

        /// <summary>
        /// Update the chalk canvas image with the given transformations
        /// </summary>
        /// <param name="transformations">A dictionary containing the transformations, where each key is an index and each value is a dictionary containing a position (Vector2) and a color (long).</param>
        /// <remarks>Only canvases with IDs {0, 1, 2, 3} are allowed to be updated.</remarks>
        private void updateCanvasImage(Dictionary<int, object> transformations)
        {
            int[] allowedCanvas = { 0, 1, 2, 3 };
            if (!Array.Exists(allowedCanvas, element => element == canvasID))
            {
                // TODO: Should not silently error this...
                return;
            }

            foreach (var idx in transformations.Keys)
            {
                var transformation = (Dictionary<int, object>)transformations[idx];
                var coords = (Vector2)transformation[0];
                var color = (long)transformation[1];
                chalkImage[coords] = color;
            }
        }

        /// <summary>
        /// Generate chalkData from the canvas image for use in a chalk_update packet
        /// </summary>
        /// <returns>A dictionary containing the chalk data.</returns>
        /// <remarks>If caller is going to broadcast the chalkData consider using ChalkCanvas.emitChalk instead.</remarks>
        public Dictionary<int, object> getChalkPacket()
        {
            Dictionary<int, object> cells = new Dictionary<int, object>();
            ulong i = 0;
            foreach (KeyValuePair<Vector2, long> entry in chalkImage.ToList())
            {
                Dictionary<int, object> arr = new();
                arr[0] = entry.Key;
                arr[1] = entry.Value;
                cells[(int)i] = arr;
                i++;
            }
            return cells;
        }

        /// <summary>
        /// Update the chalk canvas image, without broadcasting the change
        /// </summary>
        /// <param name="chalkData">A dictionary containing the chalk data, where each key is an index and each value is a dictionary containing a position (Vector2) and a color (long).</param>
        /// <remarks>This is meant to be called only by the server when handling incoming chalk packets...</remarks>
        public void chalkUpdate(Dictionary<int, object> chalkData)
        {
            this.updateCanvasImage(chalkData);
        }

        /// <summary>
        /// Copy/paste some existing chalkData onto the chalk canvas and broadcast the change
        /// </summary>
        /// <param name="chalkData"></param>
        public void chalkUpdate(Dictionary<Vector2, long> chalkData)
        {
            foreach (var transformation in chalkData.ToList())
            {
                var position = transformation.Key;
                var color = transformation.Value;
                this.updateCanvasImage(
                    new Dictionary<int, object>
                    {
                        {
                            0,
                            new Dictionary<int, object> { { 0, position }, { 1, color } }
                        },
                    }
                );
                emitChalk();
            }
        }

        /// <summary>
        /// Update a single chalk canvas cell, without broadcasting the change
        /// </summary>
        /// <param name="chalkData">A dictionary containing the chalk data, where each key is an index and each value is a dictionary containing a position (Vector2) and a color (long).</param>
        public void chalkUpdate(Vector2 position, long color)
        {
            this.updateCanvasImage(
                new Dictionary<int, object>
                {
                    {
                        0,
                        new Dictionary<int, object> { { 0, position }, { 1, color } }
                    },
                }
            );
        }

        /// <summary>
        /// Clear the chalk canvas and optionally broadcast the change
        /// </summary>
        /// <param name="emitChanges">Whether to emit the changes to players. Default: true</param>
        public void clearCanvas(bool emitChanges = true)
        {
            var transformations = getChalkPacket();
            for (int i = 0; i < transformations.Count; i++)
            {
                var cell = (Dictionary<int, object>)transformations[i];
                cell[1] = COLOR.NONE;
            }
            drawChalk(transformations);
            if (emitChanges)
            {
                emitChalk();
            }
        }

        /// <summary>
        /// Draw and broadcast given chalk updates
        /// </summary>
        /// <param name="transformations"></param>
        public void drawChalk(Dictionary<int, object> transformations)
        {
            updateCanvasImage(transformations);
            var cells = new List<Dictionary<int, object>>();
            foreach (var idx in transformations.Keys)
            {
                var transformation = (Dictionary<int, object>)transformations[idx];
                var coords = (Vector2)transformation[0];
                var color = (long)transformation[0];
                cells.Append(new Dictionary<int, object> { { 0, coords }, { 1, color } });
            }
            var chalkData = new Dictionary<int, object>();
            for (int i = 0; i < cells.Count; i++)
            {
                chalkData[i] = cells[i];
            }
            server.sendPacketToPlayers(
                new Dictionary<string, object>
                {
                    { "type", "chalk_packet" },
                    { "canvas_id", canvasID },
                    { "data", chalkData },
                    { "channel", (int)Server.CHANNELS.CHALK },
                }
            );
        }

        /// <summary>
        /// Broadcast the current chalk canvas
        /// </summary>
        /// <param name="omitEmptyCells">Whether to omit empty cells</param>
        /// <param name="recipient">Optional recipient, defaults to all players</param>
        public void emitChalk(bool omitEmptyCells = false, CSteamID? recipient = null)
        {
            Dictionary<int, object> chalkData = getChalkPacket();

            if (omitEmptyCells)
            {
                Dictionary<int, object> allChalk = chalkData;
                List<Dictionary<int, object>> nonEmptyChalk = new();
                foreach (var idx in allChalk.Keys)
                {
                    var cell = (Dictionary<int, object>)allChalk[idx];
                    bool isEmpty = (long)cell[1] == (long)Chalk.COLOR.NONE;
                    if (!isEmpty)
                    {
                        // Removing empty cells from allChalk would break indexing
                        // of the 'pseudoarray' so we have to create a new list
                        // TODO: Helper function to recreate non-ordered Dictionary
                        nonEmptyChalk.Add(cell);
                    }
                }
                chalkData = new Dictionary<int, object>();
                // Convert List to Dictionary for transmission
                // TODO: Helper to convert List to pseudoarray Dictionary
                for (int i = 0; i < nonEmptyChalk.Count; i++)
                {
                    chalkData[i] = nonEmptyChalk[i];
                }
            }

            var packet = new Dictionary<string, object>
            {
                { "type", "chalk_packet" },
                { "canvas_id", canvasID },
                { "data", chalkData },
                { "channel", (int)Server.CHANNELS.CHALK },
            };

            if (recipient?.GetType() == typeof(CSteamID))
            {
                server.sendPacketToPlayer(packet, (CSteamID)recipient);
            }
            else
            {
                server.sendPacketToPlayers(packet);
            }
        }
    }
}
