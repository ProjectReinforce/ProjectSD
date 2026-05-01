using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using SwDreams.Features.UI.Adapter.Settings;
using SwDreams.Features.Voice.Adapter;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 설정 패널 UI 컨트롤러. R12 Phase 3.
    ///
    /// 슬라이더/드롭다운/토글의 onValueChanged → SettingsManager.SetXxx 즉시 호출 (미리듣기).
    /// Show() 시 SettingsManager.Model 로 UI 동기화 (SetValueWithoutNotify 로 콜백 재발사 방지).
    /// Close() 시 SettingsManager.Flush() 로 PlayerPrefs 디스크 동기화.
    ///
    /// 진입 경로:
    ///   - TitlePanelController.OnClickSettings (Phase 4)
    ///   - ESC 인게임 일시정지 메뉴 (U4 별건)
    ///
    /// Frame_PopUp 미작성 상태 — 자체 CanvasGroup + DOTween 페이드. 도입 시 일괄 이관.
    ///
    /// Hierarchy (사용자 prefab 작업 가이드):
    ///   SettingsPanel (CanvasGroup + 본 스크립트)
    ///   ├─ Background (Image 반투명, full screen)
    ///   ├─ Content
    ///   │   ├─ Title
    ///   │   ├─ Video
    ///   │   │   ├─ WindowModeToggle (Toggle)
    ///   │   │   └─ ResolutionDropdown (TMP_Dropdown)
    ///   │   ├─ Audio
    ///   │   │   ├─ MasterSlider (Slider 0~1)
    ///   │   │   ├─ BgmSlider (0~1)
    ///   │   │   ├─ SfxSlider (0~1)
    ///   │   │   ├─ VoiceSlider (0~2)
    ///   │   │   ├─ MicSensSlider (0~1)
    ///   │   │   └─ MicModeDropdown (TMP_Dropdown)
    ///   │   ├─ Language
    ///   │   │   └─ LocaleDropdown (TMP_Dropdown)
    ///   │   └─ CloseButton
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SettingsPanelUI : MonoBehaviour
    {
        [Header("Video")]
        [SerializeField] private Toggle windowModeToggle;
        [Tooltip("on=Fullscreen Borderless, off=Windowed")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [Tooltip("0~2 범위 (1.0 = 0dB, 2.0 = +6dB).")]
        [SerializeField] private Slider voiceSlider;
        [SerializeField] private Slider micSensSlider;
        [Tooltip("Open Mic / Push-to-Talk")]
        [SerializeField] private TMP_Dropdown micModeDropdown;
        [Tooltip("자기 마이크가 자기 Speaker 로 echo. 마이크 감도/볼륨 점검용. ParrelSync 불필요.")]
        [SerializeField] private Toggle micTestToggle;

        [Header("Language")]
        [SerializeField] private TMP_Dropdown localeDropdown;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("연출")]
        [SerializeField] private float fadeDuration = 0.2f;

        private CanvasGroup canvasGroup;
        private List<Resolution> resolutionOptions;
        private bool isShown;

        // ===== 자체 라벨 (자기 언어로 표기 — Phase 8-5 키 매핑 대상 외) =====
        private static readonly string[] LocaleLabels = { "한국어", "English", "日本語", "简体中文" };
        private static readonly string[] MicModeLabels = { "오픈 마이크", "Push-to-Talk" };

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            BuildDropdownOptions();
            BindCallbacks();
        }

        private void OnEnable()
        {
            // SetActive(true) 또는 인스펙터에서 직접 활성화한 경우에도 모델 동기화.
            // SettingsManager.Awake 가 아직 안 끝났을 가능성 — 가드는 SyncFromModel 안에 있음.
            if (canvasGroup != null && SettingsManager.Instance != null)
                SyncFromModel();
        }

        private void OnDisable()
        {
            DOTween.Kill(canvasGroup);
        }

        // ===== Public API =====

        public void Show()
        {
            // GameObject 가 비활성이면 자체 활성화 (Button.onClick 인스펙터 단일 호출 지원).
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (isShown) return;
            isShown = true;

            SyncFromModel();

            DOTween.Kill(canvasGroup);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        public void Hide()
        {
            // isShown 가드 제거 — 외부에서 SetActive/alpha 직접 만진 경우에도 닫기 가능하도록.
            isShown = false;

            // 마이크 테스트 진행 중이면 강제 종료 (패널 닫혀도 마이크 잡고 있으면 안 됨)
            if (MicTestService.Instance != null) MicTestService.Instance.StopTest();
            if (micTestToggle != null) micTestToggle.SetIsOnWithoutNotify(false);

            // 닫기 시점에 디스크 동기화
            SettingsManager.Instance?.Flush();

            DOTween.Kill(canvasGroup);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        }

        public bool IsShown => isShown;

        // ===== Setup =====

        private void BuildDropdownOptions()
        {
            // 해상도 — 16:9 동적 옵션
            if (resolutionDropdown != null)
            {
                resolutionOptions = SettingsManager.Get16x9Resolutions();
                resolutionDropdown.ClearOptions();
                var labels = new List<string>(resolutionOptions.Count);
                foreach (var r in resolutionOptions)
                    labels.Add($"{r.width} × {r.height}");
                resolutionDropdown.AddOptions(labels);
            }

            // 마이크 모드
            if (micModeDropdown != null)
            {
                micModeDropdown.ClearOptions();
                micModeDropdown.AddOptions(new List<string>(MicModeLabels));
            }

            // 언어
            if (localeDropdown != null)
            {
                localeDropdown.ClearOptions();
                localeDropdown.AddOptions(new List<string>(LocaleLabels));
            }
        }

        private void BindCallbacks()
        {
            if (windowModeToggle != null)
                windowModeToggle.onValueChanged.AddListener(OnWindowModeChanged);
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (voiceSlider != null) voiceSlider.onValueChanged.AddListener(OnVoiceChanged);
            if (micSensSlider != null) micSensSlider.onValueChanged.AddListener(OnMicSensChanged);
            if (micModeDropdown != null)
                micModeDropdown.onValueChanged.AddListener(OnMicModeChanged);
            if (micTestToggle != null)
                micTestToggle.onValueChanged.AddListener(OnMicTestToggled);

            if (localeDropdown != null)
                localeDropdown.onValueChanged.AddListener(OnLocaleChanged);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
        }

        /// <summary>SettingsManager.Model → UI 동기화. 콜백 재발사 방지를 위해 SetValueWithoutNotify 사용.</summary>
        private void SyncFromModel()
        {
            var sm = SettingsManager.Instance;
            if (sm == null)
            {
                Debug.LogError("[SettingsPanelUI] SettingsManager.Instance null. MenuScene 부착 확인.");
                return;
            }

            var m = sm.Model;

            // Video
            if (windowModeToggle != null)
                windowModeToggle.SetIsOnWithoutNotify(m.windowMode == WindowMode.FullscreenBorderless);
            if (resolutionDropdown != null && resolutionOptions != null)
            {
                int idx = resolutionOptions.FindIndex(r => r.width == m.resWidth && r.height == m.resHeight);
                if (idx < 0) idx = 0;
                resolutionDropdown.SetValueWithoutNotify(idx);
            }

            // Audio
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(m.masterVolume);
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(m.bgmVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(m.sfxVolume);
            if (voiceSlider != null) voiceSlider.SetValueWithoutNotify(m.voiceGain);
            if (micSensSlider != null) micSensSlider.SetValueWithoutNotify(m.micSensitivity);
            if (micModeDropdown != null)
                micModeDropdown.SetValueWithoutNotify((int)m.micInputMode);

            // 마이크 테스트는 일시적 토글 (PlayerPrefs 미저장). 패널 열 때 항상 OFF.
            if (micTestToggle != null)
                micTestToggle.SetIsOnWithoutNotify(false);

            // Language
            if (localeDropdown != null)
                localeDropdown.SetValueWithoutNotify((int)m.locale);
        }

        // ===== Callbacks =====

        private void OnWindowModeChanged(bool isFullscreen)
        {
            SettingsManager.Instance?.SetWindowMode(
                isFullscreen ? WindowMode.FullscreenBorderless : WindowMode.Windowed);
        }

        private void OnResolutionChanged(int idx)
        {
            if (resolutionOptions == null || idx < 0 || idx >= resolutionOptions.Count) return;
            var r = resolutionOptions[idx];
            SettingsManager.Instance?.SetResolution(r.width, r.height);
        }

        private void OnMasterChanged(float v) => SettingsManager.Instance?.SetMasterVolume(v);
        private void OnBgmChanged(float v) => SettingsManager.Instance?.SetBGMVolume(v);
        private void OnSfxChanged(float v) => SettingsManager.Instance?.SetSFXVolume(v);
        private void OnVoiceChanged(float v) => SettingsManager.Instance?.SetVoiceGain(v);
        private void OnMicSensChanged(float v) => SettingsManager.Instance?.SetMicSensitivity(v);

        private void OnMicModeChanged(int idx)
        {
            SettingsManager.Instance?.SetMicInputMode((MicInputMode)idx);
        }

        private void OnMicTestToggled(bool on)
        {
            var svc = MicTestService.GetOrCreate();
            if (on)
            {
                if (!svc.StartTest() && micTestToggle != null)
                {
                    // 시작 실패 시 토글 즉시 원복 (마이크 디바이스/권한 문제)
                    micTestToggle.SetIsOnWithoutNotify(false);
                }
            }
            else
            {
                svc.StopTest();
            }
        }

        private void OnLocaleChanged(int idx)
        {
            SettingsManager.Instance?.SetLocale((Locale)idx);
        }
    }
}
