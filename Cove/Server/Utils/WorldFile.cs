using System.Globalization;
using System.Text.RegularExpressions;
using Cove.GodotFormat;

namespace Cove.Server.Utils
{
    internal class WorldFile
    {

        // for reading the point positions from a .tscn file (main_zone.tscn)
        public static List<Vector3> readPoints(string nodeGroup, string file)
        {
            List<Vector3> points = new List<Vector3>();

            // split the file into lines
            string[] lines = file.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {

                Match isFishPoint = Regex.Match(lines[i], @"groups=\[([^\]]*)\]");
                if (isFishPoint.Success && isFishPoint.Groups[1].Value == $"\"{nodeGroup}\"")
                {
                    string transformPattern = @"Transform\(.*?,\s*(-?\d+\.?\d*),\s*(-?\d+\.?\d*),\s*(-?\d+\.?\d*)\s*\)";
                    Match match = Regex.Match(lines[i + 1], transformPattern);

                    string x = match.Groups[1].Value;
                    string y = match.Groups[2].Value;
                    string z = match.Groups[3].Value;

                    Vector3 thisPoint = new Vector3(float.Parse(x, CultureInfo.InvariantCulture), float.Parse(y, CultureInfo.InvariantCulture), float.Parse(z, CultureInfo.InvariantCulture));
                    points.Add(thisPoint);
                }
            }

            return points;
        }

    }
}
