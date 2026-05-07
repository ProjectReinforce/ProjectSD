using UnityEngine;
using SwDreams.Features.Boss.Adapter;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using SwDreams.Features.Skill.Domain.ValueObjects;
using System.Collections;
using Photon.Pun;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Skill.Adapter
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
        // R9: alwaysCritical 강제 치명타는 critChance=1f 로 통일 (코드 경로 단일화).
        // 일반 치명타도 critChance > 0 이면 적중마다 새 판정 (적별 재판정).
        private float critChance;
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

        // 트리거 시스템 (로컬 소유자의 SkillTriggerSystem — 정수/무기 등 runtime 효과 실행)
        private SkillTriggerSystem triggerSystem;
        private Transform ownerTransformRef;

        // 인-런 통계 (B-1a — run-statistics.md §4) — PlacedSpawner 가 SetSourceSkillId 로 주입.
        private int sourceSkillId;

        /// <summary>발사 스킬 ID 주입. PlacedSpawner에서 호출 (B-1a).</summary>
        public void SetSourceSkillId(int skillId)
        {
            this.sourceSkillId = skillId;
        }

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
        /// alwaysCritical=true 면 critChance=1f 로 통일 강제 치명타.
        /// </summary>
        public void Initialize(Vector2 position, int damage, float attackRange,
            float attackCooldown, float duration, bool alwaysCritical,
            float critChance, float critDamageMultiplier, Transform ownerTransform,
            SkillTriggerSystem triggerSystem = null)
        {
            transform.position = position;
            this.damage = damage;
            this.attackRange = attackRange;
            this.attackCooldown = Mathf.Max(0.1f, attackCooldown);
            this.duration = duration;
            this.critChance = alwaysCritical ? 1f : Mathf.Clamp01(critChance);
            this.critDamageMultiplier = Mathf.Max(1f, critDamageMultiplier);
            this.triggerSystem = triggerSystem;
            this.ownerTransformRef = ownerTransform;

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
                      $"cd:{attackCooldown}, duration:{duration}, critChance:{this.critChance:F2}");
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

                    // R9: 적별 1회 판정. critChance=1f (alwaysCritical) 면 무조건 치명타.
                    int finalDamage = CritJudgment.Roll(damage, critChance, critDamageMultiplier, out bool isCrit);

                    // 비주얼: 모든 클라이언트에서 표시
                    ShowAttackLine(currentTarget.position);

                    var enemy = currentTarget.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();

                    if (isLocalPlayerOwned)
                    {
                        if (PhotonNetwork.IsMasterClient)
                        {
                            // 호스트의 자기 터렛: 직접 데미지 + 트리거 발화
                            if (enemy != null)
                            {
                                enemy.LastDamagerActorNumber = ownerActorNumber;
                                enemy.LastDamagerSkillId = sourceSkillId;
                            }
                            var damageable = currentTarget.GetComponent<IDamageable>();
                            if (damageable != null && damageable.IsAlive)
                            {
                                if (enemy != null) enemy.TakeDamage(finalDamage, isCrit);
                                else damageable.TakeDamage(finalDamage);
                                FireHitTriggers(currentTarget, damageable, finalDamage, isCrit);
                            }
                        }
                        else
                        {
                            // 클라이언트의 자기 터렛: 비주얼 + 데미지 요청
                            if (enemy != null && enemy.IsAlive)
                            {
                                enemy.ShowHitVisuals(finalDamage, isCrit);

                                // B-1a: 비호스트 자기 발사 — 자기 PC 누적.
                                SwDreams.Features.Stats.Adapter.LocalStatsRecorder.Instance?
                                    .AddDamage(sourceSkillId, finalDamage);

                                SpawnManager.Instance?.RequestDamage(
                                    enemy.EnemyId, finalDamage, ownerActorNumber, isCrit);
                                // 로컬 소유자 기준 트리거 발화 — 정수 효과(OnHit)가 로컬에서 동작
                                var damageable = currentTarget.GetComponent<IDamageable>();
                                FireHitTriggers(currentTarget, damageable, finalDamage, isCrit);
                            }
                            else
                            {
                                // Boss: PhotonView RPC로 직접 데미지 요청
                                var boss = currentTarget.GetComponent<SwDreams.Features.Boss.Adapter.Boss>();
                                if (boss != null)
                                {
                                    boss.RequestDamageFromClient(finalDamage, isCrit);
                                    var damageable = currentTarget.GetComponent<IDamageable>();
                                    FireHitTriggers(currentTarget, damageable, finalDamage, isCrit);
                                }
                            }
                        }
                    }
                    else if (PhotonNetwork.IsMasterClient)
                    {
                        // 남의 터렛 (호스트에서만): 직접 데미지
                        // triggerSystem 은 null (원격 플레이어의 것은 로컬에 없음) → FireTrigger 생략.
                        if (enemy != null) enemy.LastDamagerActorNumber = ownerActorNumber;
                        var damageable = currentTarget.GetComponent<IDamageable>();
                        if (damageable != null && damageable.IsAlive)
                        {
                            if (enemy != null) enemy.TakeDamage(finalDamage, isCrit);
                            else damageable.TakeDamage(finalDamage);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// OnHit/OnKill 트리거 발화. 로컬 소유자 triggerSystem 에만 실행.
        /// 정수/무기 등 runtime 효과(OnHit), 처치 보상(OnKill) 실행 지점.
        /// </summary>
        private void FireHitTriggers(Transform target, IDamageable damageable, int dmg, bool isCritFromBody = false)
        {
            if (triggerSystem == null || target == null) return;

            var ctx = new TriggerContext
            {
                target = target,
                position = target.position,
                damage = dmg,
                owner = ownerTransformRef,
                critChance = critChance,
                critDamageMultiplier = critDamageMultiplier,
                attackerActorNumber = ownerActorNumber,
                sourceSkillId = sourceSkillId
            };
            triggerSystem.FireTrigger(TriggerType.OnHit, ctx);

            if (damageable != null && !damageable.IsAlive)
                triggerSystem.FireTrigger(TriggerType.OnKill, ctx);
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
            sourceSkillId = 0; // B-1a
            gameObject.SetActive(false);
        }
    }
}