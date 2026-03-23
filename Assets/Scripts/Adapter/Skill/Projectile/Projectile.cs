using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Domain.ValueObjects;
using SwDreams.Adapter.Manager;
using SwDreams.Adapter.Entity;
using SwDreams.Adapter.Skill.TriggerEffects;
using SwDreams.Adapter.Skill.Trajectories;

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

        /// <summary>궤적 행동 부착. ProjectileEffect에서 스폰 후 호출.</summary>
        public void SetTrajectory(ITrajectoryBehavior behavior)
        {
            trajectoryBehavior = behavior;
            behavior?.Initialize(this);
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

        /// <summary>ProjectileEffect에서 스폰 후 호출. TriggerSystem 연결.</summary>
        public void SetTriggerSystem(SkillTriggerSystem system, Transform owner)
        {
            triggerSystem = system;
            ownerTransform = owner;
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
            
            if (PhotonNetwork.IsMasterClient)
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.TakeDamage(damage);

                    if (knockbackForce > 0f)
                    {
                        var enemy = other.GetComponent<Enemy>();
                        if (enemy != null)
                            enemy.ApplyKnockback(transform.position, knockbackForce);
                    }

                    // [Step 3-5] OnHit 트리거 발동
                    FireOnHit(other.transform);
                }
            }

            OnHitEnemy(other);
        }

        /// <summary>
        /// 적 히트 시 후처리.
        /// Behavior의 Penetrates가 true면 관통. 아니면 풀 반환.
        /// 기존 서브클래스는 오버라이드로 계속 동작.
        /// </summary>
        protected virtual void OnHitEnemy(Collider2D other)
        {
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
            if (triggerSystem == null || !triggerSystem.HasTrigger(TriggerType.OnHit)) return;

            triggerSystem.FireTrigger(TriggerType.OnHit, new TriggerContext
            {
                position = transform.position,
                direction = direction,
                target = target,
                damage = damage,
                owner = ownerTransform
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
                owner = ownerTransform
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
            trajectoryBehavior?.Reset();
            trajectoryBehavior = null;
        }
    }
}