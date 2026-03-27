namespace SwDreams.Domain.ValueObjects
{
    /// <summary>
    /// 스킬 발사 모드. SkillExecutor가 이 모드에 따라 타이밍을 제어.
    /// SkillData.firingMode에서 설정.
    ///
    /// [Phase 7 리팩토링] Step 4 — Executor 패턴
    /// </summary>
    public enum FiringMode
    {
        /// <summary>
        /// 한 프레임에 n개 동시 발사.
        /// 적용: 표창, 부메랑, 회오리바람, 톱날, 얼음 고리
        /// </summary>
        SimultaneousSpread,

        /// <summary>
        /// count만큼 시간차 발사. burstDelay 간격으로 Executor가 Update에서 처리.
        /// 각 발사 시점에 방향/위치 재계산 가능.
        /// 적용: 매직 미사일, 번개, 개미지옥, 자동포탑, 도깨비불, 지진(진화)
        /// </summary>
        DelayedBurst,

        /// <summary>
        /// Phase1 실행 → 완료 콜백 → Phase2 실행.
        /// Phase1/Phase2 각각 독립적인 Spawner 사용 가능.
        /// 적용: 장검 (회전 → 발사)
        /// </summary>
        TwoPhase,

        /// <summary>
        /// 오브젝트 1개만 생성. 투사체 개수 스탯 무시.
        /// 적용: 성역, 저주인형, 장풍, 독버섯, 바나나, 화염자취, 별똥별
        /// </summary>
        Single
    }
}