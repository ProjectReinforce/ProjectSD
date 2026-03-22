using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Features.Skill.Domain;
using Features.Skill.Domain.Delivery;
using Shared.Kernel;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "Skill/SkillData")]
    public sealed class SkillData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;
        [TextArea(1, 3)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("Spec")]
        [SerializeField] private float damage;
        [SerializeField] private float cooldown;
        [SerializeField] private float range;

        [Header("Delivery")]
        [SerializeField] private DeliveryType deliveryType;

        [Header("Projectile (only when DeliveryType = Projectile)")]
        [SerializeField] private TrajectoryType trajectoryType;
        [SerializeField] private HitType hitType;
        [SerializeField] private float speed;
        [SerializeField] private float radius;

        [Header("Effects")]
        [SerializeField] private GameObject castEffectPrefab;
        [SerializeField] private AudioClip castSound;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public GameObject CastEffectPrefab => castEffectPrefab;
        public AudioClip CastSound => castSound;

        public Domain.Skill ToDomain()
        {
            var id = new DomainEntityId(skillId);
            var spec = new SkillSpec(damage, cooldown, range);
            var delivery = CreateDelivery();
            return new Domain.Skill(id, spec, delivery);
        }

        private IDeliveryStrategy CreateDelivery()
        {
            switch (deliveryType)
            {
                case DeliveryType.Projectile:
                    return new ProjectileDelivery(
                        new ProjectileSpec(trajectoryType, hitType, speed, radius));
                case DeliveryType.Zone:
                    return new ZoneDelivery();
                case DeliveryType.Targeted:
                    return new TargetedDelivery();
                case DeliveryType.Self:
                    return new SelfDelivery();
                default:
                    Debug.LogError($"[SkillData] Unknown delivery type: {deliveryType}");
                    return new SelfDelivery();
            }
        }
    }
}
