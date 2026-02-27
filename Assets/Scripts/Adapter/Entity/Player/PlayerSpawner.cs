using Photon.Pun;
using UnityEngine;

namespace Adapter.Entity.Player
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private string playerPrefabPath = "Player";
        [SerializeField] private float spawnRadius = 2f;

        private void Start()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("PlayerSpawner: not in room.");
                return;
            }

            SpawnLocalPlayer();
        }

        private void SpawnLocalPlayer()
        {
            var random2D = Random.insideUnitCircle * spawnRadius;
            var spawnPosition = new Vector3(random2D.x, random2D.y, 0f);
            PhotonNetwork.Instantiate(playerPrefabPath, spawnPosition, Quaternion.identity);
        }
    }
}
