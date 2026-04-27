using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Voice.Unity;
using Photon.Voice.PUN;
using SwDreams.Features.UI.Adapter.Settings;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// 플레이어 음성 송출 제어. PlayerStub 프리팹에 Recorder/Speaker/PhotonVoiceView 와 함께 부착.
    ///
    /// 책임:
    ///   - 자기 PhotonView 만 제어 (IsMine 가드)
    ///   - PTT/OpenMic 모드 전환 (SettingsManager.Model.micInputMode 구독)
    ///   - 마이크 감도(VoiceDetectionThreshold) 반영 (SettingsManager.Model.micSensitivity)
    ///   - 음소거 (모드와 별개 축, UI 토글에서 호출)
    ///   - PunVoiceClient 재연결 시 송신 상태 복원
    ///
    /// 책임 외:
    ///   - 입력 게인 (voiceGain) 은 AudioMixer "VoiceGain" 채널에서 처리. Recorder 가 아닌 Speaker 측 출력 단계.
    ///   - PTT 키 바인딩 변경은 SettingsModel 에 없음 (별건). 일단 인스펙터 SerializeField.
    /// </summary>
    [RequireComponent(typeof(Recorder))]
    [RequireComponent(typeof(PhotonView))]
    public class VoiceController : MonoBehaviour
    {
        /// <summary>
        /// 자기 PhotonView (IsMine) 인 인스턴스. UI 버튼이 인스펙터 드래그 없이 호출하기 위함.
        /// 룸 입장 시 등록, 룸 퇴장/씬 전환 시 해제.
        /// </summary>
        public static VoiceController LocalInstance { get; private set; }

        /// <summary>음소거 상태 변경 시 발행. UI 아이콘 토글 등에서 구독.</summary>
        public static event System.Action<bool> OnLocalMuteChanged;

        [Header("Push-to-Talk")]
        [Tooltip("새 Input System 의 Key enum. 기본 V.")]
        [SerializeField] private Key pushToTalkKey = Key.V;

        [Header("Debug")]
        [SerializeField] private bool logStateChange = false;

        private Recorder recorder;
        private PhotonView photonView;

        // 음소거 — 모드와 별개 축. true 면 모드 무관 송출 차단.
        private bool isMuted = false;

        // OpenMic 모드의 활성 상태 (UI 토글로 변경).
        private bool openMicActive = true;

        private MicInputMode currentMode = MicInputMode.OpenMic;

        private void Awake()
        {
            recorder = GetComponent<Recorder>();
            photonView = GetComponent<PhotonView>();

            // 안전 기본값 — 시작 시 송출 OFF (의도치 않은 마이크 송출 방지).
            recorder.TransmitEnabled = false;
        }

        private void OnEnable()
        {
            if (!photonView.IsMine) return;

            LocalInstance = this;

            var sm = SettingsManager.Instance;
            if (sm != null)
            {
                ApplySettings(sm);
                sm.OnMicChanged += OnMicSettingsChanged;
            }
        }

        private void OnDisable()
        {
            if (LocalInstance == this) LocalInstance = null;

            var sm = SettingsManager.Instance;
            if (sm != null) sm.OnMicChanged -= OnMicSettingsChanged;
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            bool desired = ComputeDesiredTransmit();
            if (recorder.TransmitEnabled != desired)
            {
                recorder.TransmitEnabled = desired;
                if (logStateChange) Debug.Log($"[VoiceController] TransmitEnabled = {desired} (mode={currentMode}, muted={isMuted})");
            }
        }

        /// <summary>
        /// 송출 여부 결정 트리.
        ///   - 음소거면 무조건 false
        ///   - PTT: 키 누르고 있는 동안만
        ///   - OpenMic: openMicActive 토글 상태 (Recorder 의 VAD 가 별도로 게이트)
        /// </summary>
        private bool ComputeDesiredTransmit()
        {
            if (isMuted) return false;

            return currentMode switch
            {
                MicInputMode.PushToTalk => IsPttKeyHeld(),
                MicInputMode.OpenMic => openMicActive,
                _ => false,
            };
        }

        private bool IsPttKeyHeld()
        {
            var kb = Keyboard.current;
            return kb != null && kb[pushToTalkKey].isPressed;
        }

        // ===== Public API (UI 토글에서 호출) =====

        /// <summary>인게임 HUD 마이크 토글 버튼. 음소거 ↔ 해제.</summary>
        public void ToggleMute()
        {
            SetMute(!isMuted);
        }

        public void SetMute(bool muted)
        {
            if (isMuted == muted) return;
            isMuted = muted;
            OnLocalMuteChanged?.Invoke(isMuted);
            if (logStateChange) Debug.Log($"[VoiceController] Mute = {isMuted}");
        }

        public bool IsMuted => isMuted;

        /// <summary>OpenMic 모드의 활성 토글 (PTT 모드에선 무시).</summary>
        public void ToggleOpenMic() => openMicActive = !openMicActive;

        public void SetOpenMicActive(bool active) => openMicActive = active;

        // ===== Settings 연동 =====

        private void OnMicSettingsChanged()
        {
            var sm = SettingsManager.Instance;
            if (sm != null) ApplySettings(sm);
        }

        private void ApplySettings(SettingsManager sm)
        {
            currentMode = sm.Model.micInputMode;

            // VoiceDetectionThreshold = 게이트 임계값 (0~1, 높을수록 작은 소리 차단).
            // PTT 모드에선 Recorder 의 VAD 자체 비활성, OpenMic 에선 활성.
            recorder.VoiceDetection = currentMode == MicInputMode.OpenMic;
            recorder.VoiceDetectionThreshold = sm.Model.micSensitivity;

            if (logStateChange) Debug.Log($"[VoiceController] Settings applied: mode={currentMode}, sens={sm.Model.micSensitivity:F2}");
        }
    }
}
