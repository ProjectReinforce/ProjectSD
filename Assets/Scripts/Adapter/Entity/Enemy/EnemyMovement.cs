using UnityEngine;
using SwDreams.Data;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 적의 이동 처리.
    /// 모든 클라이언트에서 로컬 실행 (플레이어 위치가 PhotonTransformView로
    /// 동기화되므로 추적 결과가 거의 동일).
    ///
    /// Phase 3 변경:
    /// - EnemyType에 따라 이동 전략 자동 선택
    ///   Chaser, Runner, Tank → ChaseMovement (속도만 다름, EnemyData에서 결정)
    ///   Swarm → SwarmMovement (랜덤 방향 직진)
    /// - Swarm 수명 관리 (lifetime 만료 시 ForceReturn)
    ///
    /// Anti Overlap:
    /// - 기존 이동 전략은 유지
    /// - LateUpdate에서 실제 겹친 적만 살짝 밀어내어 시각적 겹침 완화
    /// - 플레이어 탐색은 프레임 캐시로 중복 Find 호출 감소
    /// </summary>
    public class EnemyMovement : MonoBehaviour
    {
        private Enemy enemy;
        private IEnemyMovementStrategy movementStrategy;

        // Swarm 전용: 수명 관리
        private float lifetime;
        private float aliveTimer;
        private bool hasLifetime;

        // [Phase 5 진화: 뇌전역] 슬로우
        private float slowTimer;
        private float slowMul = 1f;

        [Header("Anti Overlap")]
        [SerializeField] private bool resolveEnemyOverlap = true;
        [SerializeField, Range(0f, 1f)] private float resolveStrength = 0.5f;
        [SerializeField, Min(0.001f)] private float maxResolvePerFrame = 0.05f;
        [SerializeField, Min(4)] private int overlapBufferSize = 8;

        private Collider2D enemyCollider;
        private Collider2D[] overlapResults;
        private ContactFilter2D overlapFilter;

        private static int lastPhysicsSyncFrame = -1;
        private static int cachedPlayerSearchFrame = -1;
        private static GameObject[] cachedPlayers = new GameObject[0];

        private void Awake()
        {
            enemyCollider = GetComponent<Collider2D>();
            if (enemyCollider == null)
                enemyCollider = GetComponentInChildren<Collider2D>();

            overlapResults = new Collider2D[Mathf.Max(4, overlapBufferSize)];
            overlapFilter = new ContactFilter2D
            {
                useLayerMask = false,
                useDepth = false,
                useNormalAngle = false,
                useTriggers = true
            };
        }

        public void Initialize(Enemy enemyRef)
        {
            enemy = enemyRef;
            hasLifetime = false;
            aliveTimer = 0f;

            // EnemyType에 따라 전략 자동 선택
            movementStrategy = CreateStrategy(enemyRef.EnemyType);
        }

        /// <summary>
        /// Swarm 전용: 이동 방향 + 수명 설정.
        /// SpawnManager에서 Swarm 스폰 시 호출.
        /// </summary>
        public void InitializeSwarm(float baseAngle, float spreadDegrees, float swarmLifetime)
        {
            if (movementStrategy is SwarmMovement swarm)
            {
                swarm.SetRandomDirection(baseAngle, spreadDegrees);
            }

            lifetime = swarmLifetime;
            hasLifetime = true;
            aliveTimer = 0f;
        }

        public void SetStrategy(IEnemyMovementStrategy strategy)
        {
            movementStrategy = strategy;
        }

        private void Update()
        {
            if (enemy == null || !enemy.IsAlive) return;

            if (Manager.GameManager.Instance != null &&
                Manager.GameManager.Instance.CurrentState != Manager.GameManager.GameState.Playing)
                return;

            // Swarm 수명 체크
            if (hasLifetime)
            {
                aliveTimer += Time.deltaTime;
                if (aliveTimer >= lifetime)
                {
                    enemy.ForceReturn();
                    return;
                }
            }

            // 슬로우 타이머
            if (slowTimer > 0f)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0f)
                    slowMul = 1f;
            }

            float moveSpeed = enemy.MoveSpeed * slowMul;
            Transform target = FindClosestPlayer();

            // Swarm은 타겟 없어도 이동해야 함
            if (movementStrategy != null)
            {
                if (target != null || movementStrategy is SwarmMovement)
                {
                    movementStrategy.UpdateMovement(transform, target, moveSpeed);
                }
            }
        }

        private void LateUpdate()
        {
            if (!resolveEnemyOverlap) return;
            if (enemy == null || !enemy.IsAlive) return;

            if (Manager.GameManager.Instance != null &&
                Manager.GameManager.Instance.CurrentState != Manager.GameManager.GameState.Playing)
                return;

            ResolveEnemyOverlap();
        }

        private IEnemyMovementStrategy CreateStrategy(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Swarm:
                    return new SwarmMovement();

                case EnemyType.Chaser:
                case EnemyType.Runner:
                case EnemyType.Tank:
                default:
                    return new ChaseMovement();
            }
        }

        /// <summary>
        /// 일시적 이동속도 감소. AreaZone에서 호출.
        /// </summary>
        public void ApplySlowTemporary(float multiplier, float duration)
        {
            slowMul = multiplier;
            slowTimer = duration;
        }

        private void ResolveEnemyOverlap()
        {
            if (enemyCollider == null) return;

            // Update에서 transform.position을 직접 변경하므로,
            // 같은 프레임의 Collider 위치를 물리 쿼리에 반영.
            if (lastPhysicsSyncFrame != Time.frameCount)
            {
                Physics2D.SyncTransforms();
                lastPhysicsSyncFrame = Time.frameCount;
            }

            int overlapCount = enemyCollider.OverlapCollider(overlapFilter, overlapResults);

            if (overlapCount >= overlapResults.Length)
            {
                System.Array.Resize(ref overlapResults, overlapResults.Length * 2);
                overlapCount = enemyCollider.OverlapCollider(overlapFilter, overlapResults);
            }

            if (overlapCount <= 0) return;

            Vector2 totalCorrection = Vector2.zero;
            int correctionCount = 0;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D hit = overlapResults[i];
                if (hit == null || hit == enemyCollider) continue;

                EnemyMovement other = hit.GetComponentInParent<EnemyMovement>();
                if (other == null || other == this) continue;
                if (other.enemy == null || !other.enemy.IsAlive) continue;
                if (!other.enabled || !other.gameObject.activeInHierarchy) continue;

                ColliderDistance2D distance = enemyCollider.Distance(hit);
                if (!distance.isOverlapped) continue;

                // distance.distance는 겹친 경우 음수.
                // normal * distance를 더하면 바깥 방향 보정 벡터가 된다.
                totalCorrection += distance.normal * distance.distance;
                correctionCount++;
            }

            if (correctionCount == 0) return;

            Vector2 correction = (totalCorrection / correctionCount) * resolveStrength;
            correction = Vector2.ClampMagnitude(correction, maxResolvePerFrame);

            if (correction.sqrMagnitude <= 0.000001f) return;

            transform.position += (Vector3)correction;
        }

        private Transform FindClosestPlayer()
        {
            if (cachedPlayerSearchFrame != Time.frameCount)
            {
                cachedPlayers = GameObject.FindGameObjectsWithTag("Player");
                cachedPlayerSearchFrame = Time.frameCount;
            }

            if (cachedPlayers == null || cachedPlayers.Length == 0) return null;

            Transform closest = null;
            float minDist = float.MaxValue;

            foreach (var player in cachedPlayers)
            {
                if (player == null || !player.activeInHierarchy) continue;

                // Phase 6: 사망한 플레이어 제외
                var damageable = player.GetComponent<Domain.Interfaces.IDamageable>();
                if (damageable != null && !damageable.IsAlive) continue;

                float dist = Vector2.Distance(transform.position, player.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = player.transform;
                }
            }

            return closest;
        }
    }
}
