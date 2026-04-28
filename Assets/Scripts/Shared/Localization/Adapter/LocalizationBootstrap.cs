using System;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    /// <summary>
    /// 진입점 싱글턴. MenuScene 의 NetworkManager GameObject (또는 별도 GameObject) 에 부착.
    ///
    /// 책임:
    ///   - LocalizationManager 인스턴스 생성 (CurrentLocale 정적 접근 경로)
    ///   - LocaleFontMap 정적 노출 (LocalizedText 가 폰트 교체 시 사용)
    ///
    /// SettingsManager (R12) 와의 결선:
    ///   - 부팅 시 PlayerPrefs ("settings.locale") 를 직접 읽음 — SettingsManager Awake 순서 의존성 회피.
    ///     SettingsManager 와 동일 키를 공유하므로 다음 부팅 시 동기화 보장.
    ///   - 사용자 전환 시 SettingsManager.SetLocale → Service.SetLocale 호출이 정방향 흐름.
    ///
    /// Race 대응:
    ///   - LocalizedText.OnEnable 이 본 컴포넌트의 Awake 보다 먼저 실행될 수 있음 (서로 다른 GameObject).
    ///     OnInitialized 정적 이벤트로 늦게 깬 LocalizedText 가 구독해 갱신 가능.
    /// </summary>
    public class LocalizationBootstrap : MonoBehaviour
    {
        public static LocalizationBootstrap Instance { get; private set; }
        public static ILocalizationService Service { get; private set; }

        /// <summary>Service 가 막 초기화된 직후 1회 발생. 이미 초기화된 시점에 구독한 측은 즉시 호출 (아래 헬퍼).</summary>
        public static event Action OnInitialized;

        /// <summary>구독 시점에 Service 가 이미 있으면 즉시 콜백 1회, 아니면 OnInitialized 구독.</summary>
        public static void SubscribeWhenReady(Action callback)
        {
            if (callback == null) return;
            if (Service != null) callback();
            else OnInitialized += callback;
        }

        public static void UnsubscribeWhenReady(Action callback)
        {
            if (callback == null) return;
            OnInitialized -= callback;
        }

        // SettingsManager.KeyLocale 와 일치 — 두 컴포넌트 모두 직접 읽기 위해 키 공유.
        private const string PrefKey_Locale = "settings.locale";

        [SerializeField] private LocalizationTable table;
        [SerializeField] private LocaleFontMap fontMap;
        [SerializeField] private Locale defaultLocale = Locale.KO_KR;

        public LocaleFontMap FontMap => fontMap;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (table == null)
            {
                Debug.LogError("[Localization] LocalizationTable 미할당 — Service 미초기화. 인스펙터에서 SO 할당 필요.");
                return;
            }

            var saved = LoadSavedLocale(defaultLocale);
            Service = new LocalizationManager(table, saved);
            Debug.Log($"[Localization] Initialized: {saved}");

            // 본 컴포넌트의 Awake 보다 먼저 OnEnable 한 LocalizedText 들 일괄 갱신.
            var handler = OnInitialized;
            OnInitialized = null; // 1회용 — 이후 구독자는 SubscribeWhenReady 의 즉시 콜백 경로 사용.
            handler?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Service = null;
            Instance = null;
            OnInitialized = null;
        }

        private Locale LoadSavedLocale(Locale fallback)
        {
            int v = PlayerPrefs.GetInt(PrefKey_Locale, -1);
            if (v < 0) return fallback;
            // enum 범위 검증 — 신규 빌드에서 enum 축소된 경우 대비.
            return System.Enum.IsDefined(typeof(Locale), v) ? (Locale)v : fallback;
        }
    }
}
