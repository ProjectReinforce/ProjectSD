using System;
using System.Collections.Generic;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Adapter.Chaos;
using SwDreams.Features.Skill.Adapter.Chaos.StatWatchers;
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
    ///
    /// [Phase 8-B 리팩터] IChaosHookBus 구현 — SpawnManager/PlayerHealth/LevelUpManager 등은
    /// 구체 메서드 대신 이 포트로 이벤트 발행. 각 혼돈 효과가 핸들러화되면 switch 없이
    /// 포트 구독만으로 반응. 현재는 기존 switch 와 병존 (점진 이전).
    /// 이전 완료: ChainExplosion (8-B), Gambler (8-B3).
    /// </summary>
    public class ChaosSkillManager : MonoBehaviour, IChaosHookBus
    {
        // ===== 활성 혼돈 스킬 플래그 =====
        // ChainExplosion / Gambler 는 handler 경유 (Phase 8-B) — 플래그 불필요.
        private bool hasGlassCannon;
        private bool hasBerserkMode;
        private bool hasAccelEngine;
        private bool hasUnity;

        // ===== 연쇄 폭발 설정 (ChainExplosionHandler 에 주입되는 fallback) =====
        // Phase 8-B: 실 로직은 Chaos/Handlers/ChainExplosionHandler. 여기는 SO 미설정 시 fallback + prefab 참조만.
        [Header("연쇄 폭발 — handler 에 주입되는 fallback")]
        [SerializeField] private float explosionRadius = 2f;
        [SerializeField] private int explosionDamage = 20;
        [SerializeField] private int maxChainPerFrame = 5;
        [SerializeField] private GameObject explosionEffectPrefab;

        // ===== 단결 설정 =====
        [Header("단결")]
        [SerializeField] private float unityCheckRadius = 5f;
        private const float UNITY_CHECK_INTERVAL = 0.5f;

        // ===== Phase 8-C StatWatcher =====
        // 조건 변화 감지 책임을 watcher 객체로 분리. ChaosSkillManager 는
        // Tick() 결과만 모아서 RecalculateChaosModifiers 트리거.
        // 효과별 인스턴스는 RecalculateChaosModifiers 가 상태 조회용으로 직접 참조.
        private readonly List<StatWatcher> watchers = new List<StatWatcher>();
        private HpThresholdWatcher berserkWatcher;
        private TimerRampWatcher accelWatcher;
        private NearbyCountWatcher unityWatcher;

        // ===== 비수치 효과 프로퍼티 =====

        /// <summary>
        /// 도박꾼 활성 여부. LevelUpManager (호스트) 가 파티 전체 순회 시 참조 예정.
        /// 실 상태는 <see cref="Chaos.Handlers.GamblerHandler.IsActive"/> 에 보관 — 여기는 래퍼.
        /// </summary>
        public bool IsGambler
        {
            get
            {
                if (effectRegistry != null &&
                    effectRegistry.TryGet(ChaosEffectType.Gambler, out var h) &&
                    h is Chaos.Handlers.GamblerHandler gh)
                    return gh.IsActive;
                return false;
            }
        }

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

        // ===== IChaosHookBus 구현 (Phase 8-B) =====
        // 각 혼돈 효과가 switch 없이 구독할 수 있는 중앙 이벤트. 현재는 기존 switch 와 공존.
        // 이벤트 이름은 "On" 접두 없음 — 기존 메서드(OnEnemyKilled 등)와 구분.
        public event Action<Vector2, bool> EnemyKilled;
        public event Action<int> PlayerTakeDamage;
        public event Action PlayerDeath;
        public event Action LevelUpChoice;

        /// <summary>
        /// 혼돈 효과 handler 등록소. 현재 등록된 handler 가 있으면 Apply 시 위임,
        /// 없으면 기존 switch 경로 (ApplyGlassCannon 등) 사용.
        /// </summary>
        private ChaosEffectRegistry effectRegistry;

        // ===== 캐시 =====
        private IDamageable playerDamageable;
        private PlayerStats playerStats;
        private float baseMoveSpeed;

        private void Start()
        {
            playerDamageable = GetComponentInParent<IDamageable>();
            CachePlayerStats();

            // Phase 8-B: handler registry 초기화 + SerializeField 주입이 필요한 handler 직접 등록.
            effectRegistry = new ChaosEffectRegistry();
            effectRegistry.RegisterDefaults();
            effectRegistry.Register(new Chaos.Handlers.ChainExplosionHandler(
                explosionEffectPrefab,
                maxChainPerFrame,
                explosionDamage,
                explosionRadius));

            // 4인 동시 적 사망 시 폭발 spike 방지 prewarm. 자기 PlayerStub 만 1회.
            // ChainExplosion 혼돈 스킬 미선택 시 낭비지만 16 GameObject 메모리 미미 + 4명 중 누군가는 거의 픽함.
            var localPV = GetComponentInParent<Photon.Pun.PhotonView>();
            if (localPV != null && localPV.IsMine && explosionEffectPrefab != null)
                PoolManager.Instance?.Prewarm(explosionEffectPrefab, 16);
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

            // Phase 8-B: handler 가 등록된 타입이면 registry 경유 → switch 우회.
            // 등록 안 된 타입은 기존 switch 경로 사용 (점진 이전).
            // TODO Phase 8-B2: 모든 혼돈이 handler 로 이전되면 아래 switch + Apply{XXX} 메서드 전체 삭제.
            if (effectRegistry != null &&
                effectRegistry.TryGet(data.chaosEffectType, out var handler) &&
                data is ChaosSkillData chaosDataForHandler)
            {
                var ctx = new ChaosHandlerContext
                {
                    playerRoot = transform.root,
                    stats = playerStats,
                    hookBus = this,
                };
                handler.Apply(chaosDataForHandler, rolledRarity, ctx);
                Debug.Log($"[ChaosSkillManager] Handler 경로 적용: {data.skillName} ({data.chaosEffectType})");
                return;
            }

            switch (data.chaosEffectType)
            {
                case ChaosEffectType.GlassCannon:
                    ApplyGlassCannon();
                    break;
                // ChainExplosion — handler 로 이전 (Phase 8-B). registry 분기에서 처리.
                case ChaosEffectType.BerserkMode:
                    ApplyBerserkMode();
                    break;
                case ChaosEffectType.AccelEngine:
                    ApplyAccelEngine();
                    break;
                case ChaosEffectType.Unity:
                    ApplyUnity();
                    break;
                // Gambler — handler 로 이전 (Phase 8-B3). registry 분기에서 처리.
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

        // ApplyChainExplosion 제거 — ChainExplosionHandler 로 이전 (Phase 8-B).
        // Registry 분기에서 handler 가 SO/rarity 와 hookBus 를 받아 EnemyKilled 이벤트 구독.

        private void ApplyBerserkMode()
        {
            hasBerserkMode = true;

            // HP 임계 watcher — secondary = 발동 임계 비율 (Common 0.3 기본).
            // IDamageable 은 provider — Start 전 ApplyChaos 케이스에서 null 회복 가능.
            berserkWatcher = new HpThresholdWatcher(
                () =>
                {
                    if (playerDamageable == null)
                        playerDamageable = GetComponentInParent<IDamageable>();
                    return playerDamageable;
                },
                () => GetParams(ChaosEffectType.BerserkMode,
                    new EffectParams(0.9f, 0.3f, 1.1f)).secondary);
            watchers.Add(berserkWatcher);
        }

        private void ApplyAccelEngine()
        {
            hasAccelEngine = true;

            // 시간 비례 램프 watcher — primary = 최대 보너스, secondary = 램프 시간(0 이면 600 기본).
            accelWatcher = new TimerRampWatcher(
                () => GameManager.Instance != null ? GameManager.Instance.GameTime : 0f,
                () => GetParams(ChaosEffectType.AccelEngine,
                    new EffectParams(0.1f, 600f, 0f)).primary,
                () => GetParams(ChaosEffectType.AccelEngine,
                    new EffectParams(0.1f, 600f, 0f)).secondary);
            watchers.Add(accelWatcher);
        }

        private void ApplyUnity()
        {
            hasUnity = true;

            // 근접 인원 watcher — tertiary = 감지 반경(0 이면 인스펙터 기본값).
            unityWatcher = new NearbyCountWatcher(
                transform.root,
                "Player",
                () =>
                {
                    var p = GetParams(ChaosEffectType.Unity,
                        new EffectParams(0.1f, 0.1f, 0f));
                    return p.tertiary > 0f ? p.tertiary : unityCheckRadius;
                },
                UNITY_CHECK_INTERVAL);
            watchers.Add(unityWatcher);
        }

        // ApplyGambler 제거 — GamblerHandler 로 이전 (Phase 8-B3).
        // Registry 분기에서 handler 가 hookBus 구독 + IsActive 토글.

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

            // Phase 8-C: 등록된 watcher 들 일괄 Tick. 하나라도 변화 감지하면 recalc.
            // ChainExplosion / Gambler 는 handler 측에서 자체 처리 — 여기 Tick 없음.
            bool needRecalc = false;
            for (int i = 0; i < watchers.Count; i++)
                needRecalc |= watchers[i].Tick();

            if (needRecalc)
                RecalculateChaosModifiers();
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
            // bonus 값은 TimerRampWatcher 가 누적 추적, recalc 트리거도 watcher 가 발생.
            if (hasAccelEngine && accelWatcher != null)
            {
                float bonus = accelWatcher.CurrentBonus;
                AddAccelBonus(StatType.AttackMultiplier, bonus);
                AddAccelBonus(StatType.MoveSpeed, bonus);
                AddAccelBonus(StatType.SkillRange, bonus);
                AddAccelBonus(StatType.CooldownReduction, bonus);
                AddAccelBonus(StatType.CritDamage, bonus);
                AddAccelBonus(StatType.ProjectileSpeed, bonus);
            }

            // 3) Berserk — HP 임계 이하 시 발동. Multiplicative 로 "최종 이속/CDR 배율"
            // 활성 여부는 HpThresholdWatcher 가 추적 — 본 메서드는 결과만 반영.
            if (hasBerserkMode && berserkWatcher != null)
            {
                var p = GetParams(ChaosEffectType.BerserkMode,
                    new EffectParams(0.9f, 0.3f, 1.1f));
                bool isBerserk = berserkWatcher.IsActive;
                // 비활성 상태는 value=1 (Multiplicative 항등원). AddOrReplace 로 매번 덮어씀.
                float cdrMul = isBerserk ? p.primary   : 1f;
                float spdMul = isBerserk ? p.tertiary  : 1f;
                playerStats.AddModifier(new StatModifier(
                    "chaos_berserk_cdr", StatType.CooldownReduction, ModifierOp.Multiplicative, cdrMul));
                playerStats.AddModifier(new StatModifier(
                    "chaos_berserk_spd", StatType.MoveSpeed, ModifierOp.Multiplicative, spdMul));
            }

            // 4) Unity — 근접 아군 수 기반 PercentBonus 데미지 증폭
            // 공식: primary + (nearby - 1) * secondary. 혼자(nearby=0)면 modifier 제거.
            // 인원 수는 NearbyCountWatcher 가 interval 폴링으로 추적.
            if (hasUnity && unityWatcher != null)
            {
                int nearby = unityWatcher.Count;
                if (nearby > 0)
                {
                    var p = GetParams(ChaosEffectType.Unity,
                        new EffectParams(0.03f, 0.02f, 0f));
                    float unityBonus = p.primary + (nearby - 1) * p.secondary;
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

        // ===== 연쇄 폭발 이벤트 발행 (Phase 8-B: handler 로 이전됨) =====

        /// <summary>
        /// SpawnManager.OnEnemyDied 가 호출. 호스트 권위 경로 — EnemyKilled 훅 발행.
        /// ChainExplosionHandler 가 구독해 실제 데미지/비주얼 처리.
        /// </summary>
        public void OnEnemyKilled(Vector2 position)
        {
            EnemyKilled?.Invoke(position, false);
        }

        /// <summary>
        /// 클라이언트용: EnemyKilled 훅 비주얼 전용 경로.
        /// SpawnManager.OnReceiveDeathBatch 에서 호출.
        /// </summary>
        public void OnEnemyKilledVisualOnly(Vector2 position)
        {
            EnemyKilled?.Invoke(position, true);
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