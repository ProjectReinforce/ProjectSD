using Photon.Pun;
using UnityEngine;

namespace Features.Player.Bootstrap
{
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private string _playerPrefabName = "PlayerCharacter";
        [SerializeField] private float _spawnRadius = 3f;

        private void Start()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[Player] PlayerSpawner: not in a Photon room.");
                return;
            }

            var offset = Random.insideUnitCircle * _spawnRadius;
            var spawnPosition = new Vector3(offset.x, 0f, offset.y);
            PhotonNetwork.Instantiate(_playerPrefabName, spawnPosition, Quaternion.identity);
        }
    }
}
