using Photon.Pun;
using Shared.Math;
using UnityEngine;

namespace Features.Player.Infrastructure
{
    [RequireComponent(typeof(PhotonView))]
    public sealed class PlayerNetworkAdapter : MonoBehaviourPun, IPunObservable
    {
        private Vector3 _networkPosition;
        private Quaternion _networkRotation;
        private float _lerpSpeed = 15f;

        public bool IsMine => photonView.IsMine;

        private void Update()
        {
            if (IsMine) return;

            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * _lerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation, Time.deltaTime * _lerpSpeed);
        }

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
    }
}
