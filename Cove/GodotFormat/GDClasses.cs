using System.Numerics;

namespace Cove.GodotFormat
{
    // Proxy classes for backward compatibility with plugins that reply on these types and not the System.Numerics ones
    [Obsolete("Use System.Numerics.Vector2 instead")]
    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float Length() => new System.Numerics.Vector2(x, y).Length();

        public override string ToString() => $"({x}, {y})";

        public static implicit operator System.Numerics.Vector2(Vector2 v) => new System.Numerics.Vector2(v.x, v.y);
        public static implicit operator Vector2(System.Numerics.Vector2 v) => new Vector2(v.X, v.Y);

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 v, float s) => new Vector2(v.x * s, v.y * s);
        public static Vector2 operator /(Vector2 v, float s) => new Vector2(v.x / s, v.y / s);
    }
    
    [Obsolete("Use System.Numerics.Vector3 instead")]
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float Length() => new System.Numerics.Vector3(x, y, z).Length();

        public override string ToString() => $"({x}, {y}, {z})";

        public static implicit operator System.Numerics.Vector3(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        public static implicit operator Vector3(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 v, float s) => new Vector3(v.x * s, v.y * s, v.z * s);
        public static Vector3 operator /(Vector3 v, float s) => new Vector3(v.x / s, v.y / s, v.z / s);
    }

    public class Quat
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quat(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    public class Plane
    {
        public float x;
        public float y;
        public float z;
        public float distance;

        public Plane(float x, float y, float z, float distance)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.distance = distance;
        }
    }

    public class ReadError
    {
        public ReadError() { }
    }
}
