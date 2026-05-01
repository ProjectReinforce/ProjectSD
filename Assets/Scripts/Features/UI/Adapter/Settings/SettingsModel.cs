using System;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Features.UI.Adapter.Settings
{
    /// <summary>
    /// 사용자 설정 데이터 VO. PlayerPrefs 직렬화 대상.
    ///
    /// Voice/MicSens/MicInputMode 는 Phase 8-2 Photon Voice 통합 시 Recorder 에 적용.
    /// Locale 은 Phase 8-5 Localization 코어 도입 시 LocalizationBootstrap.Service.SetLocale 로 적용.
    /// 그 전까지는 Save/Load 만 동작 (값은 보존되나 효과는 없음).
    /// </summary>
    [Serializable]
    public sealed class SettingsModel
    {
        // ===== Video =====
        public WindowMode windowMode = WindowMode.FullscreenBorderless;
        public int resWidth = 1920;
        public int resHeight = 1080;

        // ===== Audio =====
        /// <summary>0~1 (선형). LinearToDb 변환 후 Mixer MasterVol.</summary>
        public float masterVolume = 1f;
        /// <summary>0~1 (선형). Mixer BGMVol.</summary>
        public float bgmVolume = 0.5f;
        /// <summary>0~1 (선형). Mixer SFXVol.</summary>
        public float sfxVolume = 0.7f;
        /// <summary>0~2 (선형, 1.0 = 0dB, 2.0 = +6dB). Mixer VoiceGain. Phase 8-2 부터 효과 발생.</summary>
        public float voiceGain = 1f;
        /// <summary>0~1. Photon Voice 2 Recorder.VoiceDetectionThreshold (게이트 임계값, 볼륨 아님). Phase 8-2 부터 효과 발생.</summary>
        public float micSensitivity = 0.05f;
        /// <summary>마이크 입력 모드. PTT 키 바인딩 변경은 별건 작업.</summary>
        public MicInputMode micInputMode = MicInputMode.OpenMic;

        // ===== Language =====
        public Locale locale = Locale.KO_KR;
    }

    /// <summary>창 모드. ExclusiveFullScreen 은 모니터 충돌 위험으로 제외.</summary>
    public enum WindowMode
    {
        FullscreenBorderless = 0,
        Windowed = 1,
    }

    /// <summary>마이크 입력 모드. 인게임 음소거 토글은 모드와 별개 축 (Phase 8-2 HUD).</summary>
    public enum MicInputMode
    {
        OpenMic = 0,
        PushToTalk = 1,
    }
}
