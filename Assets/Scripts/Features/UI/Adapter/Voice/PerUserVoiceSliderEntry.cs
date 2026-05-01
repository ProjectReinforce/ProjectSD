using SwDreams.Features.Voice.Adapter;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Voice
{
    /// <summary>
    /// 단일 ActorNumber 의 보이스 볼륨 슬라이더 컨트롤러 (R14).
    /// 대기실 LobbyPlayerEntry 행 / 인게임 좌측 VoicePanel 의 팀원 행 공통.
    ///
    /// PerUserVoiceSettings 와 양방향 바인딩:
    ///   - 슬라이더 → SetVolumeFor(actorNumber, v)
    ///   - 외부 변경(같은 actor 의 다른 곳 슬라이더 등) → SetValueWithoutNotify
    ///
    /// 슬라이더 범위 권장: minValue=0, maxValue=1 (AudioSource.volume cap 이 1 이라 1 초과는 무력).
    /// 1.0 default. 인스펙터에서 설정.
    ///
    /// 자기 자신 행은 호출자(LobbyPlayerEntry/VoicePanelController) 가 SetActive(false) 처리.
    /// 본 컴포넌트 자체는 actorNumber 무관 동일 동작.
    /// </summary>
    public class PerUserVoiceSliderEntry : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        private int boundActorNumber = -1;
        private bool subscribed;

        private void Awake()
        {
            if (slider == null) slider = GetComponentInChildren<Slider>();
        }

        /// <summary>특정 ActorNumber 의 볼륨 슬라이더로 바인딩.</summary>
        public void Bind(int actorNumber)
        {
            Unbind();
            if (slider == null) return;

            boundActorNumber = actorNumber;
            var settings = PerUserVoiceSettings.Instance;
            float current = settings != null ? settings.GetVolumeFor(actorNumber) : 1f;
            slider.SetValueWithoutNotify(current);

            slider.onValueChanged.AddListener(OnSliderChanged);
            if (settings != null)
            {
                settings.OnVolumeChanged += OnExternalChanged;
                subscribed = true;
            }
        }

        public void Unbind()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(OnSliderChanged);

            if (subscribed)
            {
                var settings = PerUserVoiceSettings.Instance;
                if (settings != null) settings.OnVolumeChanged -= OnExternalChanged;
                subscribed = false;
            }
            boundActorNumber = -1;
        }

        private void OnDisable() => Unbind();

        private void OnSliderChanged(float v)
        {
            if (boundActorNumber < 0) return;
            PerUserVoiceSettings.Instance?.SetVolumeFor(boundActorNumber, v);
        }

        private void OnExternalChanged(int actorNumber)
        {
            if (actorNumber != boundActorNumber) return;
            if (slider == null) return;
            var settings = PerUserVoiceSettings.Instance;
            if (settings == null) return;
            slider.SetValueWithoutNotify(settings.GetVolumeFor(actorNumber));
        }
    }
}
