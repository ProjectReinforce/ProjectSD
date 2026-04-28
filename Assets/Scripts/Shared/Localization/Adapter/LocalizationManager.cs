using System;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    /// <summary>
    /// ILocalizationService 의 기본 구현. SO 캐시 직조회 — 동기 API.
    ///
    /// WHY 동기: Survivors-like 는 매 프레임 다수 데미지 팝업/HUD 갱신 발생.
    ///   비동기면 깜빡임/race 위험. SO 메모리 상주 + Dictionary O(1) 조회로 충분.
    /// </summary>
    public class LocalizationManager : ILocalizationService
    {
        private readonly LocalizationTable table;

        public Locale CurrentLocale { get; private set; }
        public event Action OnLocaleChanged;

        public LocalizationManager(LocalizationTable table, Locale initialLocale)
        {
            this.table = table;
            this.table?.RebuildLookup();
            CurrentLocale = initialLocale;
        }

        public string Get(string key)
        {
            if (table == null) return key;
            return table.Get(key, CurrentLocale);
        }

        public string Get(string key, Locale locale)
        {
            if (table == null) return key;
            return table.Get(key, locale);
        }

        public string GetFormat(string key, params (string name, object value)[] args)
        {
            string raw = Get(key);
            if (args == null || args.Length == 0) return raw;

            // {name} → 값 치환. C# string.Format 의 {0} 인덱스 형식 미지원 (어순 차이로 깨짐).
            for (int i = 0; i < args.Length; i++)
            {
                var (name, value) = args[i];
                if (string.IsNullOrEmpty(name)) continue;
                raw = raw.Replace("{" + name + "}", value?.ToString() ?? string.Empty);
            }
            return raw;
        }

        public void SetLocale(Locale locale)
        {
            if (CurrentLocale == locale) return;
            CurrentLocale = locale;
            OnLocaleChanged?.Invoke();
            Debug.Log($"[Localization] Locale changed to {locale}");
        }
    }
}
