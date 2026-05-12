using System;

namespace SwDreams.Shared.Domain.Events
{
    /// <summary>
    /// IRunEventBus 의 단일 인스턴스 구현.
    /// 정적 Instance — 모든 Feature 가 동일 버스를 참조.
    /// 순수 C# (UnityEngine/Photon import 없음).
    /// </summary>
    public class RunEventBus : IRunEventBus
    {
        private static IRunEventBus _instance;
        public static IRunEventBus Instance => _instance ??= new RunEventBus();

        public event Action<int, int> KillRecorded;
        public event Action<int> BossDefeatRecorded;
        public event Action<int> DeathRecorded;
        public event Action<int> ZoneVisited;
        public event Action<bool> RunEnded;

        public void RaiseKill(int sourceSkillId, int enemyId)
            => KillRecorded?.Invoke(sourceSkillId, enemyId);

        public void RaiseBossDefeat(int bossId)
            => BossDefeatRecorded?.Invoke(bossId);

        public void RaiseDeath(int attackerEnemyId)
            => DeathRecorded?.Invoke(attackerEnemyId);

        public void RaiseZoneVisited(int zoneId)
            => ZoneVisited?.Invoke(zoneId);

        public void RaiseRunEnded(bool isCleared)
            => RunEnded?.Invoke(isCleared);

        /// <summary>
        /// 테스트 / 강제 리셋 용도. 모든 핸들러 detach.
        /// 메모리 진입점 잔존 디버깅 시 사용.
        /// </summary>
        public static void ResetForTesting()
        {
            _instance = new RunEventBus();
        }
    }
}
