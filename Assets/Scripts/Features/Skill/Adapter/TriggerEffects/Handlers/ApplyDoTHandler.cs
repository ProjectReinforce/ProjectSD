using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using Photon.Pun;

namespace SwDreams.Features.Skill.Adapter.TriggerEffects
{
    /// <summary>
    /// 지속 데미지(DoT) 부여.
    /// primary = 틱당 데미지
    /// secondary = 지속시간 (초)
    /// tertiary = 틱 간격 (초, 0이면 기본 0.5초)
    ///
    /// 대상에 DoTEffect 컴포넌트를 동적 추가.
    /// 이미 같은 소스의 DoT가 있으면 갱신.
    ///
    /// 사용 예: 뇌전역(OnHit → ApplyDoT), 불 정수(OnHit → ApplyDoT)
    /// </summary>
    public class ApplyDoTHandler : IEffectActionHandler
    {
        public void Execute(EffectParams parameters, TriggerContext context)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (context.target == null) return;

            int tickDamage = Mathf.RoundToInt(parameters.primary);
            float duration = parameters.secondary;
            float tickInterval = parameters.tertiary > 0f ? parameters.tertiary : 0.5f;

            if (tickDamage <= 0 || duration <= 0f) return;

            // 기존 DoT가 있으면 갱신, 없으면 추가
            var existing = context.target.GetComponent<DoTEffect>();
            if (existing != null)
            {
                existing.Refresh(tickDamage, duration, tickInterval);
                return;
            }

            var dot = context.target.gameObject.AddComponent<DoTEffect>();
            dot.Initialize(tickDamage, duration, tickInterval);
        }
    }

    /// <summary>
    /// 지속 피해 컴포넌트. 적에 동적 부착.
    /// 틱 간격마다 데미지를 주고, 지속시간 만료 시 자동 제거.
    /// </summary>
    public class DoTEffect : MonoBehaviour
    {
        private int tickDamage;
        private float duration;
        private float tickInterval;
        private float tickTimer;
        private float aliveTime;

        public void Initialize(int tickDamage, float duration, float tickInterval)
        {
            this.tickDamage = tickDamage;
            this.duration = duration;
            this.tickInterval = tickInterval;
            tickTimer = 0f;
            aliveTime = 0f;
        }

        public void Refresh(int tickDamage, float duration, float tickInterval)
        {
            this.tickDamage = tickDamage;
            this.duration = duration;
            this.tickInterval = tickInterval;
            aliveTime = 0f; // 지속시간 리셋
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Destroy(this);
                return;
            }

            aliveTime += Time.deltaTime;
            if (aliveTime >= duration)
            {
                Destroy(this);
                return;
            }

            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;

                var damageable = GetComponent<SwDreams.Shared.Domain.Interfaces.IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(tickDamage);
            }
        }
    }
}
