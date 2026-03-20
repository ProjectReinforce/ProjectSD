using UnityEngine;

namespace Shared.Math
{
    public static class Float2Extensions
    {
        public static Vector2 ToVector2(this Float2 f) => new Vector2(f.X, f.Y);
        public static Float2 ToFloat2(this Vector2 v) => new Float2(v.x, v.y);
    }
}
