using UnityEngine;
using Photon.Pun;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;
using SwDreams.Adapter.Entity;
using SwDreams.Data;
using SwDreams.Shared.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 디버프 스폰 담당. ISkillSpawner 구현.
    ///
    /// 기존 DebuffEffect의 적 탐색 + DebuffMark 부착 로직 이전.
    /// 호스트에서만 대상 선정 + 디버프 적용.
    ///
    /// 스킬: 저주인형(Single), 역병 인형(진화) 등
    ///
    /// [Phase 7 리팩토링] Step 4-5
    /// </summary>
    public class DebuffSpawner : ISkillSpawner
    {
        private readonly GameObject markerPrefab;
        private readonly int spreadOnDeathCount; // 역병 인형 진화용

        public DebuffSpawner(GameObject markerPrefab, int spreadOnDeathCount)
        {
            this.markerPrefab = markerPrefab;
            this.spreadOnDeathCount = spreadOnDeathCount;
        }

        public void Prewarm(SkillData data)
        {
            if (markerPrefab != null)
                PoolManager.Instance?.Prewarm(markerPrefab, data.targetCount * 2);
        }

        public void Cleanup()
        {
            // DebuffMark는 적에 부착되어 자체 소멸
        }

        public void Spawn(SpawnContext ctx)
        {
            // 호스트에서만 대상 선정 + 디버프 적용
            if (!PhotonNetwork.IsMasterClient) return;

            SkillData data = ctx.skillData;
            int count = data.targetCount;

            // ── 스탯 계산 (SpawnContext에서 필터링 완료된 값 사용) ──
            float duration = data.debuffDuration + ctx.skillDurationBonus;

            // 디버프 강도: 기본 배율 + 공격력 스케일링
            // rawDamage 기반으로 공격력 반영분 계산
            float amplify = data.damageAmplify;
            if (ctx.rawDamage > 0 && ctx.damage > ctx.rawDamage)
            {
                // 공격력 배율이 높으면 디버프 강도도 약간 증가
                float atkRatio = (float)ctx.damage / ctx.rawDamage;
                amplify += (atkRatio - 1f) * 0.1f;
            }

            // 랜덤 적 선택
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies.Length == 0) return;

            ShuffleArray(enemies);

            int applied = 0;
            for (int i = 0; i < enemies.Length && applied < count; i++)
            {
                if (!enemies[i].activeInHierarchy) continue;

                var enemy = enemies[i].GetComponent<Enemy>();
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