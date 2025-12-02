namespace Cove.GodotFormat
{
    public class Vector3
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

        // Zero vector
        public static Vector3 zero = new Vector3(0, 0, 0);

        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Vector3 operator *(Vector3 a, float scalar)
        {
            return new Vector3(a.x * scalar, a.y * scalar, a.z * scalar);
        }

        public static Vector3 operator /(Vector3 a, float scalar)
        {
            if (scalar == 0)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return new Vector3(a.x / scalar, a.y / scalar, a.z / scalar);
        }

        public static float Dot(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }

        public float Magnitude()
        {
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }

        public Vector3 Normalized()
        {
            float magnitude = Magnitude();
            if (magnitude == 0)
            {
                throw new InvalidOperationException("Cannot normalize a zero vector.");
            }
            return this / magnitude;
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }
    }

    public class Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero = new Vector2(0, 0);

        public float Magnitude()
        {
            return (float)Math.Sqrt(x * x + y * y);
        }

        public Vector2 Normalized()
        {
            float magnitude = Magnitude();
            if (magnitude == 0)
            {
                throw new InvalidOperationException("Cannot normalize a zero vector.");
            }
            return new Vector2(x / magnitude, y / magnitude);
        }

        public float Angle()
        {
            return (float)Math.Atan2(y, x); // Returns the angle in radians between -π and π
        }

        public float AngleInDegrees()
        {
            return Angle() * (180f / (float)Math.PI); // Converts the angle from radians to degrees
        }

        public Vector2 Rotate(float angle)
        {
            float cosTheta = (float)Math.Cos(angle);
            float sinTheta = (float)Math.Sin(angle);

            float newX = x * cosTheta - y * sinTheta;
            float newY = x * sinTheta + y * cosTheta;

            return new Vector2(newX, newY);
        }

        public Vector2 RotateInDegrees(float angleDegrees)
        {
            float angleRadians = angleDegrees * ((float)Math.PI / 180f); // Convert degrees to radians
            return Rotate(angleRadians);
        }

        public static Vector2 operator *(Vector2 a, float scalar)
        {
            return new Vector2(a.x * scalar, a.y * scalar);
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is Vector2 other)
            {
                return x == other.x && y == other.y;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }
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
