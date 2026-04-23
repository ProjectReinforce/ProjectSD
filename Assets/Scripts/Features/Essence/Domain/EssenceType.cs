namespace SwDreams.Features.Essence.Domain
{
    /// <summary>
    /// 정수(Essence) 속성. 순수 C#.
    ///
    /// 엘리트 적이 드랍하는 속성 정수. 플레이어는 최대 2개 보유 가능하며
    /// 장착 시 모든 스킬의 SkillTriggerSystem 에 런타임 효과를 주입한다.
    ///
    /// 등급 체계는 없다 — 속성 3종 중 가중치 롤로만 결정.
    /// 순서 변경 시 DropSpawnBatch 의 dataIdHash 자리 해석과 어긋나므로 금지.
    /// </summary>
    public enum EssenceType
    {
        Ice = 0,
        Fire = 1,
        Lightning = 2
    }
}
