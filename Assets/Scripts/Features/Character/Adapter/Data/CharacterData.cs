using UnityEngine;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Skill.Adapter.Data;

namespace SwDreams.Features.Character.Adapter.Data
{
    /// <summary>
    /// 캐릭터 데이터 ScriptableObject.
    /// 대기실에서 선택한 캐릭터 정보를 GameScene에 전달.
    ///
    /// 셋업:
    ///   Assets/Data/Characters/ 폴더에서 Create → SwDreams/CharacterData
    ///   캐릭터 3종 각각 SO 생성 후 필드 채우기.
    ///
    /// 네트워크:
    ///   대기실에서 characterId를 CustomProperties에 저장.
    ///   GameScene 진입 시 CharacterDatabase.GetById()로 SO 조회.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "SwDreams/CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("기본 정보")]
        public int id;
        public string displayName;
        public Sprite portrait;

        [Header("애니메이션")]
        [Tooltip("캐릭터 AnimatorController. AnimatorOverrideController 권장 (공통 base + 클립 override).\n" +
                 "Parameters 표준: IsMoving(Bool), Die(Trigger), Revive(Trigger), MoveX/MoveY(Float, 4방향용).\n" +
                 "비어있으면 Animator 미사용 — 정적 sprite 동작 (기존 동작 유지).")]
        public RuntimeAnimatorController animatorController;

        [Tooltip("스프라이트 피벗 보정 (월드 단위 X). 원본 PNG 의 캐릭터가 텍스처 정중앙에서 비대칭으로 어긋나 있을 때\n" +
                 "flipX 시 좌우로 튕겨 보이는 현상을 보정. 부호는 \"기본 facing 상태에서 캐릭터를 시각적 중심으로 옮기려면\n" +
                 "어느 방향으로 얼마나 밀어야 하는가\" 로 정함. 예: defaultFacingRight=true 인데 캐릭터가 피벗보다\n" +
                 "왼쪽으로 치우쳐 있으면 양수(예: 0.06) 입력. 런타임에 SpriteRenderer 자식 transform.localPosition.x 로\n" +
                 "보정하며 flipX 토글 시 자동으로 부호 반전. 0 이면 보정 없음.")]
        public float pivotOffsetX = 0f;

        [Header("시작 스킬")]
        [Tooltip("게임 시작 시 자동 획득하는 액티브 스킬")]
        public SkillData startingActiveSkill;

        [Tooltip("게임 시작 시 자동 획득하는 패시브 스킬 (기존 13종 중 1개)")]
        public SkillData startingPassiveSkill;

        [Header("Base 스탯")]
        public int maxHP = 100;
        public float moveSpeed = 0.8f;
        public float attackMultiplier = 1f;
        public float projectileSpeed = 0f;
        public int projectileCount = 0;
        public float skillRange = 0f;
        [Range(0f, 1f)]
        public float cooldownReduction = 0f;
        public float knockback = 1f;
        public float critDamage = 1.5f;

        [Tooltip("치명타 확률 (0~1). 0.05 = 5%. 패시브 #15 / 무기 등에 의해 가산.")]
        [Range(0f, 1f)]
        public float critChance = 0.05f;

        public float expMultiplier = 1f;

        [Tooltip("방어 보너스 (양수 = 강함). 0.05 = 받는 데미지 5% 감소. 패시브와 동일한 입력 컨벤션.")]
        public float defenseBonus = 0f;

        public float healMultiplier = 1f;
        public float skillDuration = 0f;

        [Tooltip("체력 자연회복 (HP/초). HealMultiplier 영향 안 받음.")]
        public float hpRegen = 0f;

        [Tooltip("피격 후 무적 시간 (초).")]
        public float iFrameDuration = 0.4f;
    }
}