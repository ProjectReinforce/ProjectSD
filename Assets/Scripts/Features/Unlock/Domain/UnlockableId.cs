using System;

namespace SwDreams.Features.Unlock.Domain
{
    /// <summary>
    /// 언락 가능 항목의 식별자 VO. (Type, Id) 페어.
    /// OnNewUnlocks 이벤트 페이로드 / UnlockSetSync 키 등에서 사용.
    /// </summary>
    [Serializable]
    public struct UnlockableId : IEquatable<UnlockableId>
    {
        public UnlockableType type;
        public int id;

        public UnlockableId(UnlockableType type, int id)
        {
            this.type = type;
            this.id = id;
        }

        public bool Equals(UnlockableId other) => type == other.type && id == other.id;
        public override bool Equals(object obj) => obj is UnlockableId other && Equals(other);
        public override int GetHashCode() => ((int)type * 397) ^ id;
        public override string ToString() => $"{type}:{id}";

        public static bool operator ==(UnlockableId a, UnlockableId b) => a.Equals(b);
        public static bool operator !=(UnlockableId a, UnlockableId b) => !a.Equals(b);
    }
}
