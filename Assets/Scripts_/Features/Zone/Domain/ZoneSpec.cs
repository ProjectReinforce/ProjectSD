using Shared.Kernel;

namespace Features.Zone.Domain
{
    public sealed class ZoneSpec : ValueObject
    {
        public ZoneSpec(float radius, float duration, ZoneAnchorType anchorType, ZoneHitType hitType)
        {
            Radius = radius;
            Duration = duration;
            AnchorType = anchorType;
            HitType = hitType;
        }

        public float Radius { get; }
        public float Duration { get; }
        public ZoneAnchorType AnchorType { get; }
        public ZoneHitType HitType { get; }

        public override bool Equals(object obj)
        {
            if (obj is not ZoneSpec other) return false;
            return Radius == other.Radius
                && Duration == other.Duration
                && AnchorType == other.AnchorType
                && HitType == other.HitType;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Radius.GetHashCode();
                hash = hash * 31 + Duration.GetHashCode();
                hash = hash * 31 + AnchorType.GetHashCode();
                hash = hash * 31 + HitType.GetHashCode();
                return hash;
            }
        }
    }
}
