using System;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    /// <summary>
    /// 키 ↔ Locale별 텍스트 매핑 SO. Editor 임포터(LocalizationSheetImporter)가 채움.
    /// 런타임은 읽기 전용. Dictionary lookup 캐시는 OnEnable / RebuildLookup 시 생성.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable",
                     menuName = "ProjectSD/Data/LocalizationTable")]
    public class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            [TextArea] public string ko;
            [TextArea] public string en;
            [TextArea] public string ja;
            [TextArea] public string zh;
            public string note;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, Entry> lookup;

        public IReadOnlyList<Entry> Entries => entries;

        private void OnEnable() => RebuildLookup();

        public void RebuildLookup()
        {
            lookup = new Dictionary<string, Entry>(entries.Count);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.key)) continue;
                lookup[e.key] = e;
            }
        }

        public string Get(string key, Locale locale)
        {
            if (lookup == null) RebuildLookup();
            if (string.IsNullOrEmpty(key)) return key;
            if (!lookup.TryGetValue(key, out var entry)) return key;

            // Fallback 우선순위: 현 Locale → EN → KO → key 자체.
            string primary = PickColumn(entry, locale);
            if (!string.IsNullOrEmpty(primary)) return primary;
            if (!string.IsNullOrEmpty(entry.en)) return entry.en;
            if (!string.IsNullOrEmpty(entry.ko)) return entry.ko;
            return key;
        }

        private static string PickColumn(Entry e, Locale locale) => locale switch
        {
            Locale.KO_KR => e.ko,
            Locale.EN_US => e.en,
            Locale.JA_JP => e.ja,
            Locale.ZH_CN => e.zh,
            _ => e.en,
        };

#if UNITY_EDITOR
        // Editor 임포터 전용 — 런타임 코드 사용 금지.
        public List<Entry> EditorEntries => entries;
#endif
    }
}
