using Features.Skill.Domain.Delivery;
using Shared.Kernel;

namespace Features.Skill.Domain
{
    public static class SkillCatalog
    {
        // Projectile
        public static Skill Fireball() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: 50f, cooldown: 2.0f, range: 15f),
            new ProjectileDelivery());

        public static Skill IceLance() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: 30f, cooldown: 1.0f, range: 20f),
            new ProjectileDelivery());

        // Zone
        public static Skill Blizzard() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: 20f, cooldown: 5.0f, range: 8f),
            new ZoneDelivery());

        public static Skill Earthquake() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: 40f, cooldown: 8.0f, range: 10f),
            new ZoneDelivery());

        // Targeted
        public static Skill Smite() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: 60f, cooldown: 3.0f, range: 12f),
            new TargetedDelivery());

        public static Skill ShadowBolt() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: 45f, cooldown: 2.5f, range: 18f),
            new TargetedDelivery());

        // Self
        public static Skill HealingSurge() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: -30f, cooldown: 4.0f, range: 0f),
            new SelfDelivery());

        public static Skill IronSkin() => new Skill(
            EntityId.New(),
            new SkillSpec(damage: 0f, cooldown: 10.0f, range: 0f),
            new SelfDelivery());
    }
}
