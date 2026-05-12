using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Weapon.Domain;
using SwDreams.Features.Unlock.Domain;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Weapon.Adapter.Data
{
    /// <summary>
    /// 무기 SO. 장착 시 다음 두 채널로 기여한다:
    /// 1) <see cref="statEntries"/> → PlayerStats.AddModifier(source="weapon_{id}")
    /// 2) <see cref="triggerEffects"/> → SkillTriggerSystem.AddRuntimeEffect(source="weapon_{id}")
    ///
    /// 장비 해제/조합 시 source="weapon_{id}" prefix 로 일괄 제거.
    /// 조합 결과는 별도의 WeaponData SO 로 스왑된다 (기존 id 제거 + 결과 id 장착).
    ///
    /// 등급 색상은 WeaponSlotsUI 가 rarity 기반으로 런타임 틴트.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "SwDreams/Data/WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("영문/언더스코어 권장. source = \"weapon_{id}\" 로 조합되므로 고유해야 함.")]
        public string weaponId;

        [Tooltip("HUD/프롬프트 표시용 한글 이름.")]
        public string displayName;

        [Header("시각")]
        public Sprite icon;

        [Tooltip("HUD 슬롯/드랍 프리팹 SpriteRenderer 색 틴트. 등급별 색 코드로 오버라이드 가능.")]
        public Color iconColor = Color.white;

        [Header("등급")]
        [Tooltip("드랍 롤 시 가중치 기반. 슬롯 테두리 색상에도 사용.")]
        public Rarity rarity = Rarity.Common;

        [Header("스탯 보정 (장착 시 PlayerStats 에 주입)")]
        [Tooltip("엔트리별로 isUnique 체크 가능. true 면 같은 무기 여러 개 장착해도 1회분만 적용.")]
        public WeaponStatEntry[] statEntries;

        [Header("트리거 효과 (장착 시 전 스킬 SkillTriggerSystem 에 주입)")]
        [Tooltip("예) OnHit → ApplyDoT (화염 검) / OnKill → Explode (처형 특성).\n" +
                 "엔트리별로 isUnique 체크 가능.")]
        public WeaponTriggerEntry[] triggerEntries;

        [Header("조합")]
        [Tooltip("이 무기가 재료일 때의 조합 레시피. 결과만 다른 무기일 때 각자 SO 가 동일 레시피를 중복 들고 있어도 OK.\n" +
                 "비어있으면 조합 불가능 무기.")]
        public WeaponCombineRecipe combineRecipe;

        [Header("메타 언락 (meta-unlock.md §6 — 분산형 조건)")]
        [Tooltip("합성 결과물 무기에만 부여. 기본 무기는 빈 리스트 — 처음부터 사용 가능.\n" +
                 "조건 미충족 시 PlayerWeaponInventory.FindFirstMatchingRecipe 가 매칭에서 제외.")]
        public List<UnlockCondition> unlockConditions = new List<UnlockCondition>();

        [Tooltip("미해금 상태 UI 에서 '???' 표시 여부.")]
        public bool isHidden = false;
    }
}
