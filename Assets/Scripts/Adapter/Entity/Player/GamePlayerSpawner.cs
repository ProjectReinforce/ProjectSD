using Photon.Pun;
using UnityEngine;
using Adapter.Manager;

namespace SwDreams.Adapter.Entity.Player
{
    /// <summary>
    /// 정상 플로우 플레이어 스포너.
    /// 대기실에서 선택한 캐릭터 정보를 기반으로 플레이어를 스폰.
    ///
    /// 동작 흐름:
    ///   1. AutomaticallySyncScene = false (결과창 이후 독립 전환 대비)
    ///   2. ready 초기화 (대기실 복귀 시 카운트다운 즉시 시작 방지)
    ///   3. CustomProperties에서 characterId 읽기
    ///   4. PhotonNetwork.Instantiate + instantiationData로 characterId 전달
    ///   5. 호스트: GameState → Playing
    /// </summary>
    public class GamePlayerSpawner : MonoBehaviour
    {
        [SerializeField] private string playerPrefabPath = "Player";
        [SerializeField] private float spawnRadius = 2f;

        [Tooltip("characterId를 찾지 못할 때 사용할 기본 캐릭터 ID")]
        [SerializeField] private int fallbackCharacterId = 0;

        private void Start()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[GamePlayerSpawner] Photon 방에 접속되지 않았습니다.");
                return;
            }

            // ready 초기화 (대기실 복귀 시 이전 ready 잔존 방지)
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.SetLocalReady(false);

            SpawnLocalPlayer();

            // 호스트가 게임 시작 상태로 전환
            if (PhotonNetwork.IsMasterClient)
            {
                SwDreams.Adapter.Manager.GameManager.Instance?.ChangeStateNetwork(
                    SwDreams.Adapter.Manager.GameManager.GameState.Playing);
            }
        }

        private void SpawnLocalPlayer()
        {
            int characterId = GetLocalCharacterId();

            var random2D = Random.insideUnitCircle * spawnRadius;
            var spawnPosition = new Vector3(random2D.x, random2D.y, 0f);

            // instantiationData로 characterId 전달.
            // 모든 클라이언트에서 photonView.InstantiationData[0]으로 접근 가능.
            // → PlayerStub.TryInitializeFromInstantiationData()에서 읽음.
            object[] instantiationData = new object[] { characterId };

            PhotonNetwork.Instantiate(
                playerPrefabPath,
                spawnPosition,
                Quaternion.identity,
                0,
                instantiationData
            );

            Debug.Log($"[GamePlayerSpawner] 플레이어 스폰 (CharacterID: {characterId})");
        }

        /// <summary>
        /// 대기실에서 설정한 characterId를 CustomProperties에서 읽기.
        /// NetworkManager.SetLocalCharacter()로 저장된 값.
        /// </summary>
        private int GetLocalCharacterId()
        {
            if (NetworkManager.Instance != null &&
                NetworkManager.Instance.TryGetCharacterId(PhotonNetwork.LocalPlayer, out int id))
            {
                return id;
            }

            Debug.LogWarning($"[GamePlayerSpawner] characterId를 찾을 수 없습니다. " +
                             $"기본값({fallbackCharacterId}) 사용.");
            return fallbackCharacterId;
        }
    }
}
