using System;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Data;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;
using SwDreams.Features.Skill.Adapter.TriggerEffects;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 스킬 발사 실행기. 풀링 기반 MonoBehaviour.
    ///
    /// 역할:
    /// 1) FiringMode에 따라 ISkillSpawner.Spawn() 호출 타이밍 제어
    /// 2) applicableStats 필터 적용된 SpawnContext 구성
    /// 3) 발사 기록 (메아리 스킬 대비, 현재는 stub)
    /// 4) 레벨업/사망 시 즉시 정리
    ///
    /// 생명주기:
    ///   풀에서 Get → Begin() → Update 타이머 → 완료 시 풀에 Return
    ///   SimultaneousSpread/Single: Begin()에서 즉시 완료 → 바로 Return
    ///   DelayedBurst: Update에서 타이머 관리 → count 소진 시 Return
    ///   TwoPhase: Phase1 완료 콜백 대기 → Phase2 실행 → Return
    ///
    /// [Phase 7 리팩토링] Step 4 — Executor 패턴
    /// </summary>
    public class SkillExecutor : MonoBehaviour, IPoolable
    {
        // ===== 설정 (Begin에서 주입) =====
        private FiringMode firingMode;
        private ISkillSpawner spawner;
        private SkillData skillData;
        private PlayerStats playerStats;
        private Transform playerTransform;
        private SkillTriggerSystem triggerSystem;
        private Skill sourceSkill; // 발사 기록용

        // ===== DelayedBurst 상태 =====
        private int totalCount;
        private int firedCount;
        private float burstDelay;
        private float delayTimer;

        // ===== TwoPhase 상태 =====
        private ISkillSpawner phase2Spawner;
        private bool waitingForPhase2;
        private Action onPhase1Complete; // 외부에서 Phase1 완료 시 호출

        // Phase1 완료된 orbital들의 (위치, 바깥 방향) 집계. 전원 완료 시 Phase2 일괄 발사.
        private readonly List<(Vector2 position, Vector2 direction)> phase1Results
            = new List<(Vector2, Vector2)>();
        private int phase1ExpectedCount;

        // ===== 공통 상태 =====
        private bool isActive;

        // ===== 발사 기록 (stub — 메아리 구현 시 확장) =====
        // TODO: [Phase 5+] IFireRecord 인터페이스로 분리
        // 현재는 기록 구조만 정의, 실제 저장 로직은 메아리 구현 시 추가

        // ===== 초기화 =====

        /// <summary>
        /// Executor 시작. 풀에서 꺼낸 후 호출.
        /// FiringMode에 따라 즉시 완료되거나 Update에서 계속 진행.
        /// </summary>
        public void Begin(
            Skill skill,
            ISkillSpawner spawner,
            PlayerStats stats,
            Transform playerTransform,
            SkillTriggerSystem triggerSystem)
        {
            this.sourceSkill = skill;
            this.skillData = skill.Data;
            this.firingMode = skillData.firingMode;
            this.spawner = spawner;
            this.playerStats = stats;
            this.playerTransform = playerTransform;
            this.triggerSystem = triggerSystem;

            isActive = true;
            firedCount = 0;
            waitingForPhase2 = false;
            phase2Spawner = null;
            phase1Results.Clear();
            phase1ExpectedCount = 0;

            // 투사체 개수 (필터 적용)
            totalCount = GetFilteredProjectileCount();

            switch (firingMode)
            {
                case FiringMode.SimultaneousSpread:
                    ExecuteSimultaneous();
                    break;

                case FiringMode.Single:
                    ExecuteSingle();
                    break;

                case FiringMode.DelayedBurst:
                    burstDelay = skillData.burstDelay;
                    delayTimer = 0f;
                    // 첫 발은 즉시 발사
                    FireOnce(0);
                    firedCount = 1;
                    if (firedCount >= totalCount)
                        Complete();
                    break;

                case FiringMode.TwoPhase:
                    // Phase1 실행. Phase1 완료 시 OnPhase1Complete() 호출 필요.
                    ExecutePhase1();
                    break;
            }
        }

        /// <summary>
        /// TwoPhase 모드용. Phase2의 Spawner를 별도로 지정.
        /// Begin() 호출 전에 설정.
        /// </summary>
        public void SetPhase2Spawner(ISkillSpawner phase2)
        {
            phase2Spawner = phase2;
        }

        /// <summary>
        /// TwoPhase 모드에서 개별 orbital의 Phase1 완료를 알림.
        /// 각 orbital은 1바퀴 완주 시 자기 위치(position)와 바깥 방향(outward)을 전달.
        /// 모든 orbital이 완료되면 Phase2를 각 위치에서 일괄 발사.
        /// </summary>
        public void NotifyPhase1Complete(Vector2 position, Vector2 outwardDirection)
        {
            if (!isActive || !waitingForPhase2) return;

            phase1Results.Add((position, outwardDirection));

            // 아직 모든 orbital이 완료되지 않음 — 대기
            if (phase1Results.Count < phase1ExpectedCount) return;

            waitingForPhase2 = false;
            FirePhase2();
            Complete();
        }

        /// <summary>
        /// 집계된 Phase1 결과를 기반으로 Phase2 투사체를 각 orbital 위치에서 바깥 방향으로 발사.
        /// Phase2는 per-shot 단일 발사 (spread 적용 X, 각자 독립 방향).
        /// </summary>
        private void FirePhase2()
        {
            if (phase2Spawner == null || playerTransform == null)
            {
                phase1Results.Clear();
                return;
            }

            int count = phase1Results.Count;
            for (int i = 0; i < count; i++)
            {
                SpawnContext ctx = BuildContext(i);

                // 각 장검 위치에서 바깥 방향으로 단일 발사
                ctx.playerPosition = phase1Results[i].position;
                ctx.baseDirection = phase1Results[i].direction;
                ctx.totalCount = 1;
                ctx.fireIndex = 0;
                ctx.onSpawnComplete = null; // Phase2는 재귀 방지

                phase2Spawner.Spawn(ctx);
            }

            phase1Results.Clear();
        }

        // ===== Update (DelayedBurst 타이머) =====

        private void Update()
        {
            if (!isActive) return;

            // 게임 일시정지 시 정지
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            if (firingMode != FiringMode.DelayedBurst) return;
            if (firedCount >= totalCount) return;

            delayTimer += Time.deltaTime;
            if (delayTimer >= burstDelay)
            {
                delayTimer -= burstDelay;
                FireOnce(firedCount);
                firedCount++;

                if (firedCount >= totalCount)
                    Complete();
            }
        }

        // ===== 발사 모드별 실행 =====

        private void ExecuteSimultaneous()
        {
            for (int i = 0; i < totalCount; i++)
                FireOnce(i);

            Complete();
        }

        private void ExecuteSingle()
        {
            FireOnce(0);
            Complete();
        }

        private void ExecutePhase1()
        {
            // Phase1: objectCount만큼 orbital을 균등 각도로 스폰.
            // 각 orbital은 1바퀴 완주 시 NotifyPhase1Complete(pos, dir)를 호출.
            // 전원 완료 시 Phase2로 전환.
            phase1Results.Clear();
            phase1ExpectedCount = Mathf.Max(1, totalCount);
            waitingForPhase2 = true;

            for (int i = 0; i < phase1ExpectedCount; i++)
                FireOnce(i);
        }

        // ===== 단일 발사 =====

        /// <summary>
        /// SpawnContext를 구성하고 Spawner에 전달.
        /// applicableStats 필터가 여기서 적용됨.
        /// </summary>
        private void FireOnce(int index)
        {
            if (!isActive || spawner == null || playerTransform == null) return;

            SpawnContext context = BuildContext(index);
            spawner.Spawn(context);

            // TODO: [Phase 5+] 발사 기록 — 메아리 스킬용
            // fireRecorder?.Record(skillData.skillId, context.playerPosition,
            //     context.baseDirection, Time.time);
        }

        // ===== SpawnContext 구성 (applicableStats 필터 적용) =====

        private SpawnContext BuildContext(int index)
        {
            SkillData data = skillData;
            SpawnContext ctx = new SpawnContext();

            ctx.skillData = data;
            ctx.playerPosition = playerTransform.position;
            ctx.playerTransform = playerTransform;
            ctx.fireIndex = index;
            ctx.totalCount = totalCount;
            ctx.triggerSystem = triggerSystem;

            // ── 데미지 (필터 적용) ──
            int baseDamage = sourceSkill.CurrentDamage;
            ctx.rawDamage = baseDamage;
            if (playerStats != null)
            {
                float atkMul = playerStats.GetFilteredAttackMultiplier(data);
                ctx.damage = Mathf.RoundToInt(baseDamage * atkMul);
            }
            else
            {
                ctx.damage = baseDamage;
            }

            // ── 투사체 속도 (필터 적용) ──
            if (playerStats != null)
                ctx.projectileSpeed = playerStats.GetFilteredProjectileSpeed(data.projectileSpeed, data);
            else
                ctx.projectileSpeed = data.projectileSpeed;

            // ── 투사체 개수 (이미 totalCount에 반영됨, context에도 저장) ──
            ctx.projectileCount = totalCount;

            // ── 넉백 (필터 적용) ──
            float baseKnockback = 0f;
            var cfg = GameManager.Instance?.Config;
            if (cfg != null)
                baseKnockback = cfg.baseKnockbackForce;
            if (playerStats != null)
                ctx.knockbackForce = baseKnockback * playerStats.GetFilteredKnockbackMultiplier(data);
            else
                ctx.knockbackForce = baseKnockback;

            // ── 스킬 범위 보너스 (필터 적용) ──
            if (playerStats != null)
                ctx.skillRangeBonus = playerStats.GetFilteredSkillRangeBonus(data);
            else
                ctx.skillRangeBonus = 0f;

            // ── 스킬 유지시간 보너스 (필터 적용) ──
            if (playerStats != null)
                ctx.skillDurationBonus = playerStats.GetFilteredSkillDurationBonus(data);
            else
                ctx.skillDurationBonus = 0f;

            // ── 회복량 배율 (필터 적용) ──
            if (playerStats != null)
                ctx.healMultiplier = playerStats.GetFilteredHealMultiplier(data);
            else
                ctx.healMultiplier = 1f;

            // ── 치명타 데미지 배율 (필터 적용) ──
            if (playerStats != null && data.IsStatApplicable(StatType.CritDamage))
                ctx.critDamageMultiplier = playerStats.CritDamageMultiplier;
            else
                ctx.critDamageMultiplier = 1.5f; // PlayerStats.baseCritDamage 기본값

            // ── 발사 방향 ──
            ctx.baseDirection = GetBaseDirection(data.aimType);

            // ── TwoPhase 완료 콜백 ──
            if (firingMode == FiringMode.TwoPhase)
                ctx.onSpawnComplete = NotifyPhase1Complete;

            return ctx;
        }

        /// <summary>
        /// applicableStats 필터 적용된 발사 횟수.
        /// Single 모드: 무조건 1.
        /// Orbital: objectCount를 base로 사용 + 패시브 보너스 적용.
        /// 그 외: projectileCount + 패시브 보너스.
        /// </summary>
        private int GetFilteredProjectileCount()
        {
            if (firingMode == FiringMode.Single)
                return 1;

            // Orbital은 objectCount를 base로 사용 + 패시브 보너스 적용
            if (skillData.effectType == SkillEffectType.Orbital)
            {
                int orbitalBase = skillData.objectCount;
                if (playerStats != null)
                    return playerStats.GetFilteredProjectileCount(orbitalBase, skillData);
                return orbitalBase;
            }

            int baseCount = skillData.projectileCount;
            if (playerStats != null)
                return playerStats.GetFilteredProjectileCount(baseCount, skillData);
            return baseCount;
        }

        // ===== 발사 방향 (ProjectileEffect에서 이동) =====

        private Vector2 lastMoveDirection = Vector2.right;

        private Vector2 GetBaseDirection(AimType aimType)
        {
            Rigidbody2D rb;

            switch (aimType)
            {
                case AimType.ClosestEnemy:
                    Transform closest = FindClosestEnemy();
                    if (closest != null)
                        return ((Vector2)(closest.position - playerTransform.position)).normalized;
                    return lastMoveDirection;

                case AimType.MoveDirection:
                    rb = playerTransform.GetComponent<Rigidbody2D>();
                    if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
                    {
                        lastMoveDirection = rb.linearVelocity.normalized;
                        return lastMoveDirection;
                    }
                    return lastMoveDirection;

                case AimType.ReverseMoveDirection:
                    rb = playerTransform.GetComponent<Rigidbody2D>();
                    if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
                    {
                        lastMoveDirection = rb.linearVelocity.normalized;
                        return -lastMoveDirection;
                    }
                    return -lastMoveDirection;

                case AimType.Random:
                    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                default:
                    return Vector2.right;
            }
        }

        private Transform FindClosestEnemy()
        {
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies.Length == 0) return null;

            Transform closest = null;
            float minDist = float.MaxValue;

            foreach (var e in enemies)
            {
                if (!e.activeInHierarchy) continue;
                float dist = Vector2.Distance(playerTransform.position, e.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = e.transform;
                }
            }
            return closest;
        }

        // ===== 완료/정리 =====

        private void Complete()
        {
            isActive = false;
            PoolManager.Instance?.Return(gameObject);
        }

        /// <summary>
        /// 외부에서 강제 정리. 레벨업/사망 시 SkillManager에서 호출.
        /// 진행 중인 DelayedBurst/TwoPhase를 즉시 중단.
        /// </summary>
        public void ForceCancel()
        {
            if (!isActive) return;

            spawner?.Cleanup();
            phase2Spawner?.Cleanup();
            isActive = false;
            PoolManager.Instance?.Return(gameObject);
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            isActive = false;
            firedCount = 0;
            waitingForPhase2 = false;
            phase1Results.Clear();
            phase1ExpectedCount = 0;
        }

        public void OnReturnToPool()
        {
            gameObject.SetActive(false);
            isActive = false;
            spawner = null;
            phase2Spawner = null;
            skillData = null;
            playerStats = null;
            playerTransform = null;
            triggerSystem = null;
            sourceSkill = null;
            phase1Results.Clear();
            phase1ExpectedCount = 0;
        }
    }
}