using System.Collections.Generic;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Features.Skill.Domain.ValueObjects;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Data;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 혼돈 스킬 효과 관리. 각 플레이어에 부착.
    /// SkillManager.ApplyChoice()에서 Chaos 타입이면 여기로 위임.
    ///
    /// [Step 1-4] 수치 효과는 PlayerStats의 StatModifier로 통합.
    /// 비수치 효과(연쇄 폭발, 도박꾼)만 이 클래스에서 관리.
    ///
    /// [Phase 8-A 리팩터] modifier source 네이밍 — 혼돈별 독립 + op 별:
    ///   "chaos_gc_atk"          — GlassCannon ATK 배율 (Multiplicative)
    ///   "chaos_gc_hp"           — GlassCannon HP 배율 (Multiplicative)
    ///   "chaos_accel_{StatType}" — AccelEngine 모든 스탯 +% (PercentBonus)
    ///   "chaos_berserk_cdr"     — Berserk CDR 배율 (Multiplicative, 비활성 시 1.0)
    ///   "chaos_berserk_spd"     — Berserk 이속 배율 (Multiplicative, 비활성 시 1.0)
    ///   "chaos_unity_atk"       — Unity 데미지 증폭 (PercentBonus, 근접 0 명 시 제거)
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
        [Header("연쇄 폭발 — fallback 기본값 (SO 의 paramsByRarity 미설정 시 사용)")]
        [SerializeField] private float explosionRadius = 2f;
        [SerializeField] private int explosionDamage = 20;
        [SerializeField] private int maxChainPerFrame = 5;
        [SerializeField] private GameObject explosionEffectPrefab;
        private int chainCountThisFrame;

        /// <summary>
        /// Chain Explosion params 해석 (primary=damage, secondary=radius).
        /// tertiary (프레임당 최대 연쇄) 는 설계 파라미터 아님 — 성능 무한 루프 가드이므로
        /// SerializeField <see cref="maxChainPerFrame"/> 만 사용.
        /// damage/radius 도 SO 미설정 시 SerializeField 로 fallback.
        /// </summary>
        private (int damage, float radius, int maxChain) GetChainExplosionConfig()
        {
            var p = GetParams(ChaosEffectType.ChainExplosion,
                new EffectParams(explosionDamage, explosionRadius, 0f));
            int dmg = p.primary > 0f ? Mathf.RoundToInt(p.primary) : explosionDamage;
            float r = p.secondary > 0f ? p.secondary : explosionRadius;
            return (dmg, r, maxChainPerFrame);
        }

        // ===== 단결 설정 =====
        [Header("단결")]
        [SerializeField] private float unityCheckRadius = 5f;
        private float unityCheckTimer;
        private const float UNITY_CHECK_INTERVAL = 0.5f;
        private int nearbyPlayerCount;

        // ===== 변경 감지용 캐시 (매 프레임 비교) =====
        // 각 혼돈 효과별로 "현재 상태가 바뀌었는지" 추적. 새 구조(혼돈별 독립 modifier) 에 맞춰 세분화.
        private bool cachedBerserkActive;      // Berserk HP 임계 통과 여부
        private float cachedAccelBonus = -1f;  // Accel 의 현재 bonus 값 (0~p.primary). -1 = 아직 초기화 안 됨.

        // ===== 비수치 효과 프로퍼티 =====

        /// <summary>도박꾼 활성 여부. SkillManager.GenerateChoices()에서 참조.</summary>
        public bool IsGambler => hasGambler;

        /// <summary>보유 중인 혼돈 스킬 목록 (디버그 오버레이, 보스 시스템용).</summary>
        private List<ChaosEffectType> activeChaosEffects = new List<ChaosEffectType>();
        public IReadOnlyList<ChaosEffectType> ActiveEffects => activeChaosEffects;

        /// <summary>
        /// 타입별 ChaosSkillData 참조 — paramsByRarity 해석에 사용.
        /// </summary>
        private readonly Dictionary<ChaosEffectType, ChaosSkillData> activeData =
            new Dictionary<ChaosEffectType, ChaosSkillData>();

        /// <summary>
        /// 타입별 rolledRarity — 장착 시점에 LevelUpManager 가 전달한 값.
        /// activeData 의 params 를 인덱싱할 때 사용.
        /// </summary>
        private readonly Dictionary<ChaosEffectType, Rarity> activeRarities =
            new Dictionary<ChaosEffectType, Rarity>();

        /// <summary>
        /// fallback 경고를 타입당 1회만 출력하기 위한 중복 방지 세트.
        /// </summary>
        private readonly HashSet<ChaosEffectType> warnedFallbackTypes = new HashSet<ChaosEffectType>();

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
        /// rolledRarity 는 해당 선택지가 뽑혔을 때의 등급 (paramsByRarity 인덱스).
        /// </summary>
        public void ApplyChaos(SkillData data, Rarity rolledRarity)
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

            // 데이터 + 등급 참조 저장 — paramsByRarity 해석용.
            if (data is ChaosSkillData chaosData)
                activeData[data.chaosEffectType] = chaosData;
            activeRarities[data.chaosEffectType] = rolledRarity;

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

        // ===== SO 파라미터 조회 (Phase 8-A) =====

        /// <summary>
        /// 지정 혼돈 효과의 SO 파라미터를 저장된 rolledRarity 기반으로 조회. SO / params 미설정이면 fallback.
        /// fallback 은 Common 등급 수치 (최약) — 개발자 실수로 플레이어 어드밴티지 주지 않음. 타입당 1 회 경고.
        /// </summary>
        private EffectParams GetParams(ChaosEffectType type, EffectParams fallback)
        {
            if (!activeData.TryGetValue(type, out var data) || data == null)
            {
                WarnFallbackOnce(type, "ChaosSkillData SO 참조 없음");
                return fallback;
            }
            Rarity r = activeRarities.TryGetValue(type, out var rr) ? rr : Rarity.Common;
            var p = data.GetParams(r);
            if (p.primary == 0f && p.secondary == 0f && p.tertiary == 0f)
            {
                WarnFallbackOnce(type, $"paramsByRarity[{r}] 전부 0");
                return fallback;
            }
            return p;
        }

        private void WarnFallbackOnce(ChaosEffectType type, string reason)
        {
            if (warnedFallbackTypes.Contains(type)) return;
            warnedFallbackTypes.Add(type);
            Debug.LogWarning($"[ChaosSkillManager] {type} — {reason}. Common 등급 fallback 사용. " +
                             "SO Inspector 의 paramsByRarity 를 채워주세요.");
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

            // Berserk 활성 여부가 바뀌었는지만 체크. 값 자체는 Recalculate 가 SO 에서 다시 읽음.
            var p = GetParams(ChaosEffectType.BerserkMode,
                new EffectParams(0.9f, 0.3f, 1.1f));
            bool nowActive = playerDamageable.CurrentHP <= playerDamageable.MaxHP * p.secondary;
            if (nowActive != cachedBerserkActive)
            {
                cachedBerserkActive = nowActive;
                return true;
            }
            return false;
        }

        private bool CheckAccelChanged()
        {
            // Accel bonus 는 시간에 따라 연속 변동. 임계값 이상 변하면 재계산.
            var p = GetParams(ChaosEffectType.AccelEngine,
                new EffectParams(0.1f, 600f, 0f));
            float gameTime = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
            float rampDur = p.secondary > 0f ? p.secondary : 600f;
            float bonus = Mathf.Lerp(0f, p.primary, gameTime / rampDur);

            if (Mathf.Abs(bonus - cachedAccelBonus) > 0.001f)
            {
                cachedAccelBonus = bonus;
                return true;
            }
            return false;
        }

        private bool CheckUnityChanged()
        {
            unityCheckTimer += Time.deltaTime;
            if (unityCheckTimer < UNITY_CHECK_INTERVAL) return false;
            unityCheckTimer = 0f;

            // Unity params: primary=기본 보너스, secondary=아군 당 추가, tertiary=감지 반경(0 이면 인스펙터값 사용)
            var p = GetParams(ChaosEffectType.Unity,
                new EffectParams(0.1f, 0.1f, 0f));
            float radius = p.tertiary > 0f ? p.tertiary : unityCheckRadius;

            int count = 0;
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var pl in players)
            {
                if (pl == transform.root.gameObject) continue;
                if (!pl.activeInHierarchy) continue;

                float dist = Vector2.Distance(transform.root.position, pl.transform.position);
                if (dist <= radius)
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
        /// 모든 chaos 수치 효과를 각 혼돈별 독립 modifier 로 PlayerStats 에 등록.
        ///
        /// 설계 철학:
        /// - Glass Cannon : HP/ATK 둘 다 "언제나 n 배" → Multiplicative.
        /// - Accel Engine : 시간 경과 → 모든 스탯 +N% → PercentBonus 로 여러 StatType 에 각각.
        /// - Berserk      : HP 임계 이하 발동 → CDR/이속 "배율" → Multiplicative (활성 외 1.0 = 영향 없음).
        /// - Unity        : 근접 아군 수 → 데미지 증폭 → PercentBonus.
        ///
        /// 변경 감지(CheckXxxChanged) 후 호출. Berserk 상태 토글 / Accel 램프 갱신 / Unity 카운트 변화 시 재실행.
        /// AddModifier 는 source+StatType 동일 시 replace — idempotent. 제거 경로는 Unity 0 명 케이스만.
        /// </summary>
        private void RecalculateChaosModifiers()
        {
            if (playerStats == null)
            {
                CachePlayerStats();
                if (playerStats == null) return;
            }

            // 1) Glass Cannon — HP, ATK 모두 Multiplicative (독립 곱)
            if (hasGlassCannon)
            {
                var p = GetParams(ChaosEffectType.GlassCannon,
                    new EffectParams(1.1f, 0.5f, 0f));
                playerStats.AddModifier(new StatModifier(
                    "chaos_gc_atk", StatType.AttackMultiplier, ModifierOp.Multiplicative, p.primary));
                playerStats.AddModifier(new StatModifier(
                    "chaos_gc_hp", StatType.MaxHP, ModifierOp.Multiplicative, p.secondary));
            }

            // 2) Accel Engine — 시간 경과 → 모든 주요 스탯 PercentBonus
            if (hasAccelEngine)
            {
                var p = GetParams(ChaosEffectType.AccelEngine,
                    new EffectParams(0.1f, 600f, 0f));
                float gameTime = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
                float rampDur = p.secondary > 0f ? p.secondary : 600f;
                float bonus = Mathf.Lerp(0f, p.primary, gameTime / rampDur);
                AddAccelBonus(StatType.AttackMultiplier, bonus);
                AddAccelBonus(StatType.MoveSpeed, bonus);
                AddAccelBonus(StatType.SkillRange, bonus);
                AddAccelBonus(StatType.CooldownReduction, bonus);
                AddAccelBonus(StatType.CritDamage, bonus);
                AddAccelBonus(StatType.ProjectileSpeed, bonus);
            }

            // 3) Berserk — HP 임계 이하 시 발동. Multiplicative 로 "최종 이속/CDR 배율"
            if (hasBerserkMode && playerDamageable != null)
            {
                var p = GetParams(ChaosEffectType.BerserkMode,
                    new EffectParams(0.9f, 0.3f, 1.1f));
                bool isBerserk = playerDamageable.CurrentHP <= playerDamageable.MaxHP * p.secondary;
                // 비활성 상태는 value=1 (Multiplicative 항등원). AddOrReplace 로 매번 덮어씀.
                float cdrMul = isBerserk ? p.primary   : 1f;
                float spdMul = isBerserk ? p.tertiary  : 1f;
                playerStats.AddModifier(new StatModifier(
                    "chaos_berserk_cdr", StatType.CooldownReduction, ModifierOp.Multiplicative, cdrMul));
                playerStats.AddModifier(new StatModifier(
                    "chaos_berserk_spd", StatType.MoveSpeed, ModifierOp.Multiplicative, spdMul));
            }

            // 4) Unity — 근접 아군 수 기반 PercentBonus 데미지 증폭
            // 공식: primary + (nearby - 1) * secondary. 혼자(nearby=0)면 modifier 제거 (bonus 0).
            if (hasUnity)
            {
                if (nearbyPlayerCount > 0)
                {
                    var p = GetParams(ChaosEffectType.Unity,
                        new EffectParams(0.03f, 0.02f, 0f));
                    float unityBonus = p.primary + (nearbyPlayerCount - 1) * p.secondary;
                    playerStats.AddModifier(new StatModifier(
                        "chaos_unity_atk", StatType.AttackMultiplier, ModifierOp.PercentBonus, unityBonus));
                }
                else
                {
                    playerStats.RemoveModifiersBySource("chaos_unity_atk");
                }
            }

            playerStats.Recalculate();
        }

        private void AddAccelBonus(StatType st, float bonus)
        {
            playerStats.AddModifier(new StatModifier(
                $"chaos_accel_{st}", st, ModifierOp.PercentBonus, bonus));
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

            var cfg = GetChainExplosionConfig();
            if (chainCountThisFrame >= cfg.maxChain) return;

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

            var cfg = GetChainExplosionConfig();
            if (chainCountThisFrame >= cfg.maxChain) return;

            chainCountThisFrame++;
            SpawnExplosionVisual(position);

            // 폭발 범위 내 적에게 비주얼 피드백 (데미지 팝업)
            var hits = Physics2D.OverlapCircleAll(position, cfg.radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                var enemy = hit.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                if (enemy != null && enemy.IsAlive)
                    enemy.ShowHitVisuals(cfg.damage);
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
            var cfg = GetChainExplosionConfig();
            var hits = Physics2D.OverlapCircleAll(position, cfg.radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(cfg.damage);
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