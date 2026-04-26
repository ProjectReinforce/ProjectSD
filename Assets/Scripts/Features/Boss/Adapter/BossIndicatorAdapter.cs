using UnityEngine;
using SwDreams.Features.UI.Adapter.Indicator;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Boss.Adapter
{
    [RequireComponent(typeof(Boss))]
    public class BossIndicatorAdapter : MonoBehaviour, IWorldIndicatorTarget
    {
        [SerializeField] private string displayName = "Boss";
        [SerializeField] private Color indicatorColor = Color.red;

        private Boss boss;
        private bool registered;

        public Transform Transform => transform;
        public string DisplayName  => displayName;
        public Color IndicatorColor => indicatorColor;
        public IndicatorPolicy Policy => IndicatorPolicy.OffScreenOnly;

        public bool IsActive => boss != null && boss.IsAlive
            && GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameManager.GameState.BossFight;

        private void Awake()
        {
            boss = GetComponent<Boss>();
        }

        private void Start()
        {
            WorldIndicatorManager.RegisterTarget(this);
            registered = true;
        }

        private void OnDestroy()
        {
            if (registered) WorldIndicatorManager.UnregisterTarget(this);
        }
    }
}
