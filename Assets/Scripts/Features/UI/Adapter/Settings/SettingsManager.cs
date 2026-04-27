using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.UI.Adapter.Settings
{
    /// <summary>
    /// 사용자 설정 SSOT. DontDestroyOnLoad 싱글턴.
    ///
    /// 책임:
    ///   - PlayerPrefs Load/Save
    ///   - Setter 즉시 반영 (슬라이더 미리듣기) — AudioManager / Screen 에 즉시 push
    ///   - Phase 8-2 Voice / Phase 8-5 Localization 도입 후 해당 어댑터에도 push
    ///
    /// 패널 닫기 시 Flush() 호출로 디스크 동기화. 드래그 중 PlayerPrefs.Save I/O 폭발 방지.
    ///
    /// 적용 타이밍:
    ///   - Awake: PlayerPrefs Load (Model 채움)
    ///   - Start (1프레임 지연): AudioManager 등 다른 매니저 Awake 후 ApplyAll
    ///
    /// 셋업: MenuScene 에 빈 GameObject "SettingsManager" → 본 컴포넌트 부착.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        public SettingsModel Model { get; private set; }

        public event Action OnAudioChanged;
        public event Action OnVideoChanged;
        public event Action OnLocaleChanged;
        public event Action OnMicChanged;

        // ===== PlayerPrefs Keys (roadmap.md § R12 Phase 2 + localization.md § 8) =====
        private const string KeyWindowMode = "settings.video.windowMode";
        private const string KeyResWidth = "settings.video.resWidth";
        private const string KeyResHeight = "settings.video.resHeight";
        private const string KeyMaster = "settings.audio.master";
        private const string KeyBgm = "settings.audio.bgm";
        private const string KeySfx = "settings.audio.sfx";
        private const string KeyVoice = "settings.audio.voice";
        private const string KeyMicSens = "settings.audio.micSens";
        private const string KeyMicMode = "settings.audio.micMode";
        private const string KeyLocale = "settings.locale";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Model = LoadFromPrefs();
        }

        private IEnumerator Start()
        {
            // 다른 매니저(특히 AudioManager) 의 Awake/Start 가 먼저 끝나도록 1 프레임 양보.
            // AudioManager.Start 의 ApplyInitialVolumes 가 인스펙터 값을 적용하면,
            // 그 위에 PlayerPrefs 값이 덮어쓰여 사용자 설정이 SSOT 가 됨.
            yield return null;
            ApplyAll();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) PlayerPrefs.Save();
        }

        private void OnApplicationQuit() => PlayerPrefs.Save();

        /// <summary>패널 닫기 등 명시적 디스크 동기화 시점.</summary>
        public void Flush() => PlayerPrefs.Save();

        // ===== Apply =====

        public void ApplyAll()
        {
            ApplyVideo();
            ApplyAudio();
            // Locale: Phase 8-5 LocalizationBootstrap 도입 후
            // Voice/Mic: Phase 8-2 Photon Voice 도입 후
        }

        private void ApplyVideo()
        {
            var mode = Model.windowMode == WindowMode.FullscreenBorderless
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            Screen.SetResolution(Model.resWidth, Model.resHeight, mode);
        }

        private void ApplyAudio()
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            am.SetMasterVolume(Model.masterVolume);
            am.SetBGMVolume(Model.bgmVolume);
            am.SetSFXVolume(Model.sfxVolume);
            am.SetVoiceGain(Model.voiceGain);
        }

        // ===== Setters (즉시 반영) =====

        public void SetWindowMode(WindowMode mode)
        {
            Model.windowMode = mode;
            PlayerPrefs.SetInt(KeyWindowMode, (int)mode);
            ApplyVideo();
            OnVideoChanged?.Invoke();
        }

        public void SetResolution(int width, int height)
        {
            Model.resWidth = width;
            Model.resHeight = height;
            PlayerPrefs.SetInt(KeyResWidth, width);
            PlayerPrefs.SetInt(KeyResHeight, height);
            ApplyVideo();
            OnVideoChanged?.Invoke();
        }

        public void SetMasterVolume(float v)
        {
            v = Mathf.Clamp01(v);
            Model.masterVolume = v;
            PlayerPrefs.SetFloat(KeyMaster, v);
            AudioManager.Instance?.SetMasterVolume(v);
            OnAudioChanged?.Invoke();
        }

        public void SetBGMVolume(float v)
        {
            v = Mathf.Clamp01(v);
            Model.bgmVolume = v;
            PlayerPrefs.SetFloat(KeyBgm, v);
            AudioManager.Instance?.SetBGMVolume(v);
            OnAudioChanged?.Invoke();
        }

        public void SetSFXVolume(float v)
        {
            v = Mathf.Clamp01(v);
            Model.sfxVolume = v;
            PlayerPrefs.SetFloat(KeySfx, v);
            AudioManager.Instance?.SetSFXVolume(v);
            OnAudioChanged?.Invoke();
        }

        public void SetVoiceGain(float v)
        {
            v = Mathf.Clamp(v, 0f, 2f);
            Model.voiceGain = v;
            PlayerPrefs.SetFloat(KeyVoice, v);
            AudioManager.Instance?.SetVoiceGain(v);
            OnAudioChanged?.Invoke();
        }

        public void SetMicSensitivity(float v)
        {
            v = Mathf.Clamp01(v);
            Model.micSensitivity = v;
            PlayerPrefs.SetFloat(KeyMicSens, v);
            // Phase 8-2: Recorder.VoiceDetectionThreshold = v
            OnMicChanged?.Invoke();
        }

        public void SetMicInputMode(MicInputMode mode)
        {
            Model.micInputMode = mode;
            PlayerPrefs.SetInt(KeyMicMode, (int)mode);
            // Phase 8-2: VoiceController 가 OnMicChanged 구독하여 모드 전환
            OnMicChanged?.Invoke();
        }

        public void SetLocale(Locale locale)
        {
            Model.locale = locale;
            PlayerPrefs.SetInt(KeyLocale, (int)locale);
            // Phase 8-5: LocalizationBootstrap.Service?.SetLocale(locale)
            OnLocaleChanged?.Invoke();
        }

        // ===== Load =====

        private SettingsModel LoadFromPrefs()
        {
            var m = new SettingsModel();
            m.windowMode = (WindowMode)PlayerPrefs.GetInt(KeyWindowMode, (int)m.windowMode);
            m.resWidth = PlayerPrefs.GetInt(KeyResWidth, m.resWidth);
            m.resHeight = PlayerPrefs.GetInt(KeyResHeight, m.resHeight);
            m.masterVolume = PlayerPrefs.GetFloat(KeyMaster, m.masterVolume);
            m.bgmVolume = PlayerPrefs.GetFloat(KeyBgm, m.bgmVolume);
            m.sfxVolume = PlayerPrefs.GetFloat(KeySfx, m.sfxVolume);
            m.voiceGain = PlayerPrefs.GetFloat(KeyVoice, m.voiceGain);
            m.micSensitivity = PlayerPrefs.GetFloat(KeyMicSens, m.micSensitivity);
            m.micInputMode = (MicInputMode)PlayerPrefs.GetInt(KeyMicMode, (int)m.micInputMode);
            m.locale = (Locale)PlayerPrefs.GetInt(KeyLocale, (int)m.locale);
            return m;
        }

        // ===== Helpers (Phase 3 UI 가 사용) =====

        /// <summary>
        /// 현재 모니터에서 사용 가능한 16:9 비율 해상도 목록 (width 오름차순, 동일 width 면 height).
        /// 1차 빌드는 FHD 강제이지만 코드 미리 구비. 동일 (w,h) 의 다른 refreshRate 는 첫 발견만 보존.
        /// </summary>
        public static List<Resolution> Get16x9Resolutions()
        {
            var seen = new HashSet<long>();
            var list = new List<Resolution>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width * 9 != r.height * 16) continue;
                long key = ((long)r.width << 32) | (uint)r.height;
                if (!seen.Add(key)) continue;
                list.Add(r);
            }
            list.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
            return list;
        }
    }
}
