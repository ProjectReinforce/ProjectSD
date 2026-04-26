using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.UI.Presentation.Indicator;

namespace SwDreams.Features.UI.Adapter.Indicator
{
    public class WorldIndicatorManager : MonoBehaviour
    {
        public static WorldIndicatorManager Instance { get; private set; }

        [SerializeField] private GameObject indicatorPrefab;
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Canvas screenCanvas;

        private readonly Dictionary<IWorldIndicatorTarget, WorldIndicatorView> views = new();
        private static readonly List<IWorldIndicatorTarget> pendingTargets = new();

        /// <summary>
        /// 어댑터에서 호출. Manager 가 아직 Awake 전이면 pending 큐에 적재되고, Manager Awake 시 자동 drain.
        /// 씬 배치 객체(QuestZone)와 Manager 의 Awake 순서 race 를 안전하게 처리.
        /// </summary>
        public static void RegisterTarget(IWorldIndicatorTarget target)
        {
            if (target == null) return;
            if (Instance != null)
            {
                Instance.Register(target);
            }
            else
            {
                if (!pendingTargets.Contains(target)) pendingTargets.Add(target);
            }
        }

        public static void UnregisterTarget(IWorldIndicatorTarget target)
        {
            if (target == null) return;
            pendingTargets.Remove(target);
            if (Instance != null) Instance.Unregister(target);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Awake 전에 RegisterTarget 호출된 어댑터들 일괄 등록.
            for (int i = 0; i < pendingTargets.Count; i++)
                Register(pendingTargets[i]);
            pendingTargets.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(IWorldIndicatorTarget target)
        {
            if (target == null || views.ContainsKey(target)) return;
            if (indicatorPrefab == null || worldCanvas == null || screenCanvas == null)
            {
                Debug.LogWarning($"[WorldIndicatorManager] indicatorPrefab/worldCanvas/screenCanvas 미연결. target={target.GetType().Name}");
                return;
            }

            var go = Instantiate(indicatorPrefab);
            var view = go.GetComponent<WorldIndicatorView>();
            if (view == null)
            {
                Debug.LogError("[WorldIndicatorManager] indicatorPrefab 에 WorldIndicatorView 컴포넌트 없음.");
                Destroy(go);
                return;
            }
            view.Initialize(target, worldCanvas, screenCanvas);
            views[target] = view;
        }

        public void Unregister(IWorldIndicatorTarget target)
        {
            if (target == null || !views.TryGetValue(target, out var view)) return;
            if (view != null) Destroy(view.gameObject);
            views.Remove(target);
        }
    }
}
