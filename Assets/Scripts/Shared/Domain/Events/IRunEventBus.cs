using System;

namespace SwDreams.Shared.Domain.Events
{
    /// <summary>
    /// 런(한 판) 도중/종료 이벤트 버스. Feature 간 디커플링용.
    ///
    /// 발화자: Stats(LocalStatsRecorder), Quest(QuestZone), GameManager 등.
    /// 구독자: Unlock(MetaProgressStore), 기타 분석 시스템.
    ///
    /// 이 인터페이스가 Shared/Domain 에 있어야 양쪽 Feature 가 자기 Adapter 끼리
    /// 직접 참조 없이 서로 통신 가능 (CLAUDE.md §2 의존 방향).
    /// </summary>
    public interface IRunEventBus
    {
        /// <summary>일반 적 자기 막타 — (sourceSkillId, enemyId).</summary>
        event Action<int, int> KillRecorded;

        /// <summary>보스 처치 (D13: 모든 파티원 카운트) — (bossId).</summary>
        event Action<int> BossDefeatRecorded;

        /// <summary>자기 사망 — (attackerEnemyId).</summary>
        event Action<int> DeathRecorded;

        /// <summary>자기 캐릭터가 존(퀘스트/영역) 진입 — (zoneId).</summary>
        event Action<int> ZoneVisited;

        /// <summary>런 종료 — (isCleared). GameClear / GameOver 에서 1회 발화.</summary>
        event Action<bool> RunEnded;

        void RaiseKill(int sourceSkillId, int enemyId);
        void RaiseBossDefeat(int bossId);
        void RaiseDeath(int attackerEnemyId);
        void RaiseZoneVisited(int zoneId);
        void RaiseRunEnded(bool isCleared);
    }
}
