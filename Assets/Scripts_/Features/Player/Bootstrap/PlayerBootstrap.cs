using Features.Combat.Bootstrap;
using Features.Projectile.Bootstrap;
using Features.Skill.Bootstrap;
using Features.Skill.Infrastructure;
using Features.Zone.Bootstrap;
using Photon.Pun;
using Shared.EventBus;
using UnityEngine;

namespace Features.Player.Bootstrap
{
    public sealed class GameSceneBootstrap : MonoBehaviourPunCallbacks
    {
        [Header("Player")]
        [SerializeField]
        private string _playerPrefabName = "PlayerCharacter";

        [SerializeField]
        private float _spawnRadius = 3f;

        [SerializeField]
        private Transform _cam;

        [SerializeField]
        private SkillSetup _skillSetup;

        [SerializeField]
        private ProjectileSpawner _projectileSpawner;

        [SerializeField]
        private CombatBootstrap _combatBootstrap;

        [SerializeField]
        private ZoneSetup _zoneSetup;

        private EventBus _eventBus;

        private void Start()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[GameScene] Not in a Photon room.");
                return;
            }

            _eventBus = new EventBus();

            if (_combatBootstrap == null)
            {
                Debug.LogError("[GameScene] CombatBootstrap reference is missing.");
                return;
            }

            _combatBootstrap.Initialize(_eventBus);

            var offset = Random.insideUnitCircle * _spawnRadius;
            var spawnPosition = new Vector3(offset.x, 0f, offset.y);
            var player = PhotonNetwork.Instantiate(
                _playerPrefabName,
                spawnPosition,
                Quaternion.identity
            );
            _cam.SetParent(player.transform, false);

            ConnectPlayer(player);
            _skillSetup.Initialize(_eventBus, player.transform);
            _projectileSpawner.Initialize(_eventBus, _eventBus);

            if (_zoneSetup == null)
            {
                Debug.LogError("[GameScene] ZoneSetup reference is missing.");
                return;
            }

            _zoneSetup.Initialize(_eventBus);

            foreach (var other in PhotonNetwork.PlayerListOthers)
                StartCoroutine(ConnectRemotePlayerDelayed(other));
        }

        private void ConnectPlayer(GameObject player)
        {
            var playerSetup = player.GetComponent<PlayerSetup>();
            if (playerSetup != null)
                playerSetup.Initialize(_eventBus);
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            StartCoroutine(ConnectRemotePlayerDelayed(newPlayer));
        }

        private System.Collections.IEnumerator ConnectRemotePlayerDelayed(
            Photon.Realtime.Player target
        )
        {
            yield return null;
            foreach (var pv in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
            {
                if (pv.Owner == target && pv.GetComponent<PlayerSetup>() != null)
                {
                    ConnectPlayer(pv.gameObject);
                    break;
                }
            }
        }
    }
}
