using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Data;
using SwDreams.Domain.Interfaces;
using SwDreams.Domain.ValueObjects;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 혼돈 스킬 효과 관리. 각 플레이어에 부착.
    /// SkillManager.ApplyChoice()에서 Chaos 타입이면 여기로 위임.
    ///
    /// [Step 1-4] 수치 효과는 PlayerStats의 StatModifier로 통합.
    /// 비수치 효과(연쇄 폭발, 도박꾼)만 이 클래스에서 관리.
    ///
    /// modifier source 규칙:
    ///   "chaos_attack"    — 공격력 배율 (유리대포 + 가속엔진 + 단결)
    ///   "chaos_maxhp"     — 최대 HP 배율 (유리대포)
    ///   "chaos_cdr"       — 쿨다운 배율 (폭주모드). Multiply on CooldownReduction.
    ///   "chaos_movespeed" — 이동속도 가산 (폭주모드 + 가속엔진)
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

        // ===== 변경 감지용 캐시 (매 프레임 비교) =====
        private float cachedAttackMul = 1f;
        private float cachedCdMul = 1f;
        private float cachedMoveBonus = 0f;
        private float cachedHpMul = 1f;

        // ===== 비수치 효과 프로퍼티 =====

        /// <summary>도박꾼 활성 여부. SkillManager.GenerateChoices()에서 참조.</summary>
        public bool IsGambler => hasGambler;

        /// <summary>보유 중인 혼돈 스킬 목록 (디버그 오버레이, 보스 시스템용).</summary>
        private List<ChaosEffectType> activeChaosEffects = new List<ChaosEffectType>();
        public IReadOnlyList<ChaosEffectType> ActiveEffects => activeChaosEffects;

        // ===== 캐시 =====
        private IDamageable playerDamageable;
        private PlayerStats playerStats;
        private float baseMoveSpeed;

        private void Start()
        {
            playerDamageable = GetComponentInParent<IDamageable>();
            CachePlayerStats();
        }

        private void CachePlayerStats()
        {
            if (playerStats != null) return;
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
            CachePlayerStats();

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

            // MaxHP를 50%로 낮추는 modifier 등록.
            // PlayerStats.Recalculate() → OnStatsChanged → PlayerHealth.OnPlayerStatsChanged()
            // → MaxHP 갱신 + CurrentHP를 새 MaxHP 이하로 자동 클램프.
            // 별도 TakeDamage 불필요 (이중 HP 감소 방지).
            RecalculateChaosModifiers();
        }

        private void ApplyChainExplosion()
        {
            hasChainExplosion = true;
        }

        private void ApplyBerserkMode()
        {
            hasBerserkMode = true;
        }

        private void ApplyAccelEngine()
        {
            hasAccelEngine = true;
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

            if (hasBerserkMode)
                needRecalc |= CheckBerserkChanged();

            if (hasAccelEngine)
                needRecalc |= CheckAccelChanged();

            if (hasUnity)
                needRecalc |= CheckUnityChanged();

            if (hasChainExplosion)
                chainCountThisFrame = 0;

            if (needRecalc)
                RecalculateChaosModifiers();
        }

        private bool CheckBerserkChanged()
        {
            if (playerDamageable == null) return false;

            bool isBerserk = playerDamageable.CurrentHP <= playerDamageable.MaxHP * 0.3f;
            float newCDR = isBerserk ? 0.5f : 1f;
            float newSpd = isBerserk ? baseMoveSpeed * 0.5f : 0f;

            if (Mathf.Abs(cachedCdMul - newCDR) > 0.01f ||
                Mathf.Abs(cachedMoveBonus - newSpd) > 0.01f)
                return true;

            return false;
        }

        private bool CheckAccelChanged()
        {
            float gameTime = GameManager.Instance.GameTime;
            float bonus = Mathf.Lerp(0f, 0.5f, gameTime / 600f);
            float newMul = 1f + bonus;
            if (hasGlassCannon)
                newMul += 1f;

            if (Mathf.Abs(cachedAttackMul - newMul) > 0.01f)
                return true;

            return false;
        }

        private bool CheckUnityChanged()
        {
            unityCheckTimer += Time.deltaTime;
            if (unityCheckTimer < UNITY_CHECK_INTERVAL) return false;
            unityCheckTimer = 0f;

            int count = 0;
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                if (p == transform.root.gameObject) continue;
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

        // ===== Modifier 등록 =====

        /// <summary>
        /// 모든 chaos 수치 효과를 계산하여 PlayerStats에 modifier로 등록.
        /// 값이 변경된 경우에만 호출됨 (Update의 변경 감지 후).
        /// </summary>
        private void RecalculateChaosModifiers()
        {
            if (playerStats == null)
            {
                CachePlayerStats();
                if (playerStats == null) return;
            }

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
                float unityBonus = nearbyPlayerCount * 0.1f + 0.1f;
                attackMul += unityBonus;
            }

            // 캐시 갱신
            cachedAttackMul = attackMul;
            cachedCdMul = cdMul;
            cachedMoveBonus = moveBonus;
            cachedHpMul = hpMul;

            // PlayerStats에 modifier 등록
            playerStats.AddModifier(new StatModifier(
                "chaos_attack", StatType.AttackMultiplier, ModifierOp.Multiply, attackMul));

            playerStats.AddModifier(new StatModifier(
                "chaos_cdr", StatType.CooldownReduction, ModifierOp.Multiply, cdMul));

            playerStats.AddModifier(new StatModifier(
                "chaos_movespeed", StatType.MoveSpeed, ModifierOp.Add, moveBonus));

            playerStats.AddModifier(new StatModifier(
                "chaos_maxhp", StatType.MaxHP, ModifierOp.Multiply, hpMul));

            playerStats.Recalculate();
        }

        // ===== 연쇄 폭발 =====

        /// <summary>
        /// 적 사망 시 호출 (호스트만). SpawnManager.OnEnemyDied에서 전체 플레이어 순회.
        /// 데미지: 호스트에서 모든 플레이어의 연쇄폭발 처리 (정상).
        /// 비주얼: 자기 캐릭터의 연쇄폭발만 표시 (다른 플레이어 것은 클라이언트에서 표시).
        /// </summary>
        public void OnEnemyKilled(Vector2 position)
        {
            if (!hasChainExplosion) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (chainCountThisFrame >= maxChainPerFrame) return;

            chainCountThisFrame++;

            // 데미지는 모든 플레이어의 연쇄폭발에 대해 처리
            TriggerExplosionDamage(position);

            // 비주얼은 자기(호스트) 캐릭터 것만 표시
            // 클라이언트 플레이어의 비주얼은 OnReceiveDeathBatch → OnEnemyKilledVisualOnly에서 처리
            if (IsLocalPlayer())
                SpawnExplosionVisual(position);
        }

        /// <summary>
        /// 클라이언트용: 연쇄폭발 비주얼 + 데미지 팝업 재생.
        /// SpawnManager.OnReceiveDeathBatch에서 호출.
        /// 자기 캐릭터의 연쇄폭발만 표시.
        /// </summary>
        public void OnEnemyKilledVisualOnly(Vector2 position)
        {
            if (!hasChainExplosion) return;
            if (!IsLocalPlayer()) return; // 자기 캐릭터 것만
            if (chainCountThisFrame >= maxChainPerFrame) return;

            chainCountThisFrame++;
            SpawnExplosionVisual(position);

            // 폭발 범위 내 적에게 비주얼 피드백 (데미지 팝업)
            var hits = Physics2D.OverlapCircleAll(position, explosionRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                var enemy = hit.GetComponent<SwDreams.Adapter.Entity.Enemy>();
                if (enemy != null && enemy.IsAlive)
                    enemy.ShowHitVisuals(explosionDamage);
            }
        }

        /// <summary>
        /// 이 ChaosSkillManager가 로컬 플레이어에 속하는지 확인.
        /// </summary>
        private bool IsLocalPlayer()
        {
            var pv = GetComponentInParent<PhotonView>();
            return pv != null && pv.IsMine;
        }

        private void SpawnExplosionVisual(Vector2 position)
        {
            if (explosionEffectPrefab != null)
            {
                var fx = PoolManager.Instance?.Get(explosionEffectPrefab);
                if (fx != null)
                    fx.transform.position = position;
            }
        }

        private void TriggerExplosionDamage(Vector2 position)
        {
            var hits = Physics2D.OverlapCircleAll(position, explosionRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(explosionDamage);
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