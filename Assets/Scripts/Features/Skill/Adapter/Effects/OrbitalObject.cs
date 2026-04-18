using System.Collections.Generic;
using SwDreams.Features.Boss.Adapter;
using SwDreams.Features.Skill.Adapter;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 회전 오브젝트 개체. OrbitalSpawner가 생성.
    ///
    /// [Step 4-5] 자체 궤도 위치 관리.
    /// [Step 4-6] hitCooldown → HashSet. 같은 적 중복 판정 방지, 다른 적은 즉시 판정.
    ///
    /// 동작:
    /// 1. 플레이어 주변 원형 궤도 회전 (자체 Update에서 위치 계산)
    /// 2. OverlapCircleAll로 범위 내 적 판정 + 데미지 + 넉백 (호스트만)
    /// 3. 이미 맞은 적은 HashSet으로 추적하여 중복 판정 방지
    /// 4. duration 후 풀 반환
    ///
    /// 프리팹: SpriteRenderer + OrbitalObject
    /// </summary>
    public class OrbitalObject : MonoBehaviour, IPoolable
    {
        // 런타임 설정 (Initialize에서 주입)
        private int damage;
        private float knockbackForce;
        private float duration;
        private float aliveTime;
        private bool isActive;

        // 궤도 파라미터 (자체 관리)
        private Transform playerTransform;
        private float baseAngle;
        private float orbitRadius;
        private float rotationSpeed;
        private float currentAngle;

        // TwoPhase: 1바퀴 회전 완료 시 Phase2 트리거
        private bool fireOnOneRotation;
        private float angleTraveled;
        private bool phase1Fired;

        // 판정 반경 (인스펙터에서 조정 가능)
        [SerializeField] private float hitRadius = 0.3f;

        // 소유자 판별 (C안 데미지 요청)
        private bool isLocalPlayerOwned;
        private int ownerActorNumber = -1;

        // 이미 맞은 적 추적 (1회전 동안 중복 방지)
        private readonly HashSet<int> hitEnemyIds = new HashSet<int>();

        // TwoPhase 완료 콜백 (Executor.NotifyPhase1Complete). 현재 위치/바깥 방향 전달.
        private System.Action<Vector2, Vector2> onComplete;

        /// <summary>
        /// TwoPhase 완료 콜백 설정. OrbitalSpawner에서 호출.
        /// 1바퀴 회전 완료 시 Executor에 Phase1 완료를 알림(자기 위치 + 바깥 방향 포함).
        /// </summary>
        public void SetOnComplete(System.Action<Vector2, Vector2> callback)
        {
            onComplete = callback;
        }

        /// <summary>
        /// OrbitalSpawner에서 호출. 궤도 파라미터 포함.
        /// fireOnOneRotation=true이면 duration 대신 1바퀴 완주 후 콜백 + 소멸(TwoPhase 전용).
        /// </summary>
        public void Initialize(int damage, float knockbackForce, float duration,
            Transform playerTransform, float baseAngle, float orbitRadius, float rotationSpeed,
            Transform ownerTransform, bool fireOnOneRotation = false)
        {
            this.damage = damage;
            this.knockbackForce = knockbackForce;
            this.duration = duration;
            this.playerTransform = playerTransform;
            this.baseAngle = baseAngle;
            this.orbitRadius = orbitRadius;
            this.rotationSpeed = rotationSpeed;

            // 소유자 판별
            isLocalPlayerOwned = false;
            ownerActorNumber = -1;
            if (ownerTransform != null)
            {
                var pv = ownerTransform.GetComponent<PhotonView>();
                if (pv != null)
                {
                    isLocalPlayerOwned = pv.IsMine;
                    ownerActorNumber = pv.Owner != null ? pv.Owner.ActorNumber : -1;
                }
            }

            currentAngle = baseAngle;
            aliveTime = 0f;
            angleTraveled = 0f;
            phase1Fired = false;
            this.fireOnOneRotation = fireOnOneRotation;
            hitEnemyIds.Clear();
            isActive = true;

            UpdateOrbitalPosition();
        }

        private void Update()
        {
            if (!isActive) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            // 수명 체크 — TwoPhase 모드는 duration 대신 1바퀴 완주로 종료하므로 스킵
            aliveTime += Time.deltaTime;
            if (!fireOnOneRotation && aliveTime >= duration)
            {
                ReturnToPool();
                return;
            }

            // 자체 궤도 관리
            if (playerTransform != null)
            {
                float delta = rotationSpeed * Time.deltaTime;
                currentAngle += delta;
                if (currentAngle >= 360f) currentAngle -= 360f;
                else if (currentAngle < 0f) currentAngle += 360f;

                // TwoPhase: 1바퀴 누적되면 Phase2로 전환
                if (fireOnOneRotation && !phase1Fired)
                {
                    angleTraveled += Mathf.Abs(delta);
                    if (angleTraveled >= 360f)
                    {
                        phase1Fired = true;
                        TriggerPhase1Complete();
                        return;
                    }
                }

                UpdateOrbitalPosition();
            }

            // 자기 궤도 무기가 아니고 호스트도 아니면 판정 스킵
            if (!isLocalPlayerOwned && !PhotonNetwork.IsMasterClient) return;

            var hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                int enemyInstanceId = hit.gameObject.GetInstanceID();
                if (hitEnemyIds.Contains(enemyInstanceId)) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                // Enemy 컴포넌트는 넉백/ShowHitVisuals/EnemyId용 (Boss는 null)
                var enemy = hit.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();

                if (isLocalPlayerOwned)
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                        damageable.TakeDamage(damage);
                        if (knockbackForce > 0f && enemy != null)
                            enemy.ApplyKnockback(transform.position, knockbackForce);
                    }
                    else if (enemy != null)
                    {
                        enemy.ShowHitVisuals(damage);
                        if (knockbackForce > 0f)
                            enemy.ApplyKnockback(transform.position, knockbackForce);
                        SwDreams.Shared.Managers.SpawnManager.Instance?.RequestDamage(
                            enemy.EnemyId, damage, ownerActorNumber);
                        if (knockbackForce > 0f)
                            SwDreams.Shared.Managers.SpawnManager.Instance?.RequestKnockback(
                                enemy.EnemyId, transform.position, knockbackForce);
                    }
                    else
                    {
                        // Boss: PhotonView RPC로 직접 데미지 요청
                        var boss = hit.GetComponent<SwDreams.Features.Boss.Adapter.Boss>();
                        if (boss != null)
                            boss.RequestDamageFromClient(damage);
                    }
                }
                else
                {
                    // 남의 궤도 무기 (호스트에서만 여기 도달): 직접 데미지
                    if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                    damageable.TakeDamage(damage);
                    if (knockbackForce > 0f && enemy != null)
                        enemy.ApplyKnockback(transform.position, knockbackForce);
                }

                hitEnemyIds.Add(enemyInstanceId);
            }
        }

        private void UpdateOrbitalPosition()
        {
            if (playerTransform == null) return;

            float rad = currentAngle * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
            transform.position = (Vector2)playerTransform.position + offset;

            float rotZ = currentAngle + 90f;
            transform.rotation = Quaternion.Euler(0, 0, rotZ);
        }

        /// <summary>
        /// TwoPhase 1바퀴 완주 시 호출. 현재 궤도 위치 + 바깥 방향을 Executor에 전달 후 풀 반환.
        /// duration 만료로 인한 소멸(비 TwoPhase)에는 호출되지 않음.
        /// </summary>
        private void TriggerPhase1Complete()
        {
            isActive = false;

            Vector2 currentPosition = transform.position;
            Vector2 outward = Vector2.right;
            if (playerTransform != null)
            {
                Vector2 delta = currentPosition - (Vector2)playerTransform.position;
                if (delta.sqrMagnitude > 0.0001f)
                    outward = delta.normalized;
            }

            onComplete?.Invoke(currentPosition, outward);
            onComplete = null;

            PoolManager.Instance?.Return(gameObject);
        }

        private void ReturnToPool()
        {
            isActive = false;
            // TwoPhase 완료 경로가 아니면 onComplete 미호출 (중도 소멸/duration 만료)
            onComplete = null;
            PoolManager.Instance?.Return(gameObject);
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
            hitEnemyIds.Clear();
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            isLocalPlayerOwned = false;
            ownerActorNumber = -1;
            onComplete = null;
            playerTransform = null;
            hitEnemyIds.Clear();
            gameObject.SetActive(false);
        }
    }
}