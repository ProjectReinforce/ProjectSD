using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Shared.Data;
using SwDreams.Features.Skill.Adapter.TriggerEffects;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// Executor → Spawner 전달 컨텍스트.
    /// applicableStats 필터 적용 완료된 스탯값을 포함.
    /// Spawner는 PlayerStats를 직접 참조하지 않고 이 컨텍스트만 사용.
    ///
    /// [Phase 7 리팩토링] Step 4 — Executor 패턴
    /// </summary>
    public struct SpawnContext
    {
        /// <summary>스킬 데이터 (SO 참조).</summary>
        public SkillData skillData;

        /// <summary>현재 스킬 레벨 기준 데미지 (공격력 배율 적용 완료).</summary>
        public int damage;

        /// <summary>현재 스킬 레벨 기준 데미지 (배율 미적용 원본). 회복 장판 등 별도 배율 적용 시 사용.</summary>
        public int rawDamage;

        /// <summary>발사 기준 방향. SpreadPattern 적용 전 base direction.</summary>
        public Vector2 baseDirection;

        /// <summary>플레이어 위치.</summary>
        public Vector2 playerPosition;

        /// <summary>플레이어 Transform (궤도/추적 등에서 필요).</summary>
        public Transform playerTransform;

        /// <summary>현재 발사의 인덱스 (0-based). DelayedBurst에서 몇 번째 발사인지.</summary>
        public int fireIndex;

        /// <summary>총 발사 횟수.</summary>
        public int totalCount;

        // ===== applicableStats 필터 적용 완료된 값 =====

        /// <summary>투사체 속도 (패시브 보너스 적용 완료).</summary>
        public float projectileSpeed;

        /// <summary>투사체 개수 (패시브 보너스 적용 완료).</summary>
        public int projectileCount;

        /// <summary>넉백 힘 (패시브 보너스 적용 완료).</summary>
        public float knockbackForce;

        /// <summary>스킬 범위 보너스 (패시브 보너스 적용 완료).</summary>
        public float skillRangeBonus;

        /// <summary>스킬 유지시간 보너스 (패시브 보너스 적용 완료).</summary>
        public float skillDurationBonus;

        /// <summary>회복량 배율 (패시브 보너스 적용 완료).</summary>
        public float healMultiplier;

        /// <summary>치명타 데미지 배율 (패시브 보너스 적용 완료).</summary>
        public float critDamageMultiplier;

        /// <summary>치명타 확률 (0~1, 패시브 보너스 적용 완료). PlacedTurret.alwaysCritical 같은 강제 치명타는 1f 로 주입.</summary>
        public float critChance;

        // ===== TriggerSystem 참조 =====

        /// <summary>스킬의 TriggerSystem. 투사체/장판에 연결용. null 가능.</summary>
        public SkillTriggerSystem triggerSystem;

        /// <summary>
        /// Phase1 완료 콜백 (TwoPhase 전용).
        /// OrbitalObject가 1바퀴 완주 시 (position, outwardDirection)을 Executor에 전달.
        /// </summary>
        public System.Action<Vector2, Vector2> onSpawnComplete;

        // ===== N15/N17 호스트 권위 동기화 (Phase 1) =====

        /// <summary>
        /// 외부에서 결정된 spawn 위치. Random.insideUnitCircle 같은 비결정적 결정을
        /// 자기 클라가 한 번만 수행 → RPC 인자로 모든 클라에 전파 → ctx 에 주입.
        /// hasSpawnPosOverride == true 면 Spawner 가 자체 Random 대신 spawnPosOverride 사용.
        /// </summary>
        public Vector2 spawnPosOverride;

        /// <summary>spawnPosOverride 가 유효한지. Vector2.zero 가 valid 위치라 sentinel 분리 필요.</summary>
        public bool hasSpawnPosOverride;

        // ===== 인-런 통계 / 메타 진행도 (B-1a — run-statistics.md §4) =====

        /// <summary>
        /// 데미지 가해자 ActorNumber. SkillExecutor.BuildContext 에서
        /// PhotonNetwork.LocalPlayer.ActorNumber 주입. Spawner → 인스턴스 →
        /// TriggerContext.attackerActorNumber 로 흘려보냄.
        /// 자기 ActorNumber 매칭 시 자기 PC 통계 누적, 적/보스 사망 시 LastDamagerActorNumber 전파.
        /// </summary>
        public int attackerActorNumber;
    }

    /// <summary>
    /// 스킬 스폰 로직 인터페이스.
    /// 각 구현체가 "무엇을 어떻게 만들지"를 담당.
    /// Executor는 "언제" 이 Spawn을 호출할지만 관리.
    ///
    /// 구현체:
    ///   ProjectileSpawner  — 투사체 생성 + Trajectory 부착
    ///   AreaSpawner        — 장판/지대 생성
    ///   OrbitalSpawner     — 회전 오브젝트 생성 + 궤도 관리
    ///   PlacedSpawner      — 설치형 오브젝트 (포탑 등)
    ///   DebuffSpawner      — 디버프 마커 부착
    ///
    /// [Phase 7 리팩토링] Step 4 — Executor 패턴
    /// </summary>
    public interface ISkillSpawner
    {
        /// <summary>
        /// 스폰 실행. Executor가 FiringMode 타이밍에 맞춰 호출.
        /// SimultaneousSpread: fireIndex별로 한 프레임에 여러 번 호출
        /// DelayedBurst: 딜레이 간격으로 호출
        /// Single: 한 번만 호출 (fireIndex=0, totalCount=1)
        /// </summary>
        void Spawn(SpawnContext context);

        /// <summary>
        /// 이 Spawner가 사용할 프리팹/리소스를 프리웜.
        /// SkillManager.CreateSkillSlot()에서 최초 1회 호출.
        /// </summary>
        void Prewarm(SkillData data);

        /// <summary>
        /// Executor 강제 정리 시 호출 (레벨업/사망 등).
        /// 진행 중인 스폰 정리가 필요한 경우 구현.
        /// 기본적으로 no-op이어도 됨.
        /// </summary>
        void Cleanup();

        /// <summary>
        /// [N15/N17] 자기 클라가 발사 결정 시 비결정적 spawn 위치(Random 등) 를 한 번만 결정.
        /// 결정한 spawnPos 는 RPC 인자로 모든 클라에 전파되어 동일 위치 보장.
        /// override 가 필요 없는 Spawner (Projectile / Orbital / Placed / Debuff) 는 false 반환.
        /// AreaSpawner 만 Random.insideUnitCircle 결정 후 true 반환.
        /// </summary>
        bool TryGenerateSpawnPos(SpawnContext context, out Vector2 spawnPos);
    }
}