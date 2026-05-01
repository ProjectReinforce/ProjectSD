using Photon.Pun;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Voice
{
    /// <summary>
    /// 팀원 행의 마이크 활성도 인디케이터 (R14, Discord 패턴).
    /// Speaker.IsPlaying (voice frame 수신 중) 으로 활성/비활성 판정 → 아이콘 색 토글.
    ///
    /// 마이크 없는 유저 / 음소거 중 / PTT 잠시 안 말하는 중 → 모두 idleColor 로 동일 표현 (UX 일관).
    /// 슬라이더 자체는 항상 인터랙션 가능 — 미래 송신 대비 미리 설정 가능.
    ///
    /// 사용:
    ///   - TeammateVoiceRow.prefab 에 마이크 아이콘 Image 추가
    ///   - 본 컴포넌트 부착, micIcon 슬롯에 Image 드래그
    ///   - VoicePanelController.AddRow 가 row 인스턴스화 후 BindActor(player.ActorNumber) 호출
    /// </summary>
    public class MicActivityIndicator : MonoBehaviour
    {
        [SerializeField] private Image micIcon;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private Color activeColor = new Color(0.4f, 1f, 0.5f, 1f);

        private int boundActorNumber = -1;
        private Speaker cachedSpeaker;

        public void BindActor(int actorNumber)
        {
            boundActorNumber = actorNumber;
            cachedSpeaker = null; // 다음 Update 에서 lazy lookup
            Apply(false);
        }

        public void Unbind()
        {
            boundActorNumber = -1;
            cachedSpeaker = null;
            Apply(false);
        }

        private void Update()
        {
            if (boundActorNumber < 0) return;

            // Unity 의 fake-null: destroy 된 Speaker 도 == null 반환 → 재탐색.
            if (cachedSpeaker == null)
                cachedSpeaker = FindSpeakerForActor(boundActorNumber);

            bool active = cachedSpeaker != null && cachedSpeaker.IsPlaying;
            Apply(active);
        }

        private static Speaker FindSpeakerForActor(int actorNumber)
        {
            foreach (var pv in PhotonNetwork.PhotonViewCollection)
            {
                if (pv == null || pv.Owner == null) continue;
                if (pv.Owner.ActorNumber != actorNumber) continue;
                var speaker = pv.GetComponentInChildren<Speaker>();
                if (speaker != null) return speaker;
            }
            return null;
        }

        private void Apply(bool active)
        {
            if (micIcon == null) return;
            micIcon.color = active ? activeColor : idleColor;
        }
    }
}
