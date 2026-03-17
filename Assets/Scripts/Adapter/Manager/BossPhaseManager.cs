using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DG.Tweening;
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

        // Phase 6 Step 3: 보스 혼돈 스킬 modifier
        private float cooldownMultiplier = 1f;   // 폭주 모드: 0.5
        private int shockwaveFanCount = 1;       // 도박꾼: 3

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
            CleanupChaosEffects();
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
                    bossData.shockwaveEffectPrefab,
                    shockwaveFanCount))
            };

            // Phase 2: 빠른 충격파 + 원형 지대
            phaseAttacks[BossPhase.Phase2] = new List<AttackEntry>
            {
                new AttackEntry(new ShockwaveAttack(
                    bossData.p2ShockwaveCooldown,
                    bossData.p1ShockwaveDamage,
                    bossData.p1ShockwaveHalfAngle,
                    bossData.p1ShockwaveRange,
                    bossData.shockwaveEffectPrefab,
                    shockwaveFanCount)),
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
                    bossData.shockwaveEffectPrefab,
                    shockwaveFanCount)),
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

            // 레벨업 등 일시정지 중에는 보스 공격 중단
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameManager.GameState.Paused)
                return;

            // 공격 패턴 실행
            UpdateAttacks();

            // 보스 혼돈 스킬 효과 갱신
            if (BossChaosApplicator.Instance != null)
                BossChaosApplicator.Instance.UpdateBossEffect(Time.deltaTime);

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
                
                // 쿨다운에 multiplier 적용 (폭주 모드 시 0.5배)
                float effectiveTimer = entry.timer / cooldownMultiplier;
                if (entry.pattern.CanExecute(effectiveTimer))
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
            // 원형 경고 이펙트: 빨간 원이 점점 채워지며 폭발 예고
            var warningGO = new GameObject("CircleWarning");
            warningGO.transform.position = new Vector3(x, y, 0f);

            var sr = warningGO.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(1f, 0.2f, 0.2f, 0.15f);
            sr.sortingOrder = 90;
            warningGO.transform.localScale = Vector3.zero;

            // 점점 커지는 연출
            float targetScale = radius * 2f;
            warningGO.transform.DOScale(targetScale, delay)
                .SetEase(Ease.Linear)
                .OnComplete(() => Object.Destroy(warningGO));

            // 알파 점멸
            sr.DOFade(0.5f, delay * 0.3f).SetLoops(-1, LoopType.Yoyo);
        }

        [PunRPC]
        private void RPC_ShowExplosion(float x, float y, float radius)
        {
            // 폭발 이펙트: 흰색 원이 빠르게 확장 후 사라짐
            var explosionGO = new GameObject("Explosion");
            explosionGO.transform.position = new Vector3(x, y, 0f);

            var sr = explosionGO.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            sr.sortingOrder = 95;

            float targetScale = radius * 2.5f;
            explosionGO.transform.localScale = Vector3.one * radius;

            var seq = DOTween.Sequence();
            seq.Append(explosionGO.transform.DOScale(targetScale, 0.15f).SetEase(Ease.OutQuad));
            seq.Join(sr.DOFade(0f, 0.3f));
            seq.OnComplete(() => Object.Destroy(explosionGO));
        }

        [PunRPC]
        private void RPC_ApplyGlobalSlow(float multiplier, float duration)
        {
            // 로컬 플레이어에 슬로우 적용
            var localPlayer = FindLocalPlayerStub();
            if (localPlayer != null)
                localPlayer.ApplySlow(multiplier);

            Debug.Log($"[BossPhaseManager] 전체 슬로우 적용: {multiplier * 100}%, {duration}초");
        }

        [PunRPC]
        private void RPC_RemoveGlobalSlow()
        {
            var localPlayer = FindLocalPlayerStub();
            if (localPlayer != null)
                localPlayer.RemoveSlow();

            Debug.Log("[BossPhaseManager] 슬로우 해제");
        }

        private SwDreams.Testing.PlayerStub FindLocalPlayerStub()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var go in players)
            {
                var pv = go.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                    return go.GetComponent<SwDreams.Testing.PlayerStub>();
            }
            return null;
        }

        /// <summary>런타임 원형 스프라이트 생성 (이펙트용).</summary>
        private static Sprite cachedCircleSprite;
        private static Sprite CreateCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radiusSq = center * center;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                tex.SetPixel(x, y, dx * dx + dy * dy <= radiusSq
                    ? Color.white : Color.clear);
            }
            tex.Apply();

            cachedCircleSprite = Sprite.Create(tex,
                new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return cachedCircleSprite;
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

        // ===== Phase 6 Step 3: 보스 혼돈 스킬 지원 =====

        /// <summary>폭주 모드: 쿨다운 배율 변경.</summary>
        public void SetCooldownMultiplier(float multiplier)
        {
            cooldownMultiplier = Mathf.Max(0.1f, multiplier);
            Debug.Log($"[BossPhaseManager] 쿨다운 배율: {cooldownMultiplier}");
        }

        /// <summary>도박꾼: 충격파 갈래 수 변경. 페이즈 공격 세트 재구성.</summary>
        public void SetShockwaveFanCount(int count)
        {
            shockwaveFanCount = count;
            // 이미 구성된 공격 세트를 재구성
            if (bossData != null) SetupPhaseAttacks();
            Debug.Log($"[BossPhaseManager] 충격파 갈래: {shockwaveFanCount}");
        }

        /// <summary>EndBossFight 시 혼돈 효과 정리.</summary>
        private void CleanupChaosEffects()
        {
            cooldownMultiplier = 1f;
            shockwaveFanCount = 1;
            if (BossChaosApplicator.Instance != null)
                BossChaosApplicator.Instance.CleanupBossEffect();
        }
    }
}