using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Features.Pickup.Adapter;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Features.Voice.Adapter.Data;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// R3 마이크 필터 드랍 픽업. 자석/물약 패턴 — RequiresInteraction=false (즉시 발동).
    ///
    /// 호스트 권위:
    ///   - 줍는 사람 즉시 자기 트리거에서 OnPickedUpByPlayer 호출 (호스트만)
    ///   - 호스트가 활성 플레이어 중 랜덤 1명 선택 (본인 포함)
    ///   - 랜덤 필터 인덱스 롤 후 DropSpawner.RaiseMicFilterApplied 로 RPC.
    ///
    /// 시각 표시 없음(설계 결정 6번):
    ///   - 본인은 자기 음성을 못 들음 (Photon Voice self-mute) → 다른 사람 반응에서 깨닫는 게 카오스 재미
    /// </summary>
    public class MicFilterPickup : PickupItemBase
    {
        private void Reset()
        {
            itemId = "micfilter";
            type = PickupType.MicFilter;
            rarity = Rarity.Common;
        }

        protected override void OnPickedUpByPlayer(GameObject playerObj)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var db = GameManager.Instance?.MicFilterDB;
            if (db == null || db.Count == 0)
            {
                Debug.LogWarning("[MicFilterPickup] MicFilterDB 비었거나 미할당 — 효과 없이 픽업만 소비.");
                return;
            }

            int targetActor = PickRandomActorNumber();
            if (targetActor <= 0)
            {
                Debug.LogWarning("[MicFilterPickup] 활성 플레이어 미발견 — 효과 없이 픽업만 소비.");
                return;
            }

            int filterIdx = Random.Range(0, db.Count);
            var data = db.GetByIndex(filterIdx);
            float duration = data != null ? data.durationSeconds : 15f;

            DropSpawner.Instance?.RaiseMicFilterApplied(targetActor, filterIdx, duration);
        }

        /// <summary>
        /// 룸의 활성 플레이어 중 랜덤 1명 ActorNumber. 본인 포함.
        /// PhotonNetwork.PlayerList 는 모든 룸 멤버 — 같은 룸이면 모두 후보.
        /// </summary>
        private int PickRandomActorNumber()
        {
            Player[] players = PhotonNetwork.PlayerList;
            if (players == null || players.Length == 0) return -1;
            int idx = Random.Range(0, players.Length);
            return players[idx].ActorNumber;
        }
    }
}
