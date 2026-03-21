using Photon.Pun;
using UnityEngine;

namespace Features.Player.Bootstrap
{
    public sealed class GameSceneBootstrap : MonoBehaviour
    {
        [SerializeField]
        private string _playerPrefabName = "PlayerCharacter";

        [SerializeField]
        private float _spawnRadius = 3f;

        [SerializeField]
        private Transform cam;

        private void Start()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[Player] Not in a Photon room.");
                return;
            }

            var offset = Random.insideUnitCircle * _spawnRadius;
            var spawnPosition = new Vector3(offset.x, 0f, offset.y);
            var player = PhotonNetwork.Instantiate(
                _playerPrefabName,
                spawnPosition,
                Quaternion.identity
            );
            cam.SetParent(player.transform, false);
        }
    }
}
