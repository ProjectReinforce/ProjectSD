using UnityEngine;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// 마이크 테스트 서비스 — 자기 마이크 입력을 자기 Speaker 로 즉시 echo.
    ///
    /// Photon Voice 의 Recorder.DebugEchoMode 는 voice 룸 서버 경유라 룸 미가입 시 동작 X.
    /// 본 서비스는 UnityEngine.Microphone 으로 직접 캡처 → AudioSource.loop 로 즉시 재생.
    /// → 메뉴씬/인게임/ParrelSync 의존성 0. 단일 인스턴스로 검증 가능.
    ///
    /// 의도된 사용:
    ///   1. SettingsPanel 의 "마이크 테스트" 토글이 StartTest()/StopTest()
    ///   2. 사용자가 마이크에 말하며 입력 볼륨/감도 직접 확인
    ///   3. 패널 닫기 시 자동 종료 (SettingsPanelUI.OnDisable)
    ///
    /// 주의:
    ///   - 헤드폰 미사용 시 feedback loop 위험. 기본 volume 0.7 로 완화
    ///   - 인게임에서 동시 사용 시 Photon Recorder 와 OS 마이크 디바이스 경합 가능
    /// </summary>
    public class MicTestService : MonoBehaviour
    {
        public static MicTestService Instance { get; private set; }

        private const int SampleRate = 44100;
        private const int ClipLengthSec = 1;
        private const float DefaultVolume = 0.7f;

        private AudioSource source;
        private AudioClip micClip;
        private string deviceName;

        public bool IsTesting { get; private set; }

        /// <summary>인스턴스 미생성 시 자동 생성 + DontDestroyOnLoad. SettingsPanelUI 가 호출.</summary>
        public static MicTestService GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("MicTestService");
            return go.AddComponent<MicTestService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = DefaultVolume;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                StopTest();
                Instance = null;
            }
        }

        /// <summary>마이크 테스트 시작. 디바이스 없거나 권한 거부 시 false.</summary>
        public bool StartTest()
        {
            if (IsTesting) return true;
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[MicTestService] 마이크 디바이스 없음.");
                return false;
            }

            deviceName = Microphone.devices[0];
            micClip = Microphone.Start(deviceName, true, ClipLengthSec, SampleRate);
            if (micClip == null)
            {
                Debug.LogError($"[MicTestService] Microphone.Start 실패: {deviceName}");
                return false;
            }

            source.clip = micClip;
            source.Play();
            IsTesting = true;
            Debug.Log($"[MicTestService] 마이크 테스트 시작: {deviceName}");
            return true;
        }

        public void StopTest()
        {
            if (!IsTesting) return;
            if (source != null) source.Stop();
            if (!string.IsNullOrEmpty(deviceName) && Microphone.IsRecording(deviceName))
                Microphone.End(deviceName);
            if (micClip != null)
            {
                Destroy(micClip);
                micClip = null;
            }
            IsTesting = false;
        }

        public bool Toggle()
        {
            if (IsTesting) { StopTest(); return false; }
            return StartTest();
        }
    }
}
