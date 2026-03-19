using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Data;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 혼돈 스킬 효과 관리. 각 플레이어에 부착.
    /// SkillManager.ApplyChoice()에서 Chaos 타입이면 여기로 위임.
    ///
    /// 혼돈 스킬은 슬롯을 차지하지 않으며, 글로벌 규칙을 변경.
    /// 다른 시스템이 읽어가는 modifier 프로퍼티를 제공.
    ///
    /// 셋업: PlayerStub(또는 Player) 자식에 SkillManager와 함께 부착.
    /// </summary>
    public class ChaosSkillManager : MonoBehaviour
    {
        // ===== 활성 혼돈 스킬 플래그 =====
        private bool hasGlassCannon;
        private bool hasChainExplosion;
        private bool hasBerserkMode;
        private bool hasAccelEngine;
        private bool hasUnity;
        private bool hasGambler;

        // ===== 연쇄 폭발 설정 =====
        [Header("연쇄 폭발")]
        [SerializeField] private float explosionRadius = 2f;
        [SerializeField] private int explosionDamage = 20;
        [SerializeField] private int maxChainPerFrame = 5;
        [SerializeField] private GameObject explosionEffectPrefab;
        private int chainCountThisFrame;

        // ===== 단결 설정 =====
        [Header("단결")]
        [SerializeField] private float unityCheckRadius = 5f;
        private float unityCheckTimer;
        private const float UNITY_CHECK_INTERVAL = 0.5f;
        private int nearbyPlayerCount;

        // ===== Modifier 프로퍼티 (외부 시스템에서 읽기) =====

        /// <summary>
        /// 공격력 배율. PlayerStats에서 최종 데미지에 곱함.
        /// 유리대포(2배) + 가속엔진 + 단결 합산.
        /// </summary>
        public float ChaosAttackMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 쿨다운 배율. Skill.Fire()에서 CDR 이후 추가 적용.
        /// 폭주모드(0.5배) 등.
        /// </summary>
        public float ChaosCooldownMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 이동속도 추가. PlayerStats.MoveSpeed에 가산.
        /// 폭주모드(baseMoveSpeed * 0.5) 등.
        /// </summary>
        public float ChaosMoveSpeedBonus { get; private set; } = 0f;

        /// <summary>
        /// 최대 HP 배율. 1.0 = 변동 없음, 0.5 = 절반.
        /// PlayerStats에서 MaxHP에 곱함.
        /// </summary>
        public float ChaosMaxHPMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 도박꾼 활성 여부. SkillManager.GenerateChoices()에서 참조.
        /// </summary>
        public bool IsGambler => hasGambler;

        /// <summary>
        /// 보유 중인 혼돈 스킬 목록 (디버그 오버레이, 보스 시스템용).
        /// </summary>
        private List<ChaosEffectType> activeChaosEffects = new List<ChaosEffectType>();
        public IReadOnlyList<ChaosEffectType> ActiveEffects => activeChaosEffects;

        // ===== 캐시 =====
        private IDamageable playerDamageable;
        private PlayerStats playerStats;
        private float baseMoveSpeed;

        private void Start()
        {
            playerDamageable = GetComponentInParent<IDamageable>();
            playerStats = GetComponentInParent<PlayerStats>();
            if (playerStats != null)
                baseMoveSpeed = playerStats.MoveSpeed;
        }

        // ===== 혼돈 스킬 적용 =====

        /// <summary>
        /// SkillManager.ApplyChoice()에서 호출.
        /// </summary>
        public void ApplyChaos(SkillData data)
        {
            // 참조 캐싱 (Start보다 먼저 호출될 수 있으므로)
            if (playerDamageable == null)
                playerDamageable = GetComponentInParent<IDamageable>();
            if (playerStats == null)
            {
                playerStats = GetComponentInParent<PlayerStats>();
                if (playerStats != null)
                    baseMoveSpeed = playerStats.MoveSpeed;
            }

            if (data.chaosEffectType == ChaosEffectType.None)
            {
                Debug.LogWarning($"[ChaosSkillManager] {data.skillName}: chaosEffectType이 None");
                return;
            }

            // 중복 방지
            if (activeChaosEffects.Contains(data.chaosEffectType))
            {
                Debug.Log($"[ChaosSkillManager] {data.skillName} 이미 보유");
                return;
            }

            activeChaosEffects.Add(data.chaosEffectType);

            switch (data.chaosEffectType)
            {
                case ChaosEffectType.GlassCannon:
                    ApplyGlassCannon();
                    break;
                case ChaosEffectType.ChainExplosion:
                    ApplyChainExplosion();
                    break;
                case ChaosEffectType.BerserkMode:
                    ApplyBerserkMode();
                    break;
                case ChaosEffectType.AccelEngine:
                    ApplyAccelEngine();
                    break;
                case ChaosEffectType.Unity:
                    ApplyUnity();
                    break;
                case ChaosEffectType.Gambler:
                    ApplyGambler();
                    break;
            }

            Debug.Log($"[ChaosSkillManager] 혼돈 스킬 적용: {data.skillName} ({data.chaosEffectType})");
        }

        // ===== 개별 적용 =====

        private void ApplyGlassCannon()
        {
            hasGlassCannon = true;
            ChaosMaxHPMultiplier = 0.5f;
            RecalculateModifiers();

            // HP 감소는 소유자 클라이언트에서만 실행
            // RPC_SyncSkillAcquisition으로 모든 클라이언트에서 호출되므로,
            // 소유자만 TakeDamage → RPC_TakeDamage(All)로 HP 동기화
            var pv = GetComponentInParent<PhotonView>();
            if (pv != null && !pv.IsMine) return;

            if (playerDamageable != null && playerDamageable.IsAlive)
            {
                int halfHP = playerDamageable.CurrentHP / 2;
                int dmg = playerDamageable.CurrentHP - Mathf.Max(1, halfHP);
                if (dmg > 0)
                    playerDamageable.TakeDamage(dmg);

                Debug.Log($"[ChaosSkillManager] 유리대포 — HP → {playerDamageable.CurrentHP}");
            }
        }

        private void ApplyChainExplosion()
        {
            hasChainExplosion = true;
            // 연쇄 폭발은 Update/LateUpdate에서 프레임별 카운트 리셋
        }

        private void ApplyBerserkMode()
        {
            hasBerserkMode = true;
            // 조건부 — Update에서 HP 체크
        }

        private void ApplyAccelEngine()
        {
            hasAccelEngine = true;
            // 시간 기반 — Update에서 GameTime 체크
        }

        private void ApplyUnity()
        {
            hasUnity = true;
            nearbyPlayerCount = 0;
        }

        private void ApplyGambler()
        {
            hasGambler = true;
            Debug.Log("[ChaosSkillManager] 도박꾼 활성 — 다음 레벨업부터 선택지 1개 등급 상승");
        }

        // ===== Update: 조건부 효과 갱신 =====

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight) return;

            bool needRecalc = false;

            // 폭주 모드: HP 30% 이하 체크
            if (hasBerserkMode)
                needRecalc |= UpdateBerserkMode();

            // 가속 엔진: 시간 기반 스탯 증가
            if (hasAccelEngine)
                needRecalc |= UpdateAccelEngine();

            // 단결: 팀원 밀집 체크
            if (hasUnity)
                needRecalc |= UpdateUnity();

            // 연쇄 폭발: 프레임 카운터 리셋
            if (hasChainExplosion)
                chainCountThisFrame = 0;

            if (needRecalc)
                RecalculateModifiers();
        }

        private bool UpdateBerserkMode()
        {
            if (playerDamageable == null) return false;

            bool isBerserk = playerDamageable.CurrentHP <= playerDamageable.MaxHP * 0.3f;
            float newCDR = isBerserk ? 0.5f : 1f;
            float newSpd = isBerserk ? baseMoveSpeed * 0.5f : 0f;

            if (Mathf.Abs(ChaosCooldownMultiplier - newCDR) > 0.01f ||
                Mathf.Abs(ChaosMoveSpeedBonus - newSpd) > 0.01f)
            {
                ChaosCooldownMultiplier = newCDR;
                ChaosMoveSpeedBonus = newSpd;
                return true;
            }
            return false;
        }

        private bool UpdateAccelEngine()
        {
            // 0분 +0%, 5분 +25%, 10분 +50% (선형 보간)
            float gameTime = GameManager.Instance.GameTime;
            float bonus = Mathf.Lerp(0f, 0.5f, gameTime / 600f);

            float newMul = 1f + bonus;
            if (hasGlassCannon)
                newMul += 1f; // 유리대포 2배와 합산

            if (Mathf.Abs(ChaosAttackMultiplier - newMul) > 0.01f)
            {
                ChaosAttackMultiplier = newMul;
                return true;
            }
            return false;
        }

        private bool UpdateUnity()
        {
            unityCheckTimer += Time.deltaTime;
            if (unityCheckTimer < UNITY_CHECK_INTERVAL) return false;
            unityCheckTimer = 0f;

            // 주변 플레이어 수 카운트
            int count = 0;
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                if (p == transform.root.gameObject) continue; // 자기 자신 제외
                if (!p.activeInHierarchy) continue;

                float dist = Vector2.Distance(transform.root.position, p.transform.position);
                if (dist <= unityCheckRadius)
                    count++;
            }

            if (count != nearbyPlayerCount)
            {
                nearbyPlayerCount = count;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 모든 chaos modifier를 현재 상태에 맞게 재계산.
        /// </summary>
        private void RecalculateModifiers()
        {
            float attackMul = 1f;
            float cdMul = 1f;
            float moveBonus = 0f;
            float hpMul = 1f;

            // 유리대포
            if (hasGlassCannon)
            {
                hpMul = 0.5f;
                attackMul *= 2f;
            }

            // 가속 엔진
            if (hasAccelEngine)
            {
                float gameTime = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
                float bonus = Mathf.Lerp(0f, 0.5f, gameTime / 600f);
                attackMul += bonus;
                // 가속 엔진은 "모든 스탯" 증가 — 이속도 포함
                moveBonus += baseMoveSpeed * bonus;
            }

            // 폭주 모드
            if (hasBerserkMode && playerDamageable != null)
            {
                bool isBerserk = playerDamageable.CurrentHP <= playerDamageable.MaxHP * 0.3f;
                if (isBerserk)
                {
                    cdMul = 0.5f;
                    moveBonus += baseMoveSpeed * 0.5f;
                }
            }

            // 단결
            if (hasUnity && nearbyPlayerCount > 0)
            {
                // 2명 +20%, 3명 +30%, 4명 +40%
                float unityBonus = nearbyPlayerCount * 0.1f + 0.1f;
                attackMul += unityBonus;
            }

            ChaosAttackMultiplier = attackMul;
            ChaosCooldownMultiplier = cdMul;
            ChaosMoveSpeedBonus = moveBonus;
            ChaosMaxHPMultiplier = hpMul;
        }

        // ===== 연쇄 폭발 =====

        /// <summary>
        /// 적 사망 시 호출 (호스트만). SpawnManager 또는 Enemy.OnDied에서 연결.
        /// </summary>
        public void OnEnemyKilled(Vector2 position)
        {
            if (!hasChainExplosion) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (chainCountThisFrame >= maxChainPerFrame) return;

            chainCountThisFrame++;
            TriggerExplosion(position);
        }

        private void TriggerExplosion(Vector2 position)
        {
            // 비주얼 (있으면)
            if (explosionEffectPrefab != null)
            {
                var fx = PoolManager.Instance?.Get(explosionEffectPrefab);
                if (fx != null)
                    fx.transform.position = position;
            }

            // 범위 데미지
            var hits = Physics2D.OverlapCircleAll(position, explosionRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(explosionDamage);
                    // 이 적이 죽으면 또 OnEnemyKilled가 호출되어 연쇄
                }
            }
        }

        // ===== 외부 접근 =====

        public bool HasChaosEffect(ChaosEffectType type)
        {
            return activeChaosEffects.Contains(type);
        }

        // ===== 디버그 =====

        public string GetDebugString()
        {
            if (activeChaosEffects.Count == 0) return "";
            return string.Join(", ", activeChaosEffects);
        }
    }
}