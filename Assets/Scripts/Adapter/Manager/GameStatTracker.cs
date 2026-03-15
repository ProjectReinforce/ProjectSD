using UnityEngine;
using Photon.Pun;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 게임 중 통계 추적. 결과 화면용.
    ///
    /// - 킬 카운트: 호스트에서만 추적 (적 사망은 호스트 판정)
    /// - 사망 횟수: 호스트에서 추적 (RespawnManager.RequestRespawn 호출 횟수)
    ///
    /// 셋업: GameScene에 빈 오브젝트 → GameStatTracker 부착.
    /// 또는 GameManager 오브젝트에 함께 부착.
    /// </summary>
    public class GameStatTracker : MonoBehaviour
    {
        public static GameStatTracker Instance { get; private set; }

        public int TotalKills { get; private set; }
        public int TotalDeaths { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 적 사망 시 호출. 호스트에서만.
        /// SpawnManager.OnEnemyDied()에서 호출하거나,
        /// Enemy.OnDiedWithRef 이벤트를 직접 구독.
        /// </summary>
        public void RecordKill()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            TotalKills++;
        }

        /// <summary>
        /// 플레이어 사망 시 호출. 호스트에서만.
        /// RespawnManager.RequestRespawn()에서 호출.
        /// </summary>
        public void RecordDeath()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            TotalDeaths++;
        }
    }
}
