using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 추가 데미지 효과.
    /// primary = 추가 데미지량 (고정값)
    /// secondary = 범위 (0이면 단일 대상, >0이면 AoE)
    ///
    /// 사용 예: 무기 부가효과 "적중 시 추가 10 데미지", 정수 "주변 적 추가 타격".
    /// R9: 단일 발화 = 1회 치명타 판정 (AoE 도 모든 대상 동일 isCrit).
    /// </summary>
    public class DealDamageHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            int baseDamage = Mathf.RoundToInt(parameters.primary);
            float radius = parameters.secondary;

            if (baseDamage <= 0) return;

            // R9: 호스트만 치명타 굴림. 클라 fire 시엔 일반 데미지로 fallback (§ 11 호스트 권위).
            // 본 효과 1회 판정 — AoE 의 모든 대상에 동일 isCrit (§ 9 "단일 적중 내 1회").
            bool isCrit = false;
            int finalDamage = baseDamage;
            if (PhotonNetwork.IsMasterClient)
                finalDamage = CritJudgment.Roll(baseDamage, context.critChance, context.critDamageMultiplier, out isCrit);

            if (radius > 0f)
            {
                var hits = Physics2D.OverlapCircleAll(context.position, radius);
                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Enemy")) continue;
                    var damageable = hit.GetComponent<IDamageable>();
                    if (damageable == null || !damageable.IsAlive) continue;

                    var enemy = hit.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                    if (enemy != null) enemy.TakeDamage(finalDamage, isCrit);
                    else damageable.TakeDamage(finalDamage);
                }
            }
            else if (context.target != null)
            {
                var damageable = context.target.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    var enemy = context.target.GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                    if (enemy != null) enemy.TakeDamage(finalDamage, isCrit);
                    else damageable.TakeDamage(finalDamage);
                }
            }
        }
    }
}
