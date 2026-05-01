using SwDreams.Features.UI.Adapter.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Voice
{
    /// <summary>
    /// 마이크 민감도 슬라이더 — SettingsManager.Model.micSensitivity 양방향 바인딩 (R14).
    /// 3곳 공통 사용: 설정 패널(R12 SettingsPanelUI 의 기존 슬라이더 외 추가 위치) /
    ///                대기실 Panel_Lobby / 인게임 VoicePanel.
    ///
    /// 한 곳 변경 → SettingsManager.OnMicChanged 발화 → 다른 인스턴스가 SetValueWithoutNotify 갱신.
    /// PlayerPrefs 저장은 SettingsManager 가 담당 (영구 보존).
    ///
    /// 슬라이더 권장 범위: minValue=0.01 (VAD threshold=0 시 OpenMic 송출 차단 함정),
    ///                     maxValue=1.0. R12 SetMicSensitivity 가 어차피 floor 클램프함.
    /// </summary>
    public class MicSensitivitySlider : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        private bool subscribed;

        private void Awake()
        {
            if (slider == null) slider = GetComponentInChildren<Slider>();
        }

        private void OnEnable()
        {
            if (slider == null) return;

            var sm = SettingsManager.Instance;
            if (sm != null)
            {
                slider.SetValueWithoutNotify(sm.Model.micSensitivity);
                sm.OnMicChanged += OnMicChanged;
                subscribed = true;
            }
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnDisable()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(OnSliderChanged);

            if (subscribed)
            {
                var sm = SettingsManager.Instance;
                if (sm != null) sm.OnMicChanged -= OnMicChanged;
                subscribed = false;
            }
        }

        private void OnSliderChanged(float v)
        {
            SettingsManager.Instance?.SetMicSensitivity(v);
        }

        private void OnMicChanged()
        {
            var sm = SettingsManager.Instance;
            if (sm == null || slider == null) return;
            slider.SetValueWithoutNotify(sm.Model.micSensitivity);
        }
    }
}
