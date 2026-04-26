using UnityEngine;
using SwDreams.Features.Quest.Domain;
using SwDreams.Features.UI.Adapter.Indicator;

namespace SwDreams.Features.Quest.Adapter
{
    [RequireComponent(typeof(QuestZone))]
    public class QuestIndicatorAdapter : MonoBehaviour, IWorldIndicatorTarget
    {
        [SerializeField] private Color indicatorColor = new Color(0.7f, 0.4f, 1.0f);

        private QuestZone zone;
        private bool registered;

        public Transform Transform => transform;
        public string DisplayName  => zone != null && zone.Data != null ? zone.Data.displayName : "Quest";
        public Color IndicatorColor => indicatorColor;
        public IndicatorPolicy Policy => IndicatorPolicy.OffScreenOnly;

        public bool IsActive
        {
            get
            {
                if (zone == null || zone.Data == null) return false;
                if (!zone.Data.isRandom) return false;
                var s = zone.CurrentState;
                return s == QuestState.Idle || s == QuestState.Waiting || s == QuestState.InProgress;
            }
        }

        private void Awake()
        {
            zone = GetComponent<QuestZone>();
        }

        private void Start()
        {
            if (zone == null)
            {
                Debug.LogWarning("[QuestIndicatorAdapter] QuestZone 컴포넌트 없음.");
                return;
            }
            if (zone.Data == null)
            {
                Debug.LogWarning("[QuestIndicatorAdapter] QuestData 인스펙터 미할당.");
                return;
            }
            if (!zone.Data.isRandom) return;

            WorldIndicatorManager.RegisterTarget(this);
            registered = true;
        }

        private void OnDestroy()
        {
            if (registered) WorldIndicatorManager.UnregisterTarget(this);
        }
    }
}
