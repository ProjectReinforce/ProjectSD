using Features.Player.Application.Ports;
using Photon.Pun;
using Shared.Kernel;
using UnityEngine;

namespace Features.Player.Infrastructure
{
    [RequireComponent(typeof(PhotonView))]
    public sealed class PlayerNetworkAdapter : MonoBehaviourPun, IPunObservable,
        IPlayerNetworkCommandPort, IPlayerNetworkCallbackPort
    {
        [SerializeField]
        private float _lerpSpeed = 15f;

        private Vector3 _networkPosition;
        private Quaternion _networkRotation;

        public bool IsMine => photonView.IsMine;

        // IPlayerNetworkCallbackPort
        public System.Action<DomainEntityId> OnRemoteJumped { get; set; }

        private void Update()
        {
            if (IsMine) return;

            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * _lerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation, Time.deltaTime * _lerpSpeed);
        }

        // OnPhotonSerializeView — 연속 데이터 (위치, 회전)
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
            }
            else
            {
                _networkPosition = (Vector3)stream.ReceiveNext();
                _networkRotation = (Quaternion)stream.ReceiveNext();
            }
        }

        // IPlayerNetworkCommandPort — RPC 전송
        public void SendJump(DomainEntityId playerId)
        {
            photonView.RPC(nameof(RPC_Jump), RpcTarget.Others, playerId.Value);
        }

        [PunRPC]
        private void RPC_Jump(string playerIdValue)
        {
            var playerId = new DomainEntityId(playerIdValue);
            OnRemoteJumped?.Invoke(playerId);
        }
    }
}
