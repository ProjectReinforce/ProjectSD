using UnityEngine;

namespace SwDreams.Features.Skill.Adapter.Chaos.StatWatchers
{
    /// <summary>
    /// 자신 기준 반경 내 같은 태그 오브젝트 수 추적. Unity 혼돈 — "근접 아군 수" 용.
    ///
    /// 매 프레임이 아닌 interval 단위 폴링 (FindGameObjectsWithTag 비용 보정).
    /// 자기 자신은 카운트 제외.
    /// </summary>
    public class NearbyCountWatcher : StatWatcher
    {
        private readonly Transform self;
        private readonly string targetTag;
        private readonly System.Func<float> radiusProvider;
        private readonly float pollInterval;

        private float pollTimer;
        private int cachedCount;

        public NearbyCountWatcher(
            Transform self,
            string targetTag,
            System.Func<float> radiusProvider,
            float pollInterval = 0.5f)
        {
            this.self = self;
            this.targetTag = targetTag;
            this.radiusProvider = radiusProvider;
            this.pollInterval = Mathf.Max(0.05f, pollInterval);
        }

        /// <summary>최근 폴링 시점의 근접 객체 수. 자기 자신 제외.</summary>
        public int Count => cachedCount;

        public override bool Tick()
        {
            pollTimer += Time.deltaTime;
            if (pollTimer < pollInterval) return false;
            pollTimer = 0f;

            if (self == null) return false;

            float radius = radiusProvider != null ? radiusProvider() : 5f;
            int count = CountNearby(radius);

            if (count != cachedCount)
            {
                cachedCount = count;
                return true;
            }
            return false;
        }

        private int CountNearby(float radius)
        {
            var others = GameObject.FindGameObjectsWithTag(targetTag);
            int count = 0;
            Vector3 origin = self.position;
            for (int i = 0; i < others.Length; i++)
            {
                var go = others[i];
                if (go == null) continue;
                if (go == self.gameObject) continue;
                if (!go.activeInHierarchy) continue;

                float dist = Vector2.Distance(origin, go.transform.position);
                if (dist <= radius) count++;
            }
            return count;
        }
    }
}
