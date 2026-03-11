using Features.Skill.Application;
using Features.Skill.Application.Events;
using Features.Skill.Domain;
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

            var useCase = new CastSkillUseCase(publisher);
            var casterId = EntityId.New();
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
            Debug.Log($"[SkillTest] Cast OK — {e.DeliveryDescription}");
        }

        private void OnDestroy()
        {
            if (_sceneContext != null)
                _sceneContext.Subscriber.Unsubscribe<SkillCastedEvent>(OnSkillCasted);
        }
    }
}
