using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Skill.TriggerEffects;
using SwDreams.Domain.ValueObjects;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 투사체 기반 스킬 효과.
    ///
    /// [Step 4-3] Executor 패턴 적용.
    /// - Execute()가 SkillExecutor를 풀에서 꺼내 Begin() 호출
    /// - 실제 스폰은 ProjectileSpawner가 담당
    /// - 방향 계산, 스탯 적용은 Executor가 SpawnContext로 통합 처리
    ///
    /// 투사체는 로컬 전용 (네트워크 동기화 없음).
    /// </summary>
    public class ProjectileEffect : SkillEffect
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
                Debug.LogError("[ProjectileEffect] SkillExecutor 컴포넌트 없음");
                PoolManager.Instance.Return(executorObj);
                return;
            }

            executor.Begin(skill, spawner, playerStats, playerTransform, triggerSystem);

            // OnFire 트리거
            if (triggerSystem != null && triggerSystem.HasTrigger(TriggerType.OnFire))
            {
                triggerSystem.FireTrigger(TriggerType.OnFire, new TriggerContext
                {
                    position = playerTransform.position,
                    direction = Vector2.right, // Executor 내부에서 실제 방향 계산
                    owner = playerTransform
                });
            }
        }

        // ===== 레거시 호환 (SetProjectilePrefab) =====
        // SkillEffectFactory를 거치지 않는 기존 코드가 있을 경우 대비.
        // TODO: [정리] 모든 경로가 Initialize()를 거치면 제거

        /// <summary>
        /// [레거시] SkillManager에서 동적 생성 시 프리팹 설정용.
        /// Initialize()로 대체 예정.
        /// </summary>
        public void SetProjectilePrefab(GameObject prefab)
        {
            if (spawner == null && prefab != null)
            {
                spawner = new ProjectileSpawner(prefab);
                spawner.Prewarm(null);
            }
        }
    }
}