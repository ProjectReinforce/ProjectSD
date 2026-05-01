using TMPro;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    /// <summary>
    /// TMP_Text 옆에 붙어 키로 자동 텍스트 갱신 + Locale 변경 시 폰트 교체.
    /// 정적 키만 지원 — 동적 텍스트(데미지 팝업 등)는 호출부에서 직접 Service.GetFormat(...) 사용.
    ///
    /// Phase B 점진 적용: 한 번에 모든 TMP_Text 에 부착할 필요 없음.
    /// 키 비어있으면 Refresh early return — 기존 인스펙터 텍스트 유지(점진 마이그레이션 안전).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private bool applyFontPerLocale = true;

        private TMP_Text tmp;
        private ILocalizationService service;

        public string Key => key;

        private void Awake() => tmp = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            // Bootstrap.Awake 가 본 OnEnable 보다 늦게 실행될 수 있음 → Service 가 준비되면 통지받기.
            LocalizationBootstrap.SubscribeWhenReady(BindAndRefresh);
        }

        private void OnDisable()
        {
            LocalizationBootstrap.UnsubscribeWhenReady(BindAndRefresh);
            if (service != null) service.OnLocaleChanged -= Refresh;
            service = null;
        }

        private void BindAndRefresh()
        {
            // 즉시콜백 + OnInitialized 두 경로에서 중복 호출 가능 → 첫 진입에서만 OnLocaleChanged 등록.
            if (service != null) return;
            service = LocalizationBootstrap.Service;
            if (service == null) return;
            service.OnLocaleChanged += Refresh;
            Refresh();
        }

        public void SetKey(string newKey)
        {
            key = newKey;
            Refresh();
        }

        private void Refresh()
        {
            if (tmp == null || service == null || string.IsNullOrEmpty(key)) return;
            tmp.text = service.Get(key);

            if (applyFontPerLocale)
            {
                var fontMap = LocalizationBootstrap.Instance != null
                    ? LocalizationBootstrap.Instance.FontMap
                    : null;
                var font = fontMap != null ? fontMap.GetFont(service.CurrentLocale) : null;
                if (font != null) tmp.font = font;
            }
        }
    }
}
