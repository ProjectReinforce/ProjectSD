using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Enemy.Adapter.Data;
using SwDreams.Features.Enemy.Adapter;
using Photon.Pun;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Enemy.Adapter
{
    /// <summary>
    /// 적의 이동 처리.
    ///
    /// Dead Reckoning 동기화:
    /// - 호스트 + 클라이언트 모두 동일한 시뮬레이션 실행
    ///   (추적 로직 + 넉백 + Anti-Overlap 겹침 해소)
    /// - 양쪽 시뮬레이션 결과가 거의 동일하므로 네트워크 보정이 거의 개입하지 않음
    /// - 플레이어 위치는 PhotonTransformView로 동기화되어 추적 입력이 일치
    /// - 호스트가 주기적으로 보내는 위치와 오차가 임계값 이상이면 Lerp 보정
    ///
    /// Phase 3 변경:
    /// - EnemyType에 따라 이동 전략 자동 선택
    ///   Chaser, Runner, Tank → ChaseMovement
    ///   Swarm → SwarmMovement
    /// - Swarm 수명 관리 (lifetime 만료 시 ForceReturn)
    /// </summary>
    public class EnemyMovement : MonoBehaviour
    {
        private Enemy enemy;
        private IEnemyMovementStrategy movementStrategy;

        // Swarm 전용: 수명 관리
        private float lifetime;
        private float aliveTimer;
        private bool hasLifetime;

        // [Phase 5 진화: 뇌전역] / 정수(얼음) 슬로우 스택
        // source 별로 독립 관리 → 정수 중첩 시 곱셈 스택 동작.
        // 기존 ApplySlowTemporary(mul, duration) 오버로드는 "legacy" source 로 통합.
        private struct SlowEntry
        {
            public float multiplier;
            public float remaining;
        }
        private readonly Dictionary<string, SlowEntry> slowStack = new Dictionary<string, SlowEntry>();
        private readonly List<string> slowExpireBuffer = new List<string>(4);
        private const string SlowLegacySource = "__legacy__";

        // 넉백 상태
        private Vector2 knockbackVelocity;
        private const float KnockbackDecay = 8f; // 초당 감쇠 속도

        // 네트워크 위치 보정 (클라이언트 전용)
        // Dead Reckoning: 클라이언트도 동일 추적 로직 실행 + 호스트 위치로 보정
        private Vector2 networkTargetPos;
        private bool hasNetworkTarget;
        private bool isFirstNetworkPos = true;  // 첫 수신 시 스냅용

        // 보정 임계값
        private const float CorrectionThreshold = 0.3f;  // 이 이하 오차는 무시 (자체 추적으로 충분)
        private const float SnapThreshold = 3.0f;         // 이 이상이면 즉시 스냅 (워프 방지)
        private const float CorrectionSpeed = 5f;          // Lerp 보정 속도

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
            hasNetworkTarget = false;
            isFirstNetworkPos = true;
            knockbackVelocity = Vector2.zero;
            slowStack.Clear();

            // SO에서 겹침 해소 여부 반영 (Swarm 등은 false).
            resolveEnemyOverlap = enemyRef.ResolveOverlap;

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

            if (SwDreams.Shared.Managers.GameManager.Instance != null &&
                SwDreams.Shared.Managers.GameManager.Instance.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.Playing &&
                SwDreams.Shared.Managers.GameManager.Instance.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.BossFight)
                return;

            // Swarm 수명 체크 (호스트만 — ForceReturn은 호스트 권한)
            if (hasLifetime && PhotonNetwork.IsMasterClient)
            {
                aliveTimer += Time.deltaTime;
                if (aliveTimer >= lifetime)
                {
                    enemy.ForceReturn();
                    return;
                }
            }

            // ===== 슬로우 스택 (호스트 + 클라이언트 공통) =====
            // 각 source 의 배율을 곱해 최종 배율 산출. 만료된 엔트리 제거.
            float slowMul = TickSlowStack(Time.deltaTime);

            float moveSpeed = enemy.MoveSpeed * slowMul;
            Transform target = FindClosestPlayer();

            // ===== 이동 전략 실행 (호스트 + 클라이언트 공통) =====
            // Dead Reckoning: 클라이언트도 동일한 추적 로직을 실행하여
            // 프레임 단위로 부드러운 이동을 보장.
            if (movementStrategy != null)
            {
                if (target != null || movementStrategy is SwarmMovement)
                {
                    movementStrategy.UpdateMovement(transform, target, moveSpeed);
                }
            }

            // ===== 넉백 적용 (호스트 + 클라이언트 공통) =====
            // Dead Reckoning: 양쪽에서 동일한 넉백을 적용해야 위치 오차 최소화
            if (knockbackVelocity.sqrMagnitude > 0.01f)
            {
                transform.position += (Vector3)(knockbackVelocity * Time.deltaTime);
                knockbackVelocity = Vector2.Lerp(knockbackVelocity, Vector2.zero,
                    KnockbackDecay * Time.deltaTime);
            }
            else
            {
                knockbackVelocity = Vector2.zero;
            }

            // ===== 클라이언트: 호스트 위치와의 오차 보정 =====
            if (!PhotonNetwork.IsMasterClient && hasNetworkTarget)
            {
                ApplyNetworkCorrection();
            }
        }

        private void LateUpdate()
        {
            if (!resolveEnemyOverlap) return;
            if (enemy == null || !enemy.IsAlive) return;

            if (SwDreams.Shared.Managers.GameManager.Instance != null &&
                SwDreams.Shared.Managers.GameManager.Instance.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.Playing &&
                SwDreams.Shared.Managers.GameManager.Instance.CurrentState != SwDreams.Shared.Managers.GameManager.GameState.BossFight)
                return;

            // 호스트 + 클라이언트 모두 실행.
            // Dead Reckoning 핵심: 양쪽이 동일한 시뮬레이션(추적 + 넉백 + 겹침 해소)을
            // 실행해야 위치 오차가 최소화되어 네트워크 보정이 거의 개입하지 않음.
            ResolveEnemyOverlap();
        }

        private IEnemyMovementStrategy CreateStrategy(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Swarm:
                    return new SwarmMovement();

                case EnemyType.Ranged:
                    return enemy.RangedBehaviorType == RangedBehavior.Stationary
                        ? (IEnemyMovementStrategy)new StationaryMovement()
                        : new KiteMovement(enemy.AttackRange);

                case EnemyType.Chaser:
                case EnemyType.Runner:
                case EnemyType.Tank:
                default:
                    return new ChaseMovement();
            }
        }

        /// <summary>
        /// 일시적 이동속도 감소 (source 별 스택). 정수 중첩 시 곱셈 스택 동작.
        /// 같은 source 재호출 시 파라미터 갱신(최신 값 사용, 지속시간 리셋).
        /// source 가 null/빈 문자열이면 "__legacy__" 단일 슬롯으로 통합 (기존 호출부 호환).
        /// </summary>
        public void ApplySlowTemporary(string source, float multiplier, float duration)
        {
            if (multiplier <= 0f || duration <= 0f) return;
            string key = string.IsNullOrEmpty(source) ? SlowLegacySource : source;
            slowStack[key] = new SlowEntry { multiplier = multiplier, remaining = duration };
        }

        /// <summary>
        /// 하위 호환 오버로드. 기존 AreaZone 등의 호출부(source 정보 없음) 유지용.
        /// 내부적으로 "__legacy__" source 로 위임.
        /// </summary>
        public void ApplySlowTemporary(float multiplier, float duration)
            => ApplySlowTemporary(null, multiplier, duration);

        /// <summary>
        /// 매 프레임 스택 타이머 감소 + 최종 배율 계산.
        /// 빈 스택이면 1f (감속 없음).
        /// </summary>
        private float TickSlowStack(float deltaTime)
        {
            if (slowStack.Count == 0) return 1f;

            // foreach 중 Dictionary 수정 금지 → 키 목록으로 순회.
            slowExpireBuffer.Clear();
            var keys = new string[slowStack.Count];
            slowStack.Keys.CopyTo(keys, 0);

            float result = 1f;
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                var entry = slowStack[key];
                entry.remaining -= deltaTime;
                if (entry.remaining <= 0f)
                {
                    slowExpireBuffer.Add(key);
                    continue;
                }
                slowStack[key] = entry;
                result *= entry.multiplier;
            }
            for (int i = 0; i < slowExpireBuffer.Count; i++)
                slowStack.Remove(slowExpireBuffer[i]);

            return result;
        }

        /// <summary>
        /// 넉백 충격량 적용. Enemy.ApplyKnockback()에서 호출.
        /// 방향 * 힘이 합산되어 감쇠됨.
        /// </summary>
        public void ApplyKnockback(Vector2 impulse)
        {
            knockbackVelocity += impulse;
        }

        /// <summary>
        /// 호스트 위치 수신. SpawnManager.RPC_SyncEnemyPositions에서 호출.
        /// 클라이언트에서만 의미 있음 — Dead Reckoning 보정 기준점으로 사용.
        /// </summary>
        public void SetNetworkPosition(Vector2 pos)
        {
            networkTargetPos = pos;
            hasNetworkTarget = true;

            // 첫 수신 시 즉시 스냅 (스폰 직후 적이 멈춰보이는 현상 방지)
            if (isFirstNetworkPos)
            {
                isFirstNetworkPos = false;
                transform.position = pos;
            }
        }

        /// <summary>
        /// 클라이언트 Dead Reckoning 보정.
        /// 자체 추적 로직으로 이동한 위치와 호스트 실제 위치의 오차를 부드럽게 보정.
        /// 
        /// - 오차 < CorrectionThreshold: 무시 (자체 추적으로 충분히 정확)
        /// - 오차 > SnapThreshold: 즉시 스냅 (텔레포트/넉백 등 큰 위치 변화)
        /// - 중간: Lerp로 부드럽게 수렴
        /// </summary>
        private void ApplyNetworkCorrection()
        {
            float distance = Vector2.Distance(transform.position, networkTargetPos);

            // 오차가 작으면 무시 (자체 추적으로 충분히 정확)
            if (distance < CorrectionThreshold) return;

            // 오차가 너무 크면 즉시 스냅 (넉백, 풀 등으로 큰 위치 변화 발생)
            if (distance > SnapThreshold)
            {
                transform.position = networkTargetPos;
                return;
            }

            // 중간 오차: 부드럽게 보정
            transform.position = Vector2.Lerp(
                transform.position,
                networkTargetPos,
                CorrectionSpeed * Time.deltaTime);
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

            int overlapCount = enemyCollider.Overlap(overlapFilter, overlapResults);

            if (overlapCount >= overlapResults.Length)
            {
                System.Array.Resize(ref overlapResults, overlapResults.Length * 2);
                overlapCount = enemyCollider.Overlap(overlapFilter, overlapResults);
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
                var damageable = player.GetComponent<SwDreams.Shared.Domain.Interfaces.IDamageable>();
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