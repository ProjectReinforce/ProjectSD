using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 범위 폭발 효과.
    /// primary = 폭발 반경
    /// secondary = 데미지 배율 (1.0 = context.damage의 100%)
    ///
    /// 사용 예: 폭렬 표창 (OnHit → Explode), 연쇄 폭발 혼돈 등.
    /// R9: 단일 폭발 = 1회 치명타 판정. 모든 대상에 동일 isCrit.
    /// </summary>
    public class ExplodeHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            float radius = parameters.primary;
            float damageMultiplier = parameters.secondary > 0f ? parameters.secondary : 1f;
            int baseDamage = Mathf.RoundToInt(context.damage * damageMultiplier);

            if (radius <= 0f || baseDamage <= 0) return;

            // R9: 호스트만 치명타 굴림. 클라 fire 시엔 일반 데미지로 fallback (§ 11 호스트 권위).
            bool isCrit = false;
            int finalDamage = baseDamage;
            if (PhotonNetwork.IsMasterClient)
                finalDamage = CritJudgment.Roll(baseDamage, context.critChance, context.critDamageMultiplier, out isCrit);

            var hits = Physics2D.OverlapCircleAll(context.position, radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                // 트리거 원인이 된 적은 이미 맞았으므로 제외 (선택적)
                if (context.target != null && hit.transform == context.target) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                var enemy = hit.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                if (enemy != null) enemy.TakeDamage(finalDamage, isCrit);
                else damageable.TakeDamage(finalDamage);
            }

            // TODO: 폭발 이펙트 비주얼 (PoolManager에서 가져오기)
        }
    }
}