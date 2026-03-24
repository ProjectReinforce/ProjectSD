using Features.Combat.Domain;
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

        private const string HealthKey = "hp";
        private const string MaxHealthKey = "maxHp";

        public bool IsMine => photonView.IsMine;

        // IPlayerNetworkCallbackPort
        public System.Action<DomainEntityId> OnRemoteJumped { get; set; }
        public System.Action<DomainEntityId, float, DamageType, DomainEntityId> OnRemoteDamaged { get; set; }
        public System.Action<DomainEntityId, DomainEntityId> OnRemoteDied { get; set; }
        public System.Action<DomainEntityId> OnRemoteRespawned { get; set; }
        public System.Action<DomainEntityId, float, float> OnHealthSynced { get; set; }

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

        public void SendJump(DomainEntityId playerId)
        {
            photonView.RPC(nameof(RPC_Jump), RpcTarget.Others, playerId.Value);
        }

        public void SendDamage(DomainEntityId targetId, float damage, DamageType damageType, DomainEntityId attackerId)
        {
            photonView.RPC(nameof(RPC_ApplyDamage), RpcTarget.Others,
                targetId.Value, damage, (int)damageType, attackerId.Value);
        }

        public void SendDeath(DomainEntityId targetId, DomainEntityId killerId)
        {
            photonView.RPC(nameof(RPC_PlayerDied), RpcTarget.Others,
                targetId.Value, killerId.Value);
        }

        public void SendRespawn(DomainEntityId targetId)
        {
            photonView.RPC(nameof(RPC_PlayerRespawn), RpcTarget.Others, targetId.Value);
        }

        public void SyncHealth(DomainEntityId targetId, float currentHp, float maxHp)
        {
            if (photonView == null || !photonView.IsMine) return;

            var props = new ExitGames.Client.Photon.Hashtable
            {
                { HealthKey, currentHp },
                { MaxHealthKey, maxHp }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        [PunRPC]
        private void RPC_Jump(string playerIdValue)
        {
            var playerId = new DomainEntityId(playerIdValue);
            OnRemoteJumped?.Invoke(playerId);
        }

        [PunRPC]
        private void RPC_ApplyDamage(string targetIdValue, float damage, int damageTypeInt, string attackerIdValue)
        {
            var targetId = new DomainEntityId(targetIdValue);
            var attackerId = new DomainEntityId(attackerIdValue);
            var damageType = (DamageType)damageTypeInt;
            OnRemoteDamaged?.Invoke(targetId, damage, damageType, attackerId);
        }

        [PunRPC]
        private void RPC_PlayerDied(string targetIdValue, string killerIdValue)
        {
            var targetId = new DomainEntityId(targetIdValue);
            var killerId = new DomainEntityId(killerIdValue);
            OnRemoteDied?.Invoke(targetId, killerId);
        }

        [PunRPC]
        private void RPC_PlayerRespawn(string targetIdValue)
        {
            var targetId = new DomainEntityId(targetIdValue);
            OnRemoteRespawned?.Invoke(targetId);
        }
    }
}
