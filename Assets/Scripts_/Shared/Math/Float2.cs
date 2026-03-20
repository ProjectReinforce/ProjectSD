using System;

namespace Shared.Math
{
    public readonly struct Float2 : IEquatable<Float2>
    {
        public readonly float X;
        public readonly float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Float2 Zero => new Float2(0f, 0f);

        public static Float2 operator +(Float2 a, Float2 b)
            => new Float2(a.X + b.X, a.Y + b.Y);

        public static Float2 operator -(Float2 a, Float2 b)
            => new Float2(a.X - b.X, a.Y - b.Y);

        public static Float2 operator *(Float2 a, float s)
            => new Float2(a.X * s, a.Y * s);

        public float SqrMagnitude => X * X + Y * Y;

        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        public Float2 Normalized
        {
            get
            {
                var mag = Magnitude;
                if (mag < 1e-6f) return Zero;
                return new Float2(X / mag, Y / mag);
            }
        }

        public static float Dot(Float2 a, Float2 b)
            => a.X * b.X + a.Y * b.Y;

        public bool Equals(Float2 other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is Float2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Float2 a, Float2 b) => a.Equals(b);
        public static bool operator !=(Float2 a, Float2 b) => !a.Equals(b);
    }
}
