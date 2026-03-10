using System.Collections.Generic;
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

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Radius;
            yield return Duration;
            yield return AnchorType;
            yield return HitType;
        }
    }
}
