using Photon.Pun;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;
using UnityEngine;

namespace SwDreams.Features.Skill.Adapter.Chaos.Handlers
{
    /// <summary>
    /// 연쇄 폭발 혼돈 효과 핸들러.
    ///
    /// 구독: IChaosHookBus.EnemyKilled (isVisualOnly=false → 호스트 권위 데미지, true → 로컬 비주얼).
    ///
    /// 파라미터 (ChaosSkillData.paramsByRarity):
    ///   primary   = 폭발 데미지 (0 이면 ctor fallback 사용)
    ///   secondary = 폭발 반경   (0 이면 ctor fallback 사용)
    ///   tertiary  = 미사용 (프레임당 최대 연쇄는 manager SerializeField)
    ///
    /// Frame 리셋: Unity Update 를 가질 수 없는 plain class 라 <see cref="UnityEngine.Time.frameCount"/>
    /// 비교로 자기 관리. 동일 프레임 연쇄는 maxChainPerFrame 로 제한 (무한 루프 방지).
    /// </summary>
    public class ChainExplosionHandler : IChaosEffectHandler
    {
        public ChaosEffectType EffectType => ChaosEffectType.ChainExplosion;

        // Ctor 주입 (ChaosSkillManager 의 SerializeField 전달).
        private readonly GameObject explosionEffectPrefab;
        private readonly int fallbackMaxChain;
        private readonly int fallbackDamage;
        private readonly float fallbackRadius;

        // 활성 상태
        private ChaosSkillData activeData;
        private Rarity activeRarity;
        private ChaosHandlerContext ctx;
        private bool subscribed;

        // 프레임 기반 연쇄 카운터
        private int chainCountThisFrame;
        private int lastFrame = -1;

        public ChainExplosionHandler(
            GameObject explosionEffectPrefab,
            int fallbackMaxChain,
            int fallbackDamage,
            float fallbackRadius)
        {
            this.explosionEffectPrefab = explosionEffectPrefab;
            this.fallbackMaxChain = fallbackMaxChain;
            this.fallbackDamage = fallbackDamage;
            this.fallbackRadius = fallbackRadius;
        }

        public void Apply(ChaosSkillData data, Rarity rolledRarity, ChaosHandlerContext ctx)
        {
            activeData = data;
            activeRarity = rolledRarity;
            this.ctx = ctx;

            if (!subscribed && ctx.hookBus != null)
            {
                ctx.hookBus.EnemyKilled += HandleEnemyKilled;
                subscribed = true;
            }
        }

        public void Remove(ChaosHandlerContext ctx)
        {
            if (subscribed && ctx.hookBus != null)
            {
                ctx.hookBus.EnemyKilled -= HandleEnemyKilled;
                subscribed = false;
            }
            activeData = null;
        }

        // ===== 훅 핸들러 =====

        private void HandleEnemyKilled(Vector2 position, bool isVisualOnly)
        {
            // 프레임 리셋
            if (Time.frameCount != lastFrame)
            {
                chainCountThisFrame = 0;
                lastFrame = Time.frameCount;
            }

            var (damage, radius) = ResolveParams();
            int maxChain = fallbackMaxChain;

            if (chainCountThisFrame >= maxChain) return;
            chainCountThisFrame++;

            if (isVisualOnly)
            {
                // 로컬 플레이어 소속일 때만 비주얼 재생. 매니저의 playerRoot 를 PhotonView 로 판정.
                if (!IsLocalPlayer()) return;

                SpawnExplosionVisual(position);

                // 폭발 범위 내 적에 데미지 팝업만 (실 데미지는 호스트 경로에서).
                var hits = Physics2D.OverlapCircleAll(position, radius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    var enemy = hit.GetComponent<Features.Enemy.Adapter.Enemy>();
                    if (enemy != null && enemy.IsAlive)
                        enemy.ShowHitVisuals(damage);
                }
                return;
            }

            // 호스트 권위 경로
            if (!PhotonNetwork.IsMasterClient) return;

            TriggerExplosionDamage(position, damage, radius);

            // 호스트 자기 캐릭터면 비주얼도 같이 표시 (원격 클라는 자기 visualOnly 경로에서).
            if (IsLocalPlayer())
                SpawnExplosionVisual(position);
        }

        // ===== 내부 헬퍼 =====

        private (int damage, float radius) ResolveParams()
        {
            if (activeData == null) return (fallbackDamage, fallbackRadius);
            var p = activeData.GetParams(activeRarity);
            int d = p.primary > 0f ? Mathf.RoundToInt(p.primary) : fallbackDamage;
            float r = p.secondary > 0f ? p.secondary : fallbackRadius;
            return (d, r);
        }

        private void TriggerExplosionDamage(Vector2 position, int damage, float radius)
        {
            // R9: 연쇄폭발 노드별 1회 판정 (§ 9 "체인 / 연쇄폭발 노드별 재판정").
            // ChainExplosion 은 ChaosHandlerContext 가 critChance/critMultiplier 를 들고있지 않으므로,
            // 현 구조에선 보스 변형 데미지 톤만 보존 — 일반 치명타는 미적용 (필요 시 ChaosHandlerContext 확장).
            var hits = Physics2D.OverlapCircleAll(position, radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                var enemy = hit.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                if (enemy != null) enemy.TakeDamage(damage, false);
                else damageable.TakeDamage(damage);
            }
        }

        private void SpawnExplosionVisual(Vector2 position)
        {
            if (explosionEffectPrefab == null) return;
            var fx = PoolManager.Instance?.Get(explosionEffectPrefab);
            if (fx != null) fx.transform.position = position;
        }

        private bool IsLocalPlayer()
        {
            if (ctx.playerRoot == null) return false;
            var pv = ctx.playerRoot.GetComponentInChildren<PhotonView>();
            return pv != null && pv.IsMine;
        }
    }
}
