using System;

namespace Shared.Math
{
    public readonly struct Float3 : IEquatable<Float3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 Zero => new Float3(0f, 0f, 0f);

        public static Float3 operator +(Float3 a, Float3 b)
            => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Float3 operator -(Float3 a, Float3 b)
            => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Float3 operator *(Float3 a, float s)
            => new Float3(a.X * s, a.Y * s, a.Z * s);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;

        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        public Float3 Normalized
        {
            get
            {
                var mag = Magnitude;
                if (mag < 1e-6f) return Zero;
                return new Float3(X / mag, Y / mag, Z / mag);
            }
        }

        public static float Dot(Float3 a, Float3 b)
            => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Float3 Lerp(Float3 a, Float3 b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return new Float3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }

        public static Float3 Reflect(Float3 direction, Float3 normal)
        {
            var dot = Dot(direction, normal);
            return direction - normal * (2f * dot);
        }

        public bool Equals(Float3 other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is Float3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Z.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Float3 a, Float3 b) => a.Equals(b);
        public static bool operator !=(Float3 a, Float3 b) => !a.Equals(b);
    }
}
