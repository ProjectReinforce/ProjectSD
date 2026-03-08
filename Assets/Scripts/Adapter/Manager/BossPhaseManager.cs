using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Domain;
using SwDreams.Domain.Interfaces;
using SwDreams.Data;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 보스 페이즈 FSM + 공격 패턴 실행.
    /// GameManager에서 보스 로직 분리 (SRP).
    ///
    /// 페이즈별 활성 공격 패턴이 다름:
    /// - Phase 1: [ShockwaveAttack]
    /// - Phase 2: [ShockwaveAttack(빠른), CircleZoneAttack]
    /// - Phase 3: [ShockwaveAttack(최고속), CircleZoneAttack(x2), GlobalSlowAttack]
    ///
    /// 호스트에서만 공격 실행. 비주얼 동기화는 RPC.
    ///
    /// 셋업: GameScene에 빈 GameObject "BossPhaseManager"
    ///        → BossPhaseManager + PhotonView 부착
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class BossPhaseManager : MonoBehaviourPun
    {
        public static BossPhaseManager Instance { get; private set; }

        // 현재 보스 참조
        private Boss currentBoss;
        private BossData bossData;
        private bool isActive = false;

        // 페이즈별 공격 패턴 세트
        private Dictionary<BossPhase, List<AttackEntry>> phaseAttacks
            = new Dictionary<BossPhase, List<AttackEntry>>();

        // 지연 폭발 큐
        private List<DelayedExplosion> pendingExplosions = new List<DelayedExplosion>();

        // 글로벌 슬로우
        private float slowTimer;
        private float currentSlowMultiplier = 1f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===== 초기화 =====

        /// <summary>
        /// 보스 전투 시작. BossSpawner에서 호출.
        /// </summary>
        public void StartBossFight(Boss boss, BossData data)
        {
            currentBoss = boss;
            bossData = data;
            isActive = true;

            SetupPhaseAttacks();

            // 페이즈 전환 이벤트 구독
            boss.OnPhaseChanged += OnPhaseChanged;
            boss.OnDied += OnBossDied;

            Debug.Log("[BossPhaseManager] 보스전 시작");
        }

        /// <summary>
        /// 보스전 종료 (처치 또는 게임오버).
        /// </summary>
        public void EndBossFight()
        {
            isActive = false;
            pendingExplosions.Clear();
            slowTimer = 0f;
            currentSlowMultiplier = 1f;

            if (currentBoss != null)
            {
                currentBoss.OnPhaseChanged -= OnPhaseChanged;
                currentBoss.OnDied -= OnBossDied;
            }

            currentBoss = null;
            Debug.Log("[BossPhaseManager] 보스전 종료");
        }

        // ===== 페이즈별 공격 세트 구성 =====

        private void SetupPhaseAttacks()
        {
            phaseAttacks.Clear();

            // Phase 1: 충격파만
            phaseAttacks[BossPhase.Phase1] = new List<AttackEntry>
            {
                new AttackEntry(new ShockwaveAttack(
                    bossData.p1ShockwaveCooldown,
                    bossData.p1ShockwaveDamage,
                    bossData.p1ShockwaveHalfAngle,
                    bossData.p1ShockwaveRange,
                    bossData.shockwaveEffectPrefab))
            };

            // Phase 2: 빠른 충격파 + 원형 지대
            phaseAttacks[BossPhase.Phase2] = new List<AttackEntry>
            {
                new AttackEntry(new ShockwaveAttack(
                    bossData.p2ShockwaveCooldown,
                    bossData.p1ShockwaveDamage,
                    bossData.p1ShockwaveHalfAngle,
                    bossData.p1ShockwaveRange,
                    bossData.shockwaveEffectPrefab)),
                new AttackEntry(new CircleZoneAttack(
                    bossData.p2CircleZoneCooldown,
                    bossData.p2CircleZoneDamage,
                    bossData.p2CircleZoneDelay,
                    bossData.p2CircleZoneRadius,
                    1,
                    bossData.circleZoneEffectPrefab))
            };

            // Phase 3: 최고속 충격파 + 원형 지대 x2 + 전체 슬로우
            phaseAttacks[BossPhase.Phase3] = new List<AttackEntry>
            {
                new AttackEntry(new ShockwaveAttack(
                    bossData.p3ShockwaveCooldown,
                    bossData.p1ShockwaveDamage,
                    bossData.p1ShockwaveHalfAngle,
                    bossData.p1ShockwaveRange,
                    bossData.shockwaveEffectPrefab)),
                new AttackEntry(new CircleZoneAttack(
                    bossData.p2CircleZoneCooldown,
                    bossData.p2CircleZoneDamage,
                    bossData.p2CircleZoneDelay,
                    bossData.p2CircleZoneRadius,
                    bossData.p3CircleZoneCount,
                    bossData.circleZoneEffectPrefab)),
                new AttackEntry(new GlobalSlowAttack(
                    bossData.p3SlowInterval,
                    bossData.p3SlowDuration,
                    bossData.p3SlowMultiplier))
            };
        }

        // ===== Update: 공격 실행 =====

        private void Update()
        {
            if (!isActive) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (currentBoss == null || !currentBoss.IsAlive) return;

            // 공격 패턴 실행
            UpdateAttacks();

            // 지연 폭발 처리
            UpdateDelayedExplosions();

            // 슬로우 타이머
            UpdateSlowTimer();
        }

        private void UpdateAttacks()
        {
            BossPhase phase = currentBoss.CurrentPhase;
            if (!phaseAttacks.ContainsKey(phase)) return;

            Transform target = currentBoss.FindClosestAlivePlayer();

            foreach (var entry in phaseAttacks[phase])
            {
                entry.timer += Time.deltaTime;

                if (entry.pattern.CanExecute(entry.timer))
                {
                    entry.pattern.Execute(currentBoss.transform, target);
                    entry.timer = 0f;
                }
            }
        }

        // ===== 지연 폭발 =====

        /// <summary>
        /// CircleZoneAttack에서 호출. 지연 폭발 등록.
        /// </summary>
        public void RegisterDelayedExplosion(Vector2 position, float delay,
            int damage, float radius, GameObject effectPrefab)
        {
            pendingExplosions.Add(new DelayedExplosion
            {
                position = position,
                timer = delay,
                damage = damage,
                radius = radius,
                effectPrefab = effectPrefab
            });

            // 경고 이펙트 (모든 클라이언트)
            photonView.RPC(nameof(RPC_ShowCircleWarning), RpcTarget.All,
                position.x, position.y, radius, delay);
        }

        private void UpdateDelayedExplosions()
        {
            for (int i = pendingExplosions.Count - 1; i >= 0; i--)
            {
                var exp = pendingExplosions[i];
                exp.timer -= Time.deltaTime;
                pendingExplosions[i] = exp;

                if (exp.timer <= 0f)
                {
                    ExecuteExplosion(exp);
                    pendingExplosions.RemoveAt(i);
                }
            }
        }

        private void ExecuteExplosion(DelayedExplosion exp)
        {
            // 범위 내 플레이어에게 데미지
            var hits = Physics2D.OverlapCircleAll(exp.position, exp.radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(exp.damage);
            }

            // 폭발 이펙트 (모든 클라이언트)
            photonView.RPC(nameof(RPC_ShowExplosion), RpcTarget.All,
                exp.position.x, exp.position.y, exp.radius);
        }

        // ===== 글로벌 슬로우 =====

        /// <summary>
        /// GlobalSlowAttack에서 호출.
        /// </summary>
        public void ApplyGlobalSlow(float duration, float slowMultiplier)
        {
            slowTimer = duration;
            currentSlowMultiplier = slowMultiplier;

            photonView.RPC(nameof(RPC_ApplyGlobalSlow), RpcTarget.All,
                slowMultiplier, duration);
        }

        private void UpdateSlowTimer()
        {
            if (slowTimer <= 0f) return;

            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                photonView.RPC(nameof(RPC_RemoveGlobalSlow), RpcTarget.All);
            }
        }

        // ===== 이벤트 핸들러 =====

        private void OnPhaseChanged(BossPhase newPhase)
        {
            Debug.Log($"[BossPhaseManager] 페이즈 전환 감지: {newPhase}");
            // 공격 타이머 리셋 (페이즈 전환 직후 바로 공격하지 않도록)
            if (phaseAttacks.ContainsKey(newPhase))
            {
                foreach (var entry in phaseAttacks[newPhase])
                    entry.timer = 0f;
            }
        }

        private void OnBossDied()
        {
            EndBossFight();
            GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.GameClear);
        }

        // ===== RPC =====

        [PunRPC]
        private void RPC_ShowCircleWarning(float x, float y, float radius, float delay)
        {
            // TODO: 원형 경고 이펙트 생성 (빨간 원 → 점점 채워짐)
            Debug.Log($"[BossPhaseManager] 원형 지대 경고: ({x:F1},{y:F1}), 반경={radius}, {delay}초 후 폭발");
        }

        [PunRPC]
        private void RPC_ShowExplosion(float x, float y, float radius)
        {
            // TODO: 폭발 이펙트 생성
            Debug.Log($"[BossPhaseManager] 원형 지대 폭발: ({x:F1},{y:F1}), 반경={radius}");
        }

        [PunRPC]
        private void RPC_ApplyGlobalSlow(float multiplier, float duration)
        {
            // 로컬 플레이어 이동속도 감소
            // PlayerStub의 moveSpeed에 임시 배율 적용
            // TODO: PlayerStub에 ApplySlow(multiplier) 메서드 추가
            Debug.Log($"[BossPhaseManager] 전체 슬로우 적용: {multiplier * 100}%, {duration}초");
        }

        [PunRPC]
        private void RPC_RemoveGlobalSlow()
        {
            // 슬로우 해제
            Debug.Log("[BossPhaseManager] 슬로우 해제");
        }

        // ===== 내부 구조체 =====

        /// <summary>공격 패턴 + 개별 타이머.</summary>
        private class AttackEntry
        {
            public IBossAttackPattern pattern;
            public float timer;

            public AttackEntry(IBossAttackPattern pattern)
            {
                this.pattern = pattern;
                this.timer = 0f;
            }
        }

        /// <summary>지연 폭발 데이터.</summary>
        private struct DelayedExplosion
        {
            public Vector2 position;
            public float timer;
            public int damage;
            public float radius;
            public GameObject effectPrefab;
        }
    }
}