using System.Collections.Generic;
using SwDreams.Shared.Managers;
using UnityEngine;
using Photon.Pun;
using SwDreams.Data;
using SwDreams.Shared.Data;
using SwDreams.Domain.Interfaces;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Adapter.Entity;
using SwDreams.Adapter.Entity.BossChaos;
using SwDreams.Adapter.Skill;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 보스에게 혼돈 스킬을 적용하는 중재자.
    ///
    /// 플로우:
    /// 1. Lv15 선택 완료 후 호스트가 DetermineBossChaosSkill() 호출
    /// 2. 전체 플레이어의 ChaosSkillManager.ActiveEffects 수집
    /// 3. 6종 중 미선택 혼돈 스킬 찾기 → 랜덤 1개 선정
    /// 4. 보스 스폰 시 ApplyToBoss() 호출
    ///
    /// 셋업: BossSpawner와 같은 오브젝트에 부착하거나 별도 오브젝트.
    /// </summary>
    public class BossChaosApplicator : MonoBehaviourPun
    {
        public static BossChaosApplicator Instance { get; private set; }

        // 보스에게 부여된 혼돈 스킬
        private ChaosEffectType bossChaosType = ChaosEffectType.None;
        private IBossChaosEffect activeBossEffect;

        /// <summary>보스에게 부여된 혼돈 스킬 타입. UI 표시용.</summary>
        public ChaosEffectType BossChaosType => bossChaosType;

        /// <summary>보스 혼돈 스킬 결정 시 발생. UI에서 구독.</summary>
        public event System.Action<ChaosEffectType> OnBossChaosSkillDetermined;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===== Lv15 미선택 → 보스 혼돈 스킬 결정 =====

        /// <summary>
        /// Lv15 선택 완료 후 호스트에서 호출.
        /// 전체 플레이어의 혼돈 스킬 수집 → 미선택 1개를 보스에게 부여.
        /// </summary>
        public void DetermineBossChaosSkill()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 1. 전체 플레이어의 보유 혼돈 스킬 수집
            HashSet<ChaosEffectType> selectedTypes = new HashSet<ChaosEffectType>();
            var players = GameObject.FindGameObjectsWithTag("Player");

            foreach (var player in players)
            {
                var chaosManager = player.GetComponentInChildren<ChaosSkillManager>();
                if (chaosManager == null) continue;

                foreach (var effect in chaosManager.ActiveEffects)
                {
                    selectedTypes.Add(effect);
                }
            }

            // 2. 전체 6종 중 미선택 찾기
            List<ChaosEffectType> unselected = new List<ChaosEffectType>();
            var allTypes = new ChaosEffectType[]
            {
                ChaosEffectType.GlassCannon,
                ChaosEffectType.ChainExplosion,
                ChaosEffectType.BerserkMode,
                ChaosEffectType.AccelEngine,
                ChaosEffectType.Unity,
                ChaosEffectType.Gambler
            };

            foreach (var type in allTypes)
            {
                if (!selectedTypes.Contains(type))
                    unselected.Add(type);
            }

            // 3. 미선택 중 랜덤 1개 선정
            if (unselected.Count > 0)
            {
                ChaosEffectType chosen = unselected[Random.Range(0, unselected.Count)];
                Debug.Log($"[BossChaosApplicator] 보스 혼돈 스킬 결정: {chosen} " +
                          $"(미선택 {unselected.Count}종 중)");

                // 모든 클라이언트에 동기화
                photonView.RPC(nameof(RPC_SetBossChaosSkill), RpcTarget.All, (int)chosen);
            }
            else
            {
                Debug.Log("[BossChaosApplicator] 모든 혼돈 스킬이 선택됨 → 보스 혼돈 없음");
            }
        }

        [PunRPC]
        private void RPC_SetBossChaosSkill(int chaosTypeInt)
        {
            bossChaosType = (ChaosEffectType)chaosTypeInt;
            OnBossChaosSkillDetermined?.Invoke(bossChaosType);
            Debug.Log($"[BossChaosApplicator] 보스 혼돈 스킬 설정: {bossChaosType}");
        }

        // ===== 보스 스폰 시 효과 적용 =====

        /// <summary>
        /// 보스 스폰 후 호출. 저장된 혼돈 스킬 효과를 보스에 적용.
        /// </summary>
        public void ApplyToBoss(Boss boss)
        {
            if (bossChaosType == ChaosEffectType.None)
            {
                Debug.Log("[BossChaosApplicator] 보스 혼돈 스킬 없음");
                return;
            }

            activeBossEffect = CreateEffect(bossChaosType);
            if (activeBossEffect != null)
            {
                activeBossEffect.ApplyToBoss(boss);
                Debug.Log($"[BossChaosApplicator] 보스에 {bossChaosType} 효과 적용");
            }
        }

        /// <summary>
        /// BossPhaseManager.Update()에서 매 프레임 호출.
        /// </summary>
        public void UpdateBossEffect(float deltaTime)
        {
            activeBossEffect?.OnBossUpdate(deltaTime);
        }

        /// <summary>
        /// 보스전 종료 시 호출.
        /// </summary>
        public void CleanupBossEffect()
        {
            activeBossEffect?.Cleanup();
            activeBossEffect = null;
        }

        /// <summary>
        /// 연쇄 폭발 효과 전용: 플레이어 사망 시 호출.
        /// </summary>
        public void OnPlayerDied(Vector2 deathPosition)
        {
            if (activeBossEffect is BossChainExplosionEffect chain)
            {
                chain.OnPlayerDied(deathPosition);
            }
        }

        /// <summary>
        /// 가속 엔진/단결의 데미지 배율. BossPhaseManager에서 참조.
        /// </summary>
        public float GetDamageMultiplier()
        {
            float mul = 1f;

            if (activeBossEffect is BossAccelEngineEffect accel)
                mul *= accel.DamageMultiplier;
            if (activeBossEffect is BossUnityEffect unity)
                mul *= unity.DamageMultiplier;

            return mul;
        }

        // ===== Factory =====

        private IBossChaosEffect CreateEffect(ChaosEffectType type)
        {
            switch (type)
            {
                case ChaosEffectType.GlassCannon:    return new BossGlassCannonEffect();
                case ChaosEffectType.ChainExplosion:  return new BossChainExplosionEffect();
                case ChaosEffectType.BerserkMode:     return new BossBerserkEffect();
                case ChaosEffectType.AccelEngine:     return new BossAccelEngineEffect();
                case ChaosEffectType.Unity:           return new BossUnityEffect();
                case ChaosEffectType.Gambler:         return new BossGamblerEffect();
                default: return null;
            }
        }
    }
}