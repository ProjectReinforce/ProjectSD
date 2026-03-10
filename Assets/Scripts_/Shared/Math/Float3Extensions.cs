using UnityEngine;

namespace Shared.Math
{
    public static class Float3Extensions
    {
        public static Vector3 ToVector3(this Float3 f) => new Vector3(f.X, f.Y, f.Z);
        public static Float3 ToFloat3(this Vector3 v) => new Float3(v.x, v.y, v.z);
    }
}
