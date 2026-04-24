using UnityEngine;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Features.Skill.Adapter.TriggerEffects;
using SwDreams.Shared.Domain.ValueObjects;
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
    /// 같은 source 가 이미 있으면 갱신 (Refresh), 다른 source 면 별도 컴포넌트로 추가.
    /// context.source 가 null/빈 문자열이면 "legacy" 단일 인스턴스로 취급 (기존 동작 호환).
    ///
    /// 사용 예:
    /// - 뇌전역(OnHit → ApplyDoT, 단일 스킬 효과, source=null)
    /// - 불 정수(OnHit → ApplyDoT, source="essence_fire_0" or "essence_fire_1" 로 중첩 가능)
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

            string source = string.IsNullOrEmpty(context.source) ? RuntimeSources.Legacy : context.source;

            // 같은 source 의 기존 DoT 있으면 Refresh, 없으면 새 컴포넌트 추가.
            var existing = FindDoTBySource(context.target.gameObject, source);
            if (existing != null)
            {
                existing.Refresh(tickDamage, duration, tickInterval);
                return;
            }

            var dot = context.target.gameObject.AddComponent<DoTEffect>();
            dot.Initialize(source, tickDamage, duration, tickInterval);
        }

        private static DoTEffect FindDoTBySource(GameObject go, string source)
        {
            var all = go.GetComponents<DoTEffect>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Source == source) return all[i];
            }
            return null;
        }
    }

    /// <summary>
    /// 지속 피해 컴포넌트. 적에 동적 부착.
    /// 틱 간격마다 데미지를 주고, 지속시간 만료 시 자동 제거.
    /// 하나의 적에 여러 DoTEffect 가 source 로 구분되어 공존 가능.
    /// </summary>
    public class DoTEffect : MonoBehaviour
    {
        private string source;
        private int tickDamage;
        private float duration;
        private float tickInterval;
        private float tickTimer;
        private float aliveTime;

        public string Source => source;

        public void Initialize(string source, int tickDamage, float duration, float tickInterval)
        {
            this.source = source;
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
