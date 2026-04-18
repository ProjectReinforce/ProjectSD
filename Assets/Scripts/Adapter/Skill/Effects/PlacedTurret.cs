using UnityEngine;
using System.Collections;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Adapter.Manager;
using SwDreams.Shared.Managers;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 자동포탑 오브젝트. PlacedEffect.Execute()에서 생성.
    ///
    /// 동작:
    /// 1. 플레이어 위치에 설치
    /// 2. attackRange 내 가장 가까운 적을 탐색
    /// 3. attackCooldown 간격으로 즉발 공격 + 레이저 라인 비주얼
    /// 4. duration 후 풀 반환
    ///
    /// 비주얼: LineRenderer로 공격 라인 표시 (모든 클라이언트).
    /// 데미지: 호스트에서만 판정.
    ///
    /// 프리팹: SpriteRenderer + PlacedTurret
    /// (LineRenderer는 런타임에 자동 추가)
    /// </summary>
    public class PlacedTurret : MonoBehaviour, IPoolable
    {
        // 런타임 설정
        private int damage;
        private float attackRange;
        private float attackCooldown;
        private float duration;
        private bool alwaysCritical;
        private float critDamageMultiplier;

        // 타이머
        private float aliveTime;
        private float attackTimer;
        private bool isActive;

        // 비주얼
        private SpriteRenderer spriteRenderer;

        // 공격 비주얼 (LineRenderer)
        private LineRenderer attackLine;
        private const float ATTACK_LINE_DURATION = 0.1f;

        // 공격 대상 캐시 (매 프레임 탐색 방지)
        private Transform currentTarget;
        private float targetSearchTimer;
        private const float TARGET_SEARCH_INTERVAL = 0.2f;

        // 소유자 판별 (C안 데미지 요청)
        private bool isLocalPlayerOwned;
        private int ownerActorNumber = -1;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            SetupAttackLine();
        }

        /// <summary>
        /// 공격 라인 비주얼 초기화. LineRenderer를 런타임에 추가.
        /// </summary>
        private void SetupAttackLine()
        {
            attackLine = GetComponent<LineRenderer>();
            if (attackLine == null)
                attackLine = gameObject.AddComponent<LineRenderer>();

            attackLine.positionCount = 2;
            attackLine.startWidth = 0.06f;
            attackLine.endWidth = 0.02f;
            attackLine.material = new Material(Shader.Find("Sprites/Default"));
            attackLine.startColor = new Color(1f, 1f, 0.3f, 1f); // 밝은 노랑
            attackLine.endColor = new Color(1f, 0.5f, 0f, 0.5f);  // 반투명 주황
            attackLine.sortingOrder = 10;
            attackLine.enabled = false;
        }

        /// <summary>
        /// PlacedEffect에서 스폰 후 호출.
        /// </summary>
        public void Initialize(Vector2 position, int damage, float attackRange,
            float attackCooldown, float duration, bool alwaysCritical,
            float critDamageMultiplier, Transform ownerTransform)
        {
            transform.position = position;
            this.damage = damage;
            this.attackRange = attackRange;
            this.attackCooldown = Mathf.Max(0.1f, attackCooldown);
            this.duration = duration;
            this.alwaysCritical = alwaysCritical;
            this.critDamageMultiplier = critDamageMultiplier;

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

            aliveTime = 0f;
            attackTimer = 0f;
            targetSearchTimer = 0f;
            currentTarget = null;
            isActive = true;

            Debug.Log($"[PlacedTurret] 설치 — pos:{position}, range:{attackRange}, " +
                      $"cd:{attackCooldown}, duration:{duration}, crit:{alwaysCritical}");
        }

        private void Update()
        {
            if (!isActive) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            // 수명 체크
            aliveTime += Time.deltaTime;
            if (aliveTime >= duration)
            {
                ReturnToPool();
                return;
            }

            // === 모든 클라이언트: 대상 탐색 + 방향 전환 (비주얼) ===
            targetSearchTimer += Time.deltaTime;
            if (targetSearchTimer >= TARGET_SEARCH_INTERVAL)
            {
                targetSearchTimer = 0f;
                FindTarget();
            }

            if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
            {
                Vector2 dir = currentTarget.position - transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // === 공격 타이밍 ===
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackCooldown && currentTarget != null
                && currentTarget.gameObject.activeInHierarchy)
            {
                float dist = Vector2.Distance(transform.position, currentTarget.position);
                if (dist <= attackRange)
                {
                    attackTimer -= attackCooldown;

                    int finalDamage = damage;
                    if (alwaysCritical)
                        finalDamage = Mathf.RoundToInt(damage * critDamageMultiplier);

                    // 비주얼: 모든 클라이언트에서 표시
                    ShowAttackLine(currentTarget.position);

                    var enemy = currentTarget.GetComponent<Enemy>();

                    if (isLocalPlayerOwned)
                    {
                        if (PhotonNetwork.IsMasterClient)
                        {
                            // 호스트의 자기 터렛: 직접 데미지
                            if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                            var damageable = currentTarget.GetComponent<IDamageable>();
                            if (damageable != null && damageable.IsAlive)
                                damageable.TakeDamage(finalDamage);
                        }
                        else
                        {
                            // 클라이언트의 자기 터렛: 비주얼 + 데미지 요청
                            if (enemy != null && enemy.IsAlive)
                            {
                                enemy.ShowHitVisuals(finalDamage);
                                SpawnManager.Instance?.RequestDamage(
                                    enemy.EnemyId, finalDamage, ownerActorNumber);
                            }
                            else
                            {
                                // Boss: PhotonView RPC로 직접 데미지 요청
                                var boss = currentTarget.GetComponent<Boss>();
                                if (boss != null)
                                    boss.RequestDamageFromClient(finalDamage);
                            }
                        }
                    }
                    else if (PhotonNetwork.IsMasterClient)
                    {
                        // 남의 터렛 (호스트에서만): 직접 데미지
                        if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                        var damageable = currentTarget.GetComponent<IDamageable>();
                        if (damageable != null && damageable.IsAlive)
                            damageable.TakeDamage(finalDamage);
                    }
                }
            }
        }

        /// <summary>
        /// 공격 라인 비주얼 표시. 잠시 후 자동 소멸.
        /// </summary>
        private void ShowAttackLine(Vector3 targetPos)
        {
            if (attackLine == null) return;

            attackLine.SetPosition(0, transform.position);
            attackLine.SetPosition(1, targetPos);
            attackLine.enabled = true;

            StopCoroutine(nameof(HideAttackLineCoroutine));
            StartCoroutine(nameof(HideAttackLineCoroutine));
        }

        private IEnumerator HideAttackLineCoroutine()
        {
            yield return new WaitForSeconds(ATTACK_LINE_DURATION);
            if (attackLine != null)
                attackLine.enabled = false;
        }

        private void FindTarget()
        {
            currentTarget = null;
            float minDist = float.MaxValue;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in enemies)
            {
                if (!enemy.activeInHierarchy) continue;

                float dist = Vector2.Distance(transform.position, enemy.transform.position);
                if (dist <= attackRange && dist < minDist)
                {
                    minDist = dist;
                    currentTarget = enemy.transform;
                }
            }

            // 초기 디버그용 (확인 후 제거 가능)
            if (currentTarget != null && aliveTime < 2f)
                Debug.Log($"[PlacedTurret] 대상 발견: {currentTarget.name}, 거리:{minDist:F1}");
        }

        private void ReturnToPool()
        {
            isActive = false;
            currentTarget = null;
            PoolManager.Instance?.Return(gameObject);
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
            attackTimer = 0f;
            targetSearchTimer = 0f;
            currentTarget = null;
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            isLocalPlayerOwned = false;
            ownerActorNumber = -1;
            currentTarget = null;
            StopAllCoroutines();
            if (attackLine != null)
                attackLine.enabled = false;
            gameObject.SetActive(false);
        }
    }
}