using UnityEngine;

namespace SwDreams.Domain.Interfaces
{
    /// <summary>
    /// 보스 공격 패턴 인터페이스 (Strategy Pattern).
    /// 기존 IEnemyMovementStrategy와 동일한 OCP 패턴.
    ///
    /// 새 공격 추가 시 이 인터페이스 구현체만 추가하면 됨.
    /// BossPhaseManager가 페이즈별로 패턴 세트를 교체.
    ///
    /// 호스트에서만 Execute() 호출. 비주얼 동기화는 각 구현체에서 RPC 처리.
    /// </summary>
    public interface IBossAttackPattern
    {
        /// <summary>이 패턴의 쿨다운 (초).</summary>
        float Cooldown { get; }

        /// <summary>쿨다운 경과 여부 확인.</summary>
        bool CanExecute(float timeSinceLastUse);

        /// <summary>
        /// 공격 실행. 호스트에서만 호출.
        /// </summary>
        /// <param name="bossTransform">보스 위치/방향</param>
        /// <param name="target">현재 추적 대상 (가장 가까운 플레이어)</param>
        void Execute(Transform bossTransform, Transform target);
    }
}