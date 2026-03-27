using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Skill.TriggerEffects;
using SwDreams.Domain.ValueObjects;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 장판(지대) 스킬 효과.
    ///
    /// [Step 4-4] Executor 패턴 적용.
    /// - Execute()가 SkillExecutor를 풀에서 꺼내 Begin() 호출
    /// - 실제 스폰은 AreaSpawner가 담당
    /// - activeZones 관리도 AreaSpawner로 이전
    ///
    /// 사용 스킬:
    /// - 번개 (DelayedBurst, spawnAtRandomPosition=true)
    /// - 개미지옥 (DelayedBurst, spawnAtRandomPosition=true)
    /// - 성역 (Single, 플레이어 위치)
    ///
    /// 네트워크: 장판은 로컬 비주얼, 데미지/회복 판정은 호스트만.
    /// </summary>
    public class AreaEffect : SkillEffect
    {
        // Executor 풀링용 프리팹 (SkillEffectFactory에서 주입)
        private GameObject executorPrefab;

        // 스폰 담당 (SkillEffectFactory에서 주입)
        private ISkillSpawner spawner;

        // 캐시
        private Transform playerTransform;
        private PlayerStats playerStats;
        private SkillTriggerSystem triggerSystem;

        /// <summary>
        /// SkillEffectFactory에서 생성 직후 호출.
        /// </summary>
        public void Initialize(GameObject executorPrefab, ISkillSpawner spawner)
        {
            this.executorPrefab = executorPrefab;
            this.spawner = spawner;

            CachePlayerReferences();
        }

        private void CachePlayerReferences()
        {
            if (playerTransform == null)
            {
                playerTransform = transform.root;
                if (playerTransform != null)
                    playerStats = playerTransform.GetComponent<PlayerStats>();
            }
            if (triggerSystem == null)
                triggerSystem = GetComponent<SkillTriggerSystem>();
        }

        public override void Execute(Skill skill)
        {
            CachePlayerReferences();
            if (executorPrefab == null || spawner == null || playerTransform == null) return;

            // Executor를 풀에서 꺼내서 시작
            GameObject executorObj = PoolManager.Instance.Get(executorPrefab);
            var executor = executorObj.GetComponent<SkillExecutor>();

            if (executor == null)
            {
                Debug.LogError("[AreaEffect] SkillExecutor 컴포넌트 없음");
                PoolManager.Instance.Return(executorObj);
                return;
            }

            executor.Begin(skill, spawner, playerStats, playerTransform, triggerSystem);
        }
    }
}