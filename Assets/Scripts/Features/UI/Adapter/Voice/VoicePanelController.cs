using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace SwDreams.Features.UI.Adapter.Voice
{
    /// <summary>
    /// 인게임 좌측 보이스 패널 — 자기 마이크 슬라이더 + 팀원 보이스 슬라이더 리스트 (R14).
    /// 자기 자신은 리스트에서 제외 (자기 목소리 재생 안 함, 자기 볼륨 조절 의미 X).
    ///
    /// Photon 콜백으로 동적 추가/제거 — OnPlayerEnteredRoom / OnPlayerLeftRoom / OnJoinedRoom.
    /// 행 prefab 은 인스펙터에서 할당. 안에 PerUserVoiceSliderEntry + 이름 TMP_Text 필요.
    ///
    /// 셋업: GameScene 의 InGameHUD Canvas 좌측 코너 등에 배치. root 에 CanvasGroup +
    /// VoicePanelHover 부착 (호버 알파 페이드). 자식에 마이크 슬라이더 + 팀원 컨테이너.
    /// </summary>
    public class VoicePanelController : MonoBehaviourPunCallbacks
    {
        [SerializeField] private Transform memberContainer;
        [SerializeField, Tooltip("팀원 한 줄 prefab — PerUserVoiceSliderEntry + 이름 TMP_Text 포함")]
        private GameObject memberRowPrefab;

        // ActorNumber → row GameObject
        private readonly Dictionary<int, GameObject> memberRows = new Dictionary<int, GameObject>();

        public override void OnEnable()
        {
            base.OnEnable();
            // OnEnable 한 곳에서만 빌드 — Start 와 중복 회피.
            // OnJoinedRoom 콜백이 추가 안전망 (패널이 Disable 상태에서 룸 가입 시).
            RebuildRows();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ClearAllRows();
        }

        public override void OnJoinedRoom() => RebuildRows();

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            AddRow(newPlayer);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (otherPlayer == null) return;
            RemoveRow(otherPlayer.ActorNumber);
        }

        private void RebuildRows()
        {
            ClearAllRows();
            if (!PhotonNetwork.InRoom) return;

            int localActor = PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer.ActorNumber
                : -1;

            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p == null) continue;
                if (p.ActorNumber == localActor) continue; // 자기 제외
                AddRow(p);
            }
        }

        private void AddRow(Player player)
        {
            if (player == null) return;
            if (player.IsLocal) return;
            if (memberRows.ContainsKey(player.ActorNumber)) return;
            if (memberContainer == null || memberRowPrefab == null) return;

            var row = Instantiate(memberRowPrefab, memberContainer);

            var nameText = row.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(player.NickName)
                    ? $"Player {player.ActorNumber}"
                    : player.NickName;
            }

            var entry = row.GetComponent<PerUserVoiceSliderEntry>();
            if (entry == null) entry = row.GetComponentInChildren<PerUserVoiceSliderEntry>();
            if (entry != null) entry.Bind(player.ActorNumber);

            // 마이크 활성도 인디케이터 (Discord 패턴) — Speaker.IsPlaying 기반 자동 색 토글.
            var mic = row.GetComponent<MicActivityIndicator>();
            if (mic == null) mic = row.GetComponentInChildren<MicActivityIndicator>();
            if (mic != null) mic.BindActor(player.ActorNumber);

            memberRows[player.ActorNumber] = row;
        }

        private void RemoveRow(int actorNumber)
        {
            if (memberRows.TryGetValue(actorNumber, out var row))
            {
                if (row != null) Destroy(row);
                memberRows.Remove(actorNumber);
            }
        }

        private void ClearAllRows()
        {
            foreach (var kv in memberRows)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            memberRows.Clear();
        }
    }
}
