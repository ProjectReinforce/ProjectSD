using System;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using System.Collections.Generic;
using Photon.Pun;
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

        // [N15/N17] RPC 도착 경로(BeginFromNetwork)는 자기측 발사가 아니므로 RPC 송신 금지.
        // 자기 발사(Begin)만 송신.
        private bool isFromNetwork;

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
            isFromNetwork = false; // 자기 발사 — RPC 송신 흐름 활성
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
        /// [N15/N17] 자기 발사 시 spawnPos override 결정 + RPC 송신.
        /// </summary>
        private void FireOnce(int index)
        {
            if (!isActive || spawner == null || playerTransform == null) return;

            SpawnContext context = BuildContext(index);

            // [N15] 비결정적 spawnPos 자기 클라가 한 번 결정 → ctx.spawnPosOverride
            if (spawner.TryGenerateSpawnPos(context, out Vector2 generatedPos))
            {
                context.spawnPosOverride = generatedPos;
                context.hasSpawnPosOverride = true;
            }

            // 자기측 prediction Spawn (시각 + 호스트면 데미지 권위)
            spawner.Spawn(context);

            // [N15/N17] RPC 송신 — 자기 PhotonView (IsMine) 만 송신.
            // TwoPhase (장검 진화 등) 는 Phase 1 범위에서 제외 — Phase1 결과 집계 후 Phase2 발사 RPC 미설계.
            // TwoPhase 는 자기 클라 자체 시뮬레이션 유지 (기존 desync 허용, Phase 2+ 처리).
            if (!isFromNetwork && firingMode != FiringMode.TwoPhase)
                BroadcastNetworkSpawn(context, index);

            // TODO: [Phase 5+] 발사 기록 — 메아리 스킬용
        }

        /// <summary>
        /// [N15/N17] FireOnce 직후 호출. 자기 PhotonView 가 IsMine 일 때만 RPC 송신.
        /// 자기 = 호스트 → Others 에 broadcast / 자기 ≠ 호스트 → MasterClient 에 request.
        /// </summary>
        private void BroadcastNetworkSpawn(SpawnContext ctx, int index)
        {
            if (playerTransform == null || skillData == null) return;
            var pv = playerTransform.GetComponent<PhotonView>();
            if (pv == null || !pv.IsMine) return;

            int skillId = skillData.skillId;
            Vector2 dir = ctx.baseDirection;
            Vector2 pos = ctx.hasSpawnPosOverride ? ctx.spawnPosOverride : Vector2.zero;
            bool hasOverride = ctx.hasSpawnPosOverride;

            if (PhotonNetwork.IsMasterClient)
            {
                // 자기 = 호스트. Others 에 broadcast (자기 송신자 제외).
                pv.RPC("RPC_BroadcastSkillSpawn", RpcTarget.Others,
                    skillId, dir, pos, hasOverride, index, totalCount);
            }
            else
            {
                // 자기 = 클라. MasterClient 에 request → 호스트가 자기측 spawn + Others broadcast.
                pv.RPC("RPC_RequestSkillSpawn", RpcTarget.MasterClient,
                    skillId, dir, pos, hasOverride, index, totalCount);
            }
        }

        /// <summary>
        /// [N15/N17] 다른 클라가 RPC 로 보낸 발사 정보로 자기측 단발 spawn.
        /// 호스트 측은 데미지 권위, 다른 클라 측은 시각만. RPC 송신 X.
        /// firingMode 별 timing (DelayedBurst burstDelay, TwoPhase 등) 우회 — 송신자 측에서
        /// 이미 timing 처리 후 매 발사마다 RPC 가 도착하므로.
        /// </summary>
        public void BeginFromNetwork(
            Skill skill,
            ISkillSpawner spawner,
            PlayerStats stats,
            Transform playerTransform,
            SkillTriggerSystem triggerSystem,
            Vector2 baseDir,
            Vector2 spawnPos,
            bool hasSpawnPosOverride,
            int fireIndex,
            int totalCountFromNetwork)
        {
            this.sourceSkill = skill;
            this.skillData = skill.Data;
            this.spawner = spawner;
            this.playerStats = stats;
            this.playerTransform = playerTransform;
            this.triggerSystem = triggerSystem;
            this.totalCount = totalCountFromNetwork;
            this.firedCount = totalCountFromNetwork; // Update 진입 안 함
            this.firingMode = FiringMode.Single;     // 단발 처리
            this.isFromNetwork = true;               // RPC 송신 차단
            this.isActive = true;
            this.waitingForPhase2 = false;
            this.phase2Spawner = null;
            this.phase1Results.Clear();
            this.phase1ExpectedCount = 0;

            // SpawnContext 구성 (BuildContext 와 동일하지만 외부 결정 spawnPos / baseDir override)
            SpawnContext ctx = BuildContext(fireIndex);
            ctx.baseDirection = baseDir;
            ctx.spawnPosOverride = spawnPos;
            ctx.hasSpawnPosOverride = hasSpawnPosOverride;

            spawner.Spawn(ctx);
            Complete();
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

            // ── 데미지 (3-op + 필터 적용) ──
            // 공식: (skillBase + ΣAdd + skillBase × ΣPercentBonus) × ΠMultiplicative × baseAttackMultiplier
            // applicableStats 필터는 ApplyAttackTo 내부에서 처리.
            int baseDamage = sourceSkill.CurrentDamage;
            ctx.rawDamage = baseDamage;
            if (playerStats != null)
                ctx.damage = Mathf.RoundToInt(playerStats.ApplyAttackTo(baseDamage, data));
            else
                ctx.damage = baseDamage;

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

            // ── 치명타 데미지 배율 (필터 적용 + N18 multiplier) ──
            var cfgForCrit = GameManager.Instance?.Config;
            float critMultDefault = cfgForCrit != null ? cfgForCrit.critMultBase : 1.5f;
            float critChanceDefault = cfgForCrit != null ? cfgForCrit.critChanceBase : 0.05f;

            if (playerStats != null)
            {
                float critDmgMult = data.GetStatMultiplier(StatType.CritDamage);
                // critDamageMultiplier 의 base = critMultDefault. 보너스 = (CritDamageMultiplier - default).
                ctx.critDamageMultiplier = critMultDefault
                    + (playerStats.CritDamageMultiplier - critMultDefault) * critDmgMult;
            }
            else
            {
                ctx.critDamageMultiplier = critMultDefault;
            }

            // ── 치명타 확률 (필터 적용 + N18 multiplier) ──
            if (playerStats != null)
            {
                float critChMult = data.GetStatMultiplier(StatType.CritChance);
                ctx.critChance = critChanceDefault
                    + (playerStats.CritChanceProbability - critChanceDefault) * critChMult;
                ctx.critChance = Mathf.Clamp01(ctx.critChance);
            }
            else
            {
                ctx.critChance = critChanceDefault;
            }

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
            isFromNetwork = false;
            phase1Results.Clear();
            phase1ExpectedCount = 0;
        }

        public void OnReturnToPool()
        {
            gameObject.SetActive(false);
            isActive = false;
            isFromNetwork = false;
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