using Features.Projectile.Application.Events;
using Features.Skill.Application;
using Features.Skill.Application.Events;
using Shared.Context;
using Shared.Kernel;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    public sealed class SkillTestBootstrap : MonoBehaviour
    {
        [SerializeField] private SceneContext _sceneContext;

        private void Awake()
        {
            if (_sceneContext == null)
            {
                Debug.LogError("[SkillTest] SceneContext reference is missing.");
                return;
            }

            var publisher = _sceneContext.Publisher;
            var subscriber = _sceneContext.Subscriber;

            subscriber.Subscribe<SkillCastedEvent>(OnSkillCasted);
            subscriber.Subscribe<ProjectileRequestedEvent>(OnProjectileRequested);

            var useCase = new CastSkillUseCase(publisher);
            var casterId = Shared.Kernel.EntityId.New();
            var currentTime = 100f;
            var lastCastTime = -999f;

            var skills = new[]
            {
                SkillCatalog.Fireball(),
                SkillCatalog.IceLance(),
                SkillCatalog.Blizzard(),
                SkillCatalog.Earthquake(),
                SkillCatalog.Smite(),
                SkillCatalog.ShadowBolt(),
                SkillCatalog.HealingSurge(),
                SkillCatalog.IronSkin(),
            };

            foreach (var skill in skills)
            {
                var result = useCase.Execute(skill, casterId, currentTime, lastCastTime);

                if (result.IsFailure)
                    Debug.LogWarning($"[SkillTest] FAILED: {result.Error}");
            }
        }

        private void OnSkillCasted(SkillCastedEvent e)
        {
            Debug.Log($"[SkillTest] Cast OK — skill={e.SkillId} caster={e.CasterId} dmg={e.Spec.Damage}");
        }

        private void OnProjectileRequested(ProjectileRequestedEvent e)
        {
            Debug.Log($"[SkillTest] Projectile requested — owner={e.OwnerId} speed={e.Spec.Speed} trajectory={e.Spec.TrajectoryType}");
        }

        private void OnDestroy()
        {
            if (_sceneContext != null)
            {
                _sceneContext.Subscriber.Unsubscribe<SkillCastedEvent>(OnSkillCasted);
                _sceneContext.Subscriber.Unsubscribe<ProjectileRequestedEvent>(OnProjectileRequested);
            }
        }
    }
}
