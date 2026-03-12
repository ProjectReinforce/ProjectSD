using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Features.Skill.Domain;
using Features.Skill.Domain.Delivery;
using Shared.Kernel;

namespace Features.Skill.Bootstrap
{
    public static class SkillCatalog
    {
        // Projectile
        public static Domain.Skill Fireball() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: 50f, cooldown: 2.0f, range: 15f),
            new ProjectileDelivery(new ProjectileSpec(
                TrajectoryType.Linear, HitType.Single, speed: 20f, radius: 0.5f)));

        public static Domain.Skill IceLance() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: 30f, cooldown: 1.0f, range: 20f),
            new ProjectileDelivery(new ProjectileSpec(
                TrajectoryType.Linear, HitType.Piercing, speed: 30f, radius: 0.3f)));

        // Zone
        public static Domain.Skill Blizzard() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: 20f, cooldown: 5.0f, range: 8f),
            new ZoneDelivery());

        public static Domain.Skill Earthquake() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: 40f, cooldown: 8.0f, range: 10f),
            new ZoneDelivery());

        // Targeted
        public static Domain.Skill Smite() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: 60f, cooldown: 3.0f, range: 12f),
            new TargetedDelivery());

        public static Domain.Skill ShadowBolt() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: 45f, cooldown: 2.5f, range: 18f),
            new TargetedDelivery());

        // Self
        public static Domain.Skill HealingSurge() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: -30f, cooldown: 4.0f, range: 0f),
            new SelfDelivery());

        public static Domain.Skill IronSkin() => new Domain.Skill(
            EntityId.New(),
            new SkillSpec(damage: 0f, cooldown: 10.0f, range: 0f),
            new SelfDelivery());
    }
}
