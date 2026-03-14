using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Entity.BossChaos
{
    // ===================================================================
    // 보스 혼돈 스킬 효과 6종.
    // 각각 IBossChaosEffect 구현.
    //
    // BossChaosApplicator가 ChaosEffectType → 구현체 매핑.
    // 보스 스폰 시 ApplyToBoss() 호출, 보스전 중 OnBossUpdate() 매 프레임.
    // ===================================================================

    /// <summary>
    /// 유리대포: 보스 HP 50% 감소, 모든 공격 데미지 2배.
    /// </summary>
    public class BossGlassCannonEffect : IBossChaosEffect
    {
        private Boss boss;

        public void ApplyToBoss(Boss boss)
        {
            this.boss = boss;
            boss.ApplyHPMultiplier(0.5f);
            // 공격 데미지 2배는 BossPhaseManager의 공격 패턴에서 처리
            // → BossPhaseManager.SetDamageMultiplier(2f)
            Debug.Log("[BossChaos] 유리대포 — HP 50%, 공격 2배");
        }

        public void OnBossUpdate(float deltaTime) { }
        public void Cleanup() { }
    }

    /// <summary>
    /// 연쇄 폭발: 플레이어 사망 위치에서 3초 후 폭발.
    /// </summary>
    public class BossChainExplosionEffect : IBossChaosEffect
    {
        private Boss boss;
        private float explosionDelay = 3f;
        private int explosionDamage = 40;
        private float explosionRadius = 5f;

        public void ApplyToBoss(Boss boss)
        {
            this.boss = boss;
            // 플레이어 사망 이벤트는 RespawnManager 경유
            // → BossChaosApplicator가 PlayerStub.OnDied 구독 처리
            Debug.Log("[BossChaos] 연쇄 폭발 — 플레이어 사망 위치 폭발");
        }

        public void OnPlayerDied(Vector2 deathPosition)
        {
            // BossPhaseManager에 지연 폭발 등록 (기존 인프라 재사용)
            Manager.BossPhaseManager.Instance?.RegisterDelayedExplosion(
                deathPosition, explosionDelay, explosionDamage, explosionRadius, null);
        }

        public void OnBossUpdate(float deltaTime) { }
        public void Cleanup() { }
    }

    /// <summary>
    /// 폭주 모드: 보스 HP 30% 이하 시 쿨타임 50% 감소, 속도 4.0.
    /// </summary>
    public class BossBerserkEffect : IBossChaosEffect
    {
        private Boss boss;
        private bool isBerserkActive = false;

        public void ApplyToBoss(Boss boss)
        {
            this.boss = boss;
            Debug.Log("[BossChaos] 폭주 모드 — Phase 3에서 극한 강화");
        }

        public void OnBossUpdate(float deltaTime)
        {
            if (boss == null || !boss.IsAlive) return;

            bool shouldBerserk = boss.CurrentPhase == Domain.BossPhase.Phase3;

            if (shouldBerserk && !isBerserkActive)
            {
                isBerserkActive = true;
                boss.SetMoveSpeed(4.0f);
                // 쿨타임 감소는 BossPhaseManager.SetCooldownMultiplier(0.5f)
                Manager.BossPhaseManager.Instance?.SetCooldownMultiplier(0.5f);
                Debug.Log("[BossChaos] 폭주 발동! 속도 4.0, 쿨타임 -50%");
            }
        }

        public void Cleanup() { isBerserkActive = false; }
    }

    /// <summary>
    /// 가속 엔진: 보스전 시작 후 매 1분마다 공격력 +25%, 이동속도 +0.3.
    /// </summary>
    public class BossAccelEngineEffect : IBossChaosEffect
    {
        private Boss boss;
        private float elapsedTime;
        private int lastMinute = 0;
        private float baseMoveSpeed;
        private float damageMultiplier = 1f;

        public float DamageMultiplier => damageMultiplier;

        public void ApplyToBoss(Boss boss)
        {
            this.boss = boss;
            baseMoveSpeed = boss.Data.moveSpeed;
            elapsedTime = 0f;
            lastMinute = 0;
            Debug.Log("[BossChaos] 가속 엔진 — 매 1분 공격력 +25%, 속도 +0.3");
        }

        public void OnBossUpdate(float deltaTime)
        {
            if (boss == null || !boss.IsAlive) return;

            elapsedTime += deltaTime;
            int currentMinute = Mathf.FloorToInt(elapsedTime / 60f);

            if (currentMinute > lastMinute)
            {
                lastMinute = currentMinute;
                damageMultiplier = 1f + (currentMinute * 0.25f);
                float speedBonus = currentMinute * 0.3f;

                float phaseSpeed = boss.Data.GetMoveSpeedForPhase(boss.CurrentPhase);
                boss.SetMoveSpeed(phaseSpeed + speedBonus);

                Debug.Log($"[BossChaos] 가속 — {currentMinute}분 경과, 공격력 x{damageMultiplier}, 속도+{speedBonus}");
            }
        }

        public void Cleanup() { }
    }

    /// <summary>
    /// 단결: 플레이어들이 5m 이상 흩어지면 보스 데미지 +40%.
    /// </summary>
    public class BossUnityEffect : IBossChaosEffect
    {
        private Boss boss;
        private float checkInterval = 0.5f;
        private float checkTimer;
        private float separationThreshold = 5f;
        private bool isSeparated = false;

        public float DamageMultiplier => isSeparated ? 1.4f : 1f;

        public void ApplyToBoss(Boss boss)
        {
            this.boss = boss;
            Debug.Log("[BossChaos] 단결 — 플레이어 분산 시 데미지 +40%");
        }

        public void OnBossUpdate(float deltaTime)
        {
            checkTimer += deltaTime;
            if (checkTimer < checkInterval) return;
            checkTimer = 0f;

            // 플레이어 간 최대 거리 체크
            var players = GameObject.FindGameObjectsWithTag("Player");
            float maxDist = 0f;
            int aliveCount = 0;

            for (int i = 0; i < players.Length; i++)
            {
                var di = players[i].GetComponent<IDamageable>();
                if (di == null || !di.IsAlive) continue;
                aliveCount++;

                for (int j = i + 1; j < players.Length; j++)
                {
                    var dj = players[j].GetComponent<IDamageable>();
                    if (dj == null || !dj.IsAlive) continue;

                    float dist = Vector2.Distance(
                        players[i].transform.position,
                        players[j].transform.position);
                    if (dist > maxDist) maxDist = dist;
                }
            }

            bool wasSeparated = isSeparated;
            isSeparated = aliveCount >= 2 && maxDist > separationThreshold;

            if (isSeparated != wasSeparated)
                Debug.Log($"[BossChaos] 단결 — 분산 상태: {isSeparated} (최대 거리: {maxDist:F1}m)");
        }

        public void Cleanup() { isSeparated = false; }
    }

    /// <summary>
    /// 도박꾼: 보스 충격파가 3갈래로 발사.
    /// 이 효과는 BossPhaseManager의 ShockwaveAttack 생성 시 fanCount=3 파라미터로 반영.
    /// </summary>
    public class BossGamblerEffect : IBossChaosEffect
    {
        public void ApplyToBoss(Boss boss)
        {
            // ShockwaveAttack의 fanCount를 3으로 변경
            // → BossPhaseManager.SetShockwaveFanCount(3)
            Manager.BossPhaseManager.Instance?.SetShockwaveFanCount(3);
            Debug.Log("[BossChaos] 도박꾼 — 충격파 3갈래");
        }

        public void OnBossUpdate(float deltaTime) { }
        public void Cleanup() { }
    }
}