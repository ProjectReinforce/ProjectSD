using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;

namespace SwDreams.Features.Skill.Adapter.Spread
{
    /// <summary>
    /// 투사체 배치 패턴 인터페이스.
    /// 기준 방향 + 발사 수 → 각 투사체의 방향 배열 반환.
    /// </summary>
    public interface ISpreadPattern
    {
        Vector2[] GetDirections(Vector2 baseDirection, int count);
    }

    /// <summary>
    /// SpreadPatternType → ISpreadPattern 생성.
    /// </summary>
    public static class SpreadPatternFactory
    {
        public static ISpreadPattern Create(SpreadPatternType type, float spreadAngle = 15f)
        {
            switch (type)
            {
                case SpreadPatternType.Single:  return new SingleSpread();
                case SpreadPatternType.Fan:     return new FanSpread(spreadAngle);
                case SpreadPatternType.Radial:  return new RadialSpread();
                case SpreadPatternType.Random:  return new RandomSpread();
                default:                        return new FanSpread(spreadAngle);
            }
        }
    }

    /// <summary>단일 방향. count개가 전부 같은 방향으로 발사.</summary>
    public class SingleSpread : ISpreadPattern
    {
        public Vector2[] GetDirections(Vector2 baseDirection, int count)
        {
            var dirs = new Vector2[count];
            for (int i = 0; i < count; i++)
                dirs[i] = baseDirection;
            return dirs;
        }
    }

    /// <summary>부채꼴. 중심 기준 좌우로 균등 배치.</summary>
    public class FanSpread : ISpreadPattern
    {
        private readonly float spreadAngle;

        public FanSpread(float spreadAngle = 15f)
        {
            this.spreadAngle = spreadAngle;
        }

        public Vector2[] GetDirections(Vector2 baseDirection, int count)
        {
            var dirs = new Vector2[count];
            if (count <= 1)
            {
                dirs[0] = baseDirection;
                return dirs;
            }

            float startAngle = -(count - 1) * spreadAngle * 0.5f;
            for (int i = 0; i < count; i++)
                dirs[i] = RotateVector(baseDirection, startAngle + i * spreadAngle);
            return dirs;
        }

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }

    /// <summary>360도 균등 분배. 전방위 공격용.</summary>
    public class RadialSpread : ISpreadPattern
    {
        public Vector2[] GetDirections(Vector2 baseDirection, int count)
        {
            var dirs = new Vector2[count];
            if (count <= 1)
            {
                dirs[0] = baseDirection;
                return dirs;
            }

            float angleStep = 360f / count;
            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + angleStep * i) * Mathf.Deg2Rad;
                dirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return dirs;
        }
    }

    /// <summary>랜덤 방향. 각 투사체가 무작위 방향으로 발사.</summary>
    public class RandomSpread : ISpreadPattern
    {
        public Vector2[] GetDirections(Vector2 baseDirection, int count)
        {
            var dirs = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                dirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return dirs;
        }
    }
}
