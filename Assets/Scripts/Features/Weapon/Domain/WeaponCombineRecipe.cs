using System;

namespace SwDreams.Features.Weapon.Domain
{
    /// <summary>
    /// 무기 조합 레시피. "재료 무기 id 2개 이상 → 결과 무기 id" 매핑.
    ///
    /// 인벤토리가 4 슬롯 가득 찬 상태에서 신규 무기 픽업 시, 이 레시피로
    /// (기존 슬롯 N 개 + 신규) → (결과 1) 조합 가능 여부를 검사.
    ///
    /// Domain VO — string 으로만 식별자를 다루고 WeaponData 참조는 갖지 않는다.
    /// Adapter 레이어(WeaponDatabase)가 id → WeaponData 를 해결한다.
    /// </summary>
    [Serializable]
    public struct WeaponCombineRecipe
    {
        /// <summary>재료 무기 id 목록 (2 개 이상). 순서 무관 — 동일 집합이면 매칭.</summary>
        public string[] inputWeaponIds;

        /// <summary>결과 무기 id. 비어있으면 유효하지 않은 레시피.</summary>
        public string outputWeaponId;

        public WeaponCombineRecipe(string[] inputWeaponIds, string outputWeaponId)
        {
            this.inputWeaponIds = inputWeaponIds;
            this.outputWeaponId = outputWeaponId;
        }

        public bool IsValid =>
            inputWeaponIds != null &&
            inputWeaponIds.Length >= 2 &&
            !string.IsNullOrEmpty(outputWeaponId);
    }
}
