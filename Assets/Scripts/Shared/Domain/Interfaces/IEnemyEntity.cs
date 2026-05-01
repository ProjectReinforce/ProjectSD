namespace SwDreams.Shared.Domain.Interfaces
{
    /// <summary>
    /// 적 계열(일반 적·엘리트·보스) 공통 마커.
    ///
    /// 도입 배경:
    /// EnemyMovement 가 가진 일반 적의 겹침 분리(ResolveEnemyOverlap) 로직이
    /// EnemyMovement 컴포넌트만 검사해 보스(EnemyMovement 없음)를 인식하지 못한다.
    /// 그렇다고 EnemyMovement 가 Boss 클래스를 직접 import 하면 Feature 간 직접 참조가 되어
    /// 아키텍처 규칙(§ 2 — Feature 의 Adapter 끼리 직접 참조 금지)에 위배된다.
    /// 공통 마커를 Shared/Domain/Interfaces 에 두고 양쪽이 구현하면 의존 방향이 깨끗해진다.
    ///
    /// 분리 검사에 필요한 최소 정보(IsAlive)만 노출. IDamageable 과 분리한 이유는
    /// IDamageable 은 Player 도 구현하기 때문 — 적-플레이어 분리는 게임 의도가 아님.
    /// </summary>
    public interface IEnemyEntity
    {
        bool IsAlive { get; }
    }
}
