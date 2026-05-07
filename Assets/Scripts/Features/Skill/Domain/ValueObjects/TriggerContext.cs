using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;

namespace SwDreams.Features.Skill.Domain.ValueObjects
{
    /// <summary>
    /// 트리거 발동 시 핸들러에 전달되는 컨텍스트.
    /// 모든 필드가 항상 유효하지는 않음 — TriggerType에 따라 다름.
    ///
    /// OnHit:   position=적중 위치, target=맞은 적, damage=입힌 데미지, skill=발동 스킬
    /// OnKill:  position=처치 위치, target=죽은 적, damage=마지막 데미지, skill=발동 스킬
    /// OnFire:  position=플레이어 위치, direction=발사 방향, skill=발동 스킬
    /// OnExpire: position=소멸 위치, skill=발동 스킬
    /// OnInterval: position=플레이어 위치, skill=발동 스킬
    /// OnPlayerHit: position=플레이어 위치, damage=받은 데미지
    ///
    /// [Phase 7 리팩토링] Step 3-1
    /// </summary>
    public struct TriggerContext
    {
        /// <summary>이벤트 발생 위치.</summary>
        public Vector2 position;

        /// <summary>발사/이동 방향 (OnFire 등).</summary>
        public Vector2 direction;

        /// <summary>관련 적 Transform (OnHit, OnKill). null 가능.</summary>
        public Transform target;

        /// <summary>관련 데미지 수치. 0이면 해당 없음.</summary>
        public int damage;

        /// <summary>트리거를 유발한 스킬 참조. null 가능.</summary>
        public MonoBehaviour skillRef;

        /// <summary>스킬 소유자(플레이어) Transform.</summary>
        public Transform owner;

        /// <summary>서브 투사체 프리팹. SpawnProjectileHandler에서 사용. null 가능.</summary>
        public GameObject subProjectilePrefab;

        /// <summary>
        /// 런타임 효과 식별자. SkillTriggerSystem.FireTrigger 가 runtime 효과 실행 시 주입.
        /// baseEffect 실행 시 null/빈 문자열.
        /// 핸들러가 "같은 source 기존 인스턴스 갱신" 같은 중첩 제어에 사용 (예: ApplyDoT/ApplySlow).
        /// </summary>
        public string source;

        /// <summary>
        /// 치명타 확률 (0~1). SkillExecutor.BuildContext 에서 applicableStats 가드 후 주입.
        /// 핸들러는 이 값과 critDamageMultiplier 로 치명타 판정.
        /// 0 이면 치명타 없음 (강제 isCrit=true 경로 — PlacedTurret alwaysCritical — 는 1f 로 주입).
        /// </summary>
        public float critChance;

        /// <summary>
        /// 치명타 데미지 배율 (보통 1.5~). PlayerStats.CritDamageMultiplier 에서 주입.
        /// </summary>
        public float critDamageMultiplier;

        /// <summary>
        /// 데미지 가해자 ActorNumber (B-1a — run-statistics.md §4).
        /// SkillExecutor.BuildContext 에서 PhotonNetwork.LocalPlayer.ActorNumber 주입.
        /// DealDamage*Handler 에서 자기 ActorNumber 매칭 시 자기 PC 통계 누적,
        /// 사망 시 Enemy.LastDamagerActorNumber 전파.
        /// </summary>
        public int attackerActorNumber;

        /// <summary>
        /// 발사 스킬의 SkillData.skillId (B-1a — run-statistics.md §4).
        /// SkillExecutor.BuildContext 에서 ctx.skillData.skillId 주입.
        /// 0 이면 미상 (적 자해/장판 등 기본 데미지 경로).
        /// 자기 막타 시 사망 RPC 페이로드 killerSkillId 로 전파 → 결과 화면 스킬별 차트.
        /// </summary>
        public int sourceSkillId;
    }
}