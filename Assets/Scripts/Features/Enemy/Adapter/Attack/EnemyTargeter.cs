using UnityEngine;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Enemy.Adapter.Attack
{
    /// <summary>
    /// 적의 타겟 선택 담당 컴포넌트.
    /// 현재 전략: 살아있는 가장 가까운 플레이어.
    /// 추후 변형(무작위 타겟, 가장 먼 플레이어, 태그 기반 우선순위 등) 추가 시 이 컴포넌트만 교체.
    ///
    /// 성능 주의:
    /// - 매 프레임 FindGameObjectsWithTag 호출 중. PlayerRegistry 공통화는 별도 티켓.
    /// </summary>
    public class EnemyTargeter : MonoBehaviour
    {
        public Transform FindClosestAlivePlayer()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            Transform closest = null;
            float minSqr = float.MaxValue;
            Vector2 here = transform.position;

            foreach (var p in players)
            {
                if (p == null || !p.activeInHierarchy) continue;

                var damageable = p.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsAlive) continue;

                float sqr = ((Vector2)p.transform.position - here).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    closest = p.transform;
                }
            }

            return closest;
        }
    }
}
