using System;

namespace SwDreams.Features.Unlock.Domain
{
    /// <summary>
    /// 메타 언락 조건 1개. SkillData/WeaponData/CharacterData 등에 List 로 부착.
    ///
    /// [Serializable] struct 채택 이유 (meta-unlock.md §4 WHY):
    /// - 프로젝트에 [SerializeReference] 사용 0건 — 추상 클래스 + List 직렬화 패턴이 없음
    /// - enum 분기가 인스펙터 친화적 + 직렬화 안전
    ///
    /// 평가는 UnlockEvaluator.Evaluate(condition, IRunStats) 로 분리.
    /// </summary>
    [Serializable]
    public struct UnlockCondition
    {
        /// <summary>조건 종류. None 이면 처음부터 해금.</summary>
        public UnlockConditionType type;

        /// <summary>KillCount/RunsCleared 의 N 값.</summary>
        public int targetValue;

        /// <summary>보스/적/존 id (BossDefeat/ZoneVisited/DeathByEnemy 용).</summary>
        public int targetIdA;

        /// <summary>예비 필드 — 향후 복합 조건 확장 시 사용.</summary>
        public int targetIdB;
    }
}
