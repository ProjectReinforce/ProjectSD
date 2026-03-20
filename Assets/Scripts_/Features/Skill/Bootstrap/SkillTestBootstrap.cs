using System.Collections;
using System.Collections.Generic;
using Features.Skill.Application;
using Shared.EventBus;
using Shared.Time;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainSkill = Features.Skill.Domain.Skill;

namespace Features.Skill.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SkillTestBootstrap : MonoBehaviour
    {
        private const float AutoCastIntervalSeconds = 1.5f;

        private readonly EventBus _eventBus = new EventBus();
        private readonly ClockAdapter _clock = new ClockAdapter();
        private readonly Dictionary<string, float> _lastCastTimesBySkillId =
            new Dictionary<string, float>();

        private CastSkillUseCase _castSkillUseCase;
        private DomainSkill[] _skills;
        private Shared.Kernel.DomainEntityId _casterId;
        private int _nextSkillIndex;
        private Coroutine _autoCastRoutine;

        private void Awake()
        {
            _castSkillUseCase = new CastSkillUseCase(_eventBus);
            _skills = new[] { SkillCatalog.Fireball(), SkillCatalog.IceLance() };
            _casterId = Shared.Kernel.DomainEntityId.New();

            var rigView = gameObject.AddComponent<SkillTestRigView>();
            rigView.Initialize(_eventBus, _clock);
        }

        private void Start()
        {
            Debug.Log(
                "[SkillTest] SampleScene test rig ready. Fireball and IceLance will auto-cast toward the dummy target."
            );
            CastNextSkill();
            _autoCastRoutine = StartCoroutine(AutoCastLoop());
        }

        private void OnDestroy()
        {
            if (_autoCastRoutine != null)
            {
                StopCoroutine(_autoCastRoutine);
                _autoCastRoutine = null;
            }
        }

        private IEnumerator AutoCastLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(AutoCastIntervalSeconds);
                CastNextSkill();
            }
        }

        private void CastNextSkill()
        {
            if (_skills == null || _skills.Length == 0)
            {
                Debug.LogWarning("[SkillTest] No skills configured for the test rig.");
                return;
            }

            var skill = _skills[_nextSkillIndex];
            _nextSkillIndex = (_nextSkillIndex + 1) % _skills.Length;

            var lastCastTime = -999f;
            if (_lastCastTimesBySkillId.TryGetValue(skill.Id.Value, out var cachedLastCastTime))
            {
                lastCastTime = cachedLastCastTime;
            }

            var result = _castSkillUseCase.Execute(skill, _casterId, Time.time, lastCastTime);
            if (result.IsFailure)
            {
                Debug.LogWarning($"[SkillTest] FAILED: {result.Error}");
                return;
            }

            _lastCastTimesBySkillId[skill.Id.Value] = Time.time;
        }
    }

    internal static class SkillTestRuntimeInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForSampleScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || (scene.name != "SampleScene" && scene.name != "JG_GameScene"))
            {
                return;
            }

            if (Object.FindFirstObjectByType<SkillTestBootstrap>() != null)
            {
                return;
            }

            var go = new GameObject("SkillTestBootstrap_Auto");
            go.AddComponent<SkillTestBootstrap>();
            Debug.Log($"[SkillTest] Auto-installed test rig in {scene.name}.");
        }
    }
}
