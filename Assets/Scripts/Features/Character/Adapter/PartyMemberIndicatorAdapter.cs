using Photon.Pun;
using UnityEngine;
using SwDreams.Features.UI.Adapter.Indicator;

namespace SwDreams.Features.Character.Adapter
{
    [RequireComponent(typeof(PhotonView))]
    public class PartyMemberIndicatorAdapter : MonoBehaviour, IWorldIndicatorTarget
    {
        private PhotonView pv;
        private bool registered;

        public Transform Transform => transform;
        public string DisplayName  => pv != null && pv.Owner != null ? pv.Owner.NickName : "Player";
        public Color IndicatorColor => pv != null && pv.Owner != null
            ? PlayerColorPalette.Get(pv.Owner.ActorNumber)
            : Color.white;
        public IndicatorPolicy Policy => IndicatorPolicy.AlwaysShow;
        public bool IsActive => true;

        private void Awake() => pv = GetComponent<PhotonView>();

        private void Start()
        {
            if (pv == null || pv.IsMine) return;
            WorldIndicatorManager.RegisterTarget(this);
            registered = true;
        }

        private void OnDestroy()
        {
            if (registered) WorldIndicatorManager.UnregisterTarget(this);
        }
    }
}
