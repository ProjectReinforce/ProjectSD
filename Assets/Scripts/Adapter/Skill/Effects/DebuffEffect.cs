using UnityEngine;
using Photon.Pun;
using SwDreams.Adapter.Manager;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 디버프 스킬 효과.
    /// Execute() 호출 시 랜덤 적에게 저주 마커를 부여.
    ///
    /// 사용 스킬:
    /// - 저주인형: 랜덤 적에게 받는 피해 증가 디버프
    ///
    /// SkillData 참조 필드:
    /// - targetCount: 동시 디버프 대상 수
    /// - debuffDuration: 디버프 유지 시간 (+ PlayerStats.SkillDurationBonus)
    /// - damageAmplify: 받는 피해 증가 배율 (× PlayerStats.AttackMultiplier로 추가 스케일)
    /// - effectPrefab: 저주 마커 비주얼 프리팹 (선택, 없어도 동작)
    ///
    /// 네트워크:
    /// - 호스트가 대상 선정 + DebuffMark 부착
    /// - 비주얼은 모든 클라이언트에서 표시 (적 오브젝트 자식으로)
    ///
    /// DebuffMark 연동:
    /// - 적에게 DebuffMark 컴포넌트를 동적 추가
    /// - Enemy.TakeDamage() 시 DebuffMark.DamageAmplify 참조하여 추가 피해 적용
    /// - 이를 위해 Enemy.TakeDamage() 확장 필요 (주석으로 표시)
    /// </summary>
    public class DebuffEffect : SkillEffect
    {
        private GameObject markerPrefab;
        private Transform playerTransform;
        private PlayerStats playerStats;
        private int spreadOnDeathCount; // 역병 인형 진화용

        private void Start()
        {
            CachePlayerReferences();
        }

        /// <summary>
        /// SkillEffectFactory에서 생성 직후 호출.
        /// </summary>
        public void Initialize(SkillData data)
        {
            markerPrefab = data.effectPrefab;
            spreadOnDeathCount = data.spreadOnDeathCount;

            CachePlayerReferences();

            if (markerPrefab != null)
                PoolManager.Instance?.Prewarm(markerPrefab, data.targetCount * 2);
        }

        private void CachePlayerReferences()
        {
            if (playerTransform != null) return;
            playerTransform = transform.root;
            if (playerTransform != null)
                playerStats = playerTransform.GetComponent<PlayerStats>();
        }

        public override void Execute(Skill skill)
        {
            CachePlayerReferences();
            if (playerTransform == null) return;

            // 호스트에서만 대상 선정 + 디버프 적용
            // 비주얼은 적 오브젝트 자식으로 생성되므로 자동 동기화
            if (!PhotonNetwork.IsMasterClient) return;

            SkillData data = skill.Data;
            int count = data.targetCount;

            // PlayerStats 보너스 적용
            float duration = data.debuffDuration;
            if (playerStats != null)
                duration += playerStats.SkillDurationBonus;

            float amplify = data.damageAmplify;
            // 공격력 배율이 높으면 디버프 강도도 약간 증가
            if (playerStats != null)
                amplify += (playerStats.AttackMultiplier - 1f) * 0.1f;

            // 랜덤 적 선택
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies.Length == 0) return;

            // 셔플해서 앞에서 count개 선택
            ShuffleArray(enemies);

            int applied = 0;
            for (int i = 0; i < enemies.Length && applied < count; i++)
            {
                if (!enemies[i].activeInHierarchy) continue;

                var enemy = enemies[i].GetComponent<Entity.Enemy>();
                if (enemy == null || !enemy.IsAlive) continue;

                ApplyDebuff(enemies[i], amplify, duration);
                applied++;
            }
        }

        private void ApplyDebuff(GameObject enemyObj, float amplify, float duration)
        {
            // 이미 디버프가 있으면 갱신
            var existing = enemyObj.GetComponent<DebuffMark>();
            if (existing != null)
            {
                existing.Refresh(amplify, duration);
                return;
            }

            // 새 디버프 부착
            var mark = enemyObj.AddComponent<DebuffMark>();
            mark.Initialize(amplify, duration, markerPrefab, spreadOnDeathCount);
        }

        /// <summary>
        /// Fisher–Yates 셔플.
        /// </summary>
        private void ShuffleArray(GameObject[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
