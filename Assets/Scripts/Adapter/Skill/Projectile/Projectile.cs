using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Domain.ValueObjects;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;
using SwDreams.Adapter.Entity;
using SwDreams.Adapter.Skill.TriggerEffects;
using SwDreams.Adapter.Skill.Trajectories;
using SwDreams.Presentation;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 투사체 기본 클래스.
    /// 모든 클라이언트에서 로컬로 이동 + 렌더링.
    /// 히트 판정은 호스트에서만 처리.
    ///
    /// [Step 3-7c] Behavior 조합 모델:
    /// - ITrajectoryBehavior 부착으로 궤적 결정
    /// - TriggerEffect 시스템으로 적중/소멸 효과
    /// - 기존 서브클래스도 MoveStep/OnHitEnemy 오버라이드로 계속 동작 (하위 호환)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour, IPoolable
    {
        // 서브클래스에서 접근 가능하도록 protected
        protected Vector2 direction;
        protected float speed;
        protected int damage;
        protected float lifetime;
        protected float aliveTime;
        protected float knockbackForce;

        // ===== Public 접근자 (Behavior에서 사용) =====
        public Vector2 Direction { get => direction; set => direction = value; }
        public float Speed => speed;
        public int Damage => damage;
        public float Lifetime => lifetime;
        public float AliveTime => aliveTime;

        // ===== Trajectory Behavior (Step 3-7c) =====
        private ITrajectoryBehavior trajectoryBehavior;

        // SO에서 관통 설정 (Trajectory 기본값을 오버라이드)
        private bool? penetratesOverride;

        // ===== 체인 비행 (체인 미사일 진화) =====
        private int chainFlightCount;
        private float chainSearchRadius;
        private System.Collections.Generic.HashSet<int> chainHitIds;

        // ===== 서브 투사체 (분기탄 등) =====
        private GameObject subProjectilePrefab;

        /// <summary>궤적 행동 부착. ProjectileEffect에서 스폰 후 호출.</summary>
        public void SetTrajectory(ITrajectoryBehavior behavior)
        {
            trajectoryBehavior = behavior;
            behavior?.Initialize(this);
        }

        /// <summary>SO의 penetrates 설정. Trajectory 기본값을 오버라이드.</summary>
        public void SetPenetrates(bool value)
        {
            penetratesOverride = value;
        }

        /// <summary>
        /// 체인 비행 설정. ProjectileSpawner에서 호출.
        /// 적중 시 소멸 대신 타겟 교체 + 계속 비행.
        /// HomingTrajectory와 함께 사용.
        /// </summary>
        public void SetChainFlight(int count, float searchRadius)
        {
            chainFlightCount = count;
            chainSearchRadius = searchRadius;
            if (count > 0)
                chainHitIds = new System.Collections.Generic.HashSet<int>();
        }

        /// <summary>서브 투사체 프리팹 설정. SpawnProjectileHandler용.</summary>
        public void SetSubProjectilePrefab(GameObject prefab)
        {
            subProjectilePrefab = prefab;
        }

        /// <summary>Behavior에서 호출. 투사체 강제 풀 반환.</summary>
        public void ForceReturn()
        {
            FireOnExpire();
            ReturnToPool();
        }

        /// <summary>방향에 맞는 회전 적용. Behavior에서 호출.</summary>
        public void SetRotation(Vector2 dir)
        {
            UpdateRotation(dir);
        }

        // ===== Trigger+Effect 시스템 (Step 3-5) =====
        protected SkillTriggerSystem triggerSystem;
        protected Transform ownerTransform;

        // ===== 소유자 판별 (C안 데미지 요청) =====
        // 로컬 플레이어의 투사체인지 여부. 데미지 처리 분기에 사용.
        // - true + IsMasterClient → TakeDamage 직접
        // - true + !IsMasterClient → ShowHitVisuals + RequestDamage
        // - false → ShowHitVisuals만 (이중 데미지 방지)
        protected bool isLocalPlayerOwned;
        protected int ownerActorNumber = -1;

        /// <summary>ProjectileEffect에서 스폰 후 호출. TriggerSystem 연결 + 소유자 판별.</summary>
        public void SetTriggerSystem(SkillTriggerSystem system, Transform owner)
        {
            triggerSystem = system;
            ownerTransform = owner;

            isLocalPlayerOwned = false;
            ownerActorNumber = -1;
            if (owner != null)
            {
                var pv = owner.GetComponent<PhotonView>();
                if (pv != null)
                {
                    isLocalPlayerOwned = pv.IsMine;
                    ownerActorNumber = pv.Owner != null ? pv.Owner.ActorNumber : -1;
                }
            }
        }

        public virtual void Initialize(Vector2 position, Vector2 direction,
            int damage, float speed, float lifetime, float knockbackForce = 0f)
        {
            transform.position = position;
            this.direction = direction.normalized;
            // 방향이 zero면 기본값 (적과 플레이어가 완전히 겹칠 때)
            if (this.direction.sqrMagnitude < 0.01f)
                this.direction = Vector2.right;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            this.knockbackForce = knockbackForce;
            aliveTime = 0f;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        protected virtual void Update()
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            MoveStep();

            aliveTime += Time.deltaTime;

            // Behavior가 lifetime을 직접 관리하면 (부메랑 등) 기본 체크 스킵
            bool checkLifetime = trajectoryBehavior == null || !trajectoryBehavior.OverridesLifetime;
            if (checkLifetime && aliveTime >= lifetime)
            {
                // [Step 3-5] OnExpire 트리거 발동
                FireOnExpire();
                ReturnToPool();
            }
        }

        /// <summary>
        /// 매 프레임 이동 처리.
        /// 궤적 Behavior가 있으면 위임, 없으면 직선 이동.
        /// 기존 서브클래스는 오버라이드로 계속 동작.
        /// </summary>
        protected virtual void MoveStep()
        {
            if (trajectoryBehavior != null)
                trajectoryBehavior.UpdateMovement(this, Time.deltaTime);
            else
                transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;

            // IDamageable로 체크 (Enemy + Boss 모두 지원)
            var damageable = other.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            // Enemy 컴포넌트는 넉백/ShowHitVisuals/킬러추적용 (Boss는 null)
            var enemy = other.GetComponent<Enemy>();

            if (ownerTransform == null)
            {
                // 소유자 미설정 (서브 투사체 등): 기존 호스트 권한 방식
                if (PhotonNetwork.IsMasterClient)
                {
                    if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                    damageable.TakeDamage(damage);
                    if (knockbackForce > 0f && enemy != null)
                        enemy.ApplyKnockback(transform.position, knockbackForce);
                    FireOnHit(other.transform);
                }
            }
            else if (isLocalPlayerOwned)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    // 호스트의 자기 투사체: 직접 데미지 처리
                    if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                    damageable.TakeDamage(damage);
                    if (knockbackForce > 0f && enemy != null)
                        enemy.ApplyKnockback(transform.position, knockbackForce);
                    FireOnHit(other.transform);
                }
                else
                {
                    // 클라이언트의 자기 투사체: 비주얼 즉시 + 호스트에 데미지 요청
                    if (enemy != null)
                    {
                        enemy.ShowHitVisuals(damage);
                        if (knockbackForce > 0f)
                            enemy.ApplyKnockback(transform.position, knockbackForce);
                        SpawnManager.Instance?.RequestDamage(
                            enemy.EnemyId, damage, ownerActorNumber);
                        if (knockbackForce > 0f)
                            SpawnManager.Instance?.RequestKnockback(
                                enemy.EnemyId, transform.position, knockbackForce);
                    }
                    else
                    {
                        // Boss: PhotonView RPC로 직접 데미지 요청
                        var boss = other.GetComponent<Boss>();
                        if (boss != null)
                        {
                            DamagePopup.Spawn(other.transform.position, damage);
                            HitEffect.Spawn(other.transform.position);
                            boss.RequestDamageFromClient(damage);
                        }
                    }
                }
            }
            else
            {
                // 다른 플레이어의 투사체: 비주얼만 표시, 충돌 무시 (관통)
                if (enemy != null)
                    enemy.ShowHitVisuals(damage);
                else
                {
                    DamagePopup.Spawn(other.transform.position, damage);
                    HitEffect.Spawn(other.transform.position);
                }
                return; // OnHitEnemy 스킵 → ReturnToPool 안 됨 → 투사체 유지
            }

            OnHitEnemy(other);
        }

        /// <summary>
        /// 적 히트 시 후처리.
        /// 체인 비행 활성화 시: 소멸 대신 타겟 교체 + 계속 비행.
        /// 그 외: SO penetrates 오버라이드 → Trajectory 기본값.
        /// </summary>
        protected virtual void OnHitEnemy(Collider2D other)
        {
            // ── 체인 비행 처리 ──
            if (chainFlightCount > 0 && trajectoryBehavior is Trajectories.HomingTrajectory homing)
            {
                // 맞은 적 기록
                chainHitIds.Add(other.gameObject.GetInstanceID());
                chainFlightCount--;

                if (chainFlightCount <= 0)
                {
                    ReturnToPool();
                    return;
                }

                // 다음 타겟 탐색 (이미 맞은 적 제외)
                Transform next = homing.FindTargetExcluding(
                    transform.position, chainSearchRadius, chainHitIds);

                if (next != null)
                {
                    homing.SetTarget(next);
                    return; // 소멸하지 않고 계속 비행
                }

                // 타겟 없으면 소멸
                ReturnToPool();
                return;
            }

            // ── 기본 관통/소멸 처리 ──
            // SO 오버라이드가 있으면 우선
            if (penetratesOverride.HasValue)
            {
                if (penetratesOverride.Value) return;
                ReturnToPool();
                return;
            }

            // Trajectory 기본값
            if (trajectoryBehavior != null && trajectoryBehavior.Penetrates)
                return;

            ReturnToPool();
        }

        protected void ReturnToPool()
        {
            PoolManager.Instance?.Return(gameObject);
        }

        /// <summary>
        /// 투사체 방향에 맞게 회전 갱신.
        /// </summary>
        protected void UpdateRotation(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // ===== [Step 3-5] 트리거 헬퍼 =====

        /// <summary>적 적중 시 OnHit 트리거 발동. 호스트에서만 호출됨.</summary>
        protected void FireOnHit(Transform target)
        {
            if (triggerSystem == null) return;
            if (!triggerSystem.HasTrigger(TriggerType.OnHit)) return;

            triggerSystem.FireTrigger(TriggerType.OnHit, new TriggerContext
            {
                position = transform.position,
                direction = direction,
                target = target,
                damage = damage,
                owner = ownerTransform,
                subProjectilePrefab = subProjectilePrefab
            });
        }

        /// <summary>투사체 소멸 시 OnExpire 트리거 발동.</summary>
        protected void FireOnExpire()
        {
            if (triggerSystem == null || !triggerSystem.HasTrigger(TriggerType.OnExpire)) return;
            if (!PhotonNetwork.IsMasterClient) return;

            triggerSystem.FireTrigger(TriggerType.OnExpire, new TriggerContext
            {
                position = transform.position,
                direction = direction,
                damage = damage,
                owner = ownerTransform,
                subProjectilePrefab = subProjectilePrefab
            });
        }

        public virtual void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
        }

        public virtual void OnReturnToPool()
        {
            gameObject.SetActive(false);
            triggerSystem = null;
            ownerTransform = null;
            isLocalPlayerOwned = false;
            ownerActorNumber = -1;
            trajectoryBehavior?.Reset();
            trajectoryBehavior = null;
            penetratesOverride = null;
            chainFlightCount = 0;
            chainHitIds?.Clear();
            subProjectilePrefab = null;
        }
    }
}