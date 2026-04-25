# Localization — 다국어 텍스트 시스템

Google Sheets 를 작업용 SSOT 로, 빌드타임에 ScriptableObject 로 임포트하여 게임이 사용한다. 본 문서는 **다른 세션이 이 문서만 보고 Phase A 구현을 끝낼 수 있도록** 작성되었다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `localization` |
| 분류 | UI / 인프라 |
| 의존 레이어 | Domain (인터페이스), Adapter (Manager/SO/Editor) |
| 최종 업데이트 | 2026-04-25 |
| 구현 상태 | ⬜ 미구현 (Phase A 문서화 단계) |
| 1차 지원 언어 | KO, EN, JA, ZH-CN (Steam 출시 시점까지) |

## 2. 목적

**문제:** 인게임 텍스트(UI, 스킬 이름/설명, 보스 알림, 결과창 등)가 한국어 하드코딩 상태. Stove(KO) → Steam(글로벌) 출시 라인업상 다국어 지원이 필수. 단, ProjectSD 텍스트량(스킬 24+패시브 19+혼돈 19+UI ≈ 수백 키)이 작아 Unity Localization Package 같은 헤비 솔루션의 강점(Pluralization, ICU MessageFormat)이 죽는다.

**해결:** **자체 LocalizationManager + Key 기반 + 빌드타임 CSV 임포트** 의 가벼운 시스템. Google Sheets 가 작업용 SSOT, 게임은 임포트된 SO 만 사용. Unity Localization Package 의 Addressables 의존·Async API 부담 회피. 인터페이스(`ILocalizationService`) 만 잘 만들어두면 출시 후 언어 7개+로 늘어나 Pluralization 이 필요해진 시점에 백엔드 교체 가능.

**왜 자체 구현:**
- ProjectSD 텍스트량 기준 i18n 고급 기능 거의 불필요
- Feature-first 구조에 자연 통합 (`Shared/Platform/` 패턴과 동일)
- 동기 API → 멀티 게임 첫 프레임 깜빡임 위험 0
- Addressables 안 들어옴 → 빌드 시스템 단순 유지
- Public Sheet CSV export URL → Service Account JSON 발급 불필요

## 3. 1차 지원 언어 (확정)

| Locale | 언어 | 폰트 패밀리 | 출시 단계 | 비고 |
|---|---|---|---|---|
| `ko-KR` | 한국어 | CJK-KR (NotoSansKR 등) | Stove (필수) | Stove 인디 = 한국 우선 |
| `en-US` | English | Latin (NotoSans 등) | Steam 1차 (필수) | Steam 36% 커버 |
| `ja-JP` | 日本語 | CJK-JP (NotoSansJP) | Steam 1차 | 동아시아 매칭 풀 |
| `zh-CN` | 简体中文 | CJK-SC (NotoSansSC) | Steam 1차 | Steam 26% 커버 |

**비범위 (1차):** RU, ES, PT-BR, DE, FR, IT, PL, TR, ZH-TW. Steam 출시 후 리뷰·매칭 풀 보고 2차 결정.

**폰트 정책:** TMP_FontAsset 4종 별도 셋업. 런타임 Locale 전환 시 `LocalizedText` 가 폰트도 함께 교체.

## 4. 폴더 구조

```
Assets/Scripts/Shared/Localization/
├── Domain/                                ← UnityEngine / Photon import 금지
│   ├── ILocalizationService.cs            ← 핵심 인터페이스
│   ├── Locale.cs                          ← enum: KO_KR, EN_US, JA_JP, ZH_CN
│   └── LocalizationKey.cs                 ← 상수 키 카탈로그 (선택, 컴파일 안전성용)
├── Adapter/
│   ├── LocalizationBootstrap.cs           ← MonoBehaviour 싱글턴 (DontDestroyOnLoad)
│   ├── LocalizationManager.cs             ← ILocalizationService 구현, 동기 API
│   ├── LocalizationTable.cs               ← ScriptableObject (키 ↔ Locale별 텍스트)
│   ├── LocaleFontMap.cs                   ← ScriptableObject (Locale ↔ TMP_FontAsset)
│   └── LocalizedText.cs                   ← TMP_Text 옆에 붙는 컴포넌트
└── Editor/
    └── LocalizationSheetImporter.cs       ← CSV 다운로드 + LocalizationTable 갱신
```

**WHY `Shared/Localization/`:** `Shared/Platform/` 과 동일 패턴 — 인프라 레벨이라 단일 Feature 가 아님. UI Feature 만 쓰는 게 아니라 Skill/Boss/Quest 등 모든 Feature 가 텍스트를 노출.

**SO 자산 위치:**
- `Assets/Data/Localization/LocalizationTable.asset` — 임포트 대상
- `Assets/Data/Localization/LocaleFontMap.asset` — 폰트 매핑

## 5. 키 명명 규칙

**형식:** `{scope}.{subscope}.{name}` — 점(.) 구분, 영문 소문자 + 언더스코어.

| 스코프 | 예시 | 출처 |
|---|---|---|
| `ui.menu.*` | `ui.menu.start_button`, `ui.menu.room_list_title` | MenuScene UI |
| `ui.hud.*` | `ui.hud.timer_label`, `ui.hud.level_label` | InGameHUD |
| `ui.levelup.*` | `ui.levelup.title`, `ui.levelup.skip_button` | LevelUpPanel |
| `ui.result.*` | `ui.result.victory`, `ui.result.defeat` | ResultPanel |
| `ui.toast.*` | `ui.toast.disconnect`, `ui.toast.host_migrated` | FrameToast 메시지 |
| `ui.popup.*` | `ui.popup.exit_confirm` | Frame_PopUp 메시지 |
| `skill.{id}.name` | `skill.longsword.name` | SkillData.name |
| `skill.{id}.desc` | `skill.longsword.desc` | SkillData.description |
| `passive.{id}.name` | `passive.regen.name` | PassiveSkillData |
| `chaos.{id}.name` | `chaos.gambler.name` | ChaosSkillData |
| `boss.{id}.name` | `boss.phase1.name`, `boss.phase2_warning` | Boss 전용 |
| `enemy.{id}.name` | `enemy.basic.name` | EnemyData (선택) |
| `essence.{id}.*` | `essence.fire.name` | EssenceData |
| `weapon.{id}.*` | `weapon.dagger.name` | WeaponData |
| `quest.{id}.*` | `quest.kill_30.title`, `quest.kill_30.objective` | QuestData |
| `stat.{type}.name` | `stat.attack_power.name` | StatType enum 텍스트 라벨 |
| `error.*` | `error.connection_lost`, `error.room_full` | 에러 메시지 |

**규칙:**
- 키는 **영문 + 점 + 언더스코어만**. 공백·한글·하이픈 금지 (CSV 파싱 안전성).
- `name` / `desc` / `title` / `objective` 같은 의미 표시는 **항상 마지막 분절**.
- SO 인스펙터에서 키 입력 실수를 줄이려면 `LocalizationKey.cs` 에 자주 쓰는 키 상수화 (선택).

**포맷 인자 (named placeholder):**
- `"{playerName} 이(가) {damage} 데미지를 입혔다"` — `{이름}` 형식만 허용. C# `string.Format` 의 `{0}` 인덱스 형식 **금지** (언어별 어순 차이로 깨짐).
- 런타임: `LocalizationManager.GetFormat("event.player_dealt_damage", ("playerName", "Alice"), ("damage", 42))`.

## 6. Google Sheets 포맷

**시트 구조 (1 시트 = 전체 키):**

| key | ko_auto | en_auto | ja_auto | zh_auto | ko_final | en_final | ja_final | zh_final | note |
|---|---|---|---|---|---|---|---|---|---|
| `ui.menu.start_button` | 시작 | =GOOGLETRANSLATE(B2,"ko","en") | =GOOGLETRANSLATE(B2,"ko","ja") | =GOOGLETRANSLATE(B2,"ko","zh-CN") | 시작 | Start | スタート | 开始 | |
| `skill.longsword.name` | 장검 | =GOOGLETRANSLATE(...) | ... | ... | 장검 | Longsword | 長剣 | 长剑 | 고유명사, 검수 필요 |
| `event.player_dealt_damage` | {playerName} 이(가) {damage} 데미지 | ... | ... | ... | {playerName} 이(가) {damage} 데미지 | {playerName} dealt {damage} damage | {playerName}が{damage}ダメージ | {playerName}造成{damage}伤害 | placeholder 보존 검증 필요 |

**컬럼 의미:**
- `key` — 게임 코드가 참조하는 키
- `*_auto` — `GOOGLETRANSLATE()` 자동 번역 (초기 더미). **게임은 안 읽음.**
- `*_final` — 검수 완료 컬럼. **게임은 이것만 읽음.**
- `note` — 검수자/번역자 메모 (게임 미사용)

**WHY 이중 컬럼:** 자동 번역은 도메인 용어("혼돈", "정수")에서 어색하게 나옴. `*_final` 을 분리하면 검수 전후 상태가 시트에서 즉시 보이고, 검수자가 `*_auto` 를 참조해 빠르게 작업 가능.

**시트 공유 설정:**
- 보기 권한: "링크가 있는 모든 사용자 (보기 가능)"
- 편집 권한: 번역가/디자이너에게 개별 부여
- **Service Account JSON 불필요** — public read URL 로 다운로드.

**다운로드 URL 포맷:**
```
https://docs.google.com/spreadsheets/d/{SHEET_ID}/export?format=csv&gid={GID}
```
- `SHEET_ID` — URL `/d/` 다음 문자열
- `GID` — 시트 탭 ID (URL `#gid=` 다음 숫자)

## 7. 인터페이스 정의

```csharp
// Assets/Scripts/Shared/Localization/Domain/ILocalizationService.cs
// ⚠ UnityEngine, Photon import 절대 금지

namespace SwDreams.Shared.Localization.Domain
{
    /// <summary>
    /// 다국어 텍스트 조회. LocalizationBootstrap.Service 로 접근.
    /// </summary>
    public interface ILocalizationService
    {
        Locale CurrentLocale { get; }

        /// <summary>현재 Locale 의 번역 텍스트. 키 없으면 키 자체 반환 (디버깅 용이).</summary>
        string Get(string key);

        /// <summary>named placeholder 치환. args 는 (이름, 값) 튜플.</summary>
        string GetFormat(string key, params (string name, object value)[] args);

        /// <summary>특정 Locale 강제 조회 (튜토리얼 미리보기 등 특수 용도).</summary>
        string Get(string key, Locale locale);

        void SetLocale(Locale locale);

        /// <summary>Locale 변경 시 발생. LocalizedText 가 구독.</summary>
        event System.Action OnLocaleChanged;
    }
}
```

```csharp
// Assets/Scripts/Shared/Localization/Domain/Locale.cs
namespace SwDreams.Shared.Localization.Domain
{
    public enum Locale
    {
        KO_KR = 0,  // 한국어 (기본값)
        EN_US = 1,
        JA_JP = 2,
        ZH_CN = 3,
    }
}
```

```csharp
// Assets/Scripts/Shared/Localization/Domain/LocalizationKey.cs (선택)
namespace SwDreams.Shared.Localization.Domain
{
    /// <summary>
    /// 자주 쓰는 키 상수. 컴파일 타임 안전성용. 모든 키를 여기 둘 필요는 없음.
    /// 동적으로 만드는 키 (예: $"skill.{id}.name") 는 그대로 문자열 사용.
    /// </summary>
    public static class LocalizationKey
    {
        // UI
        public const string UI_StartButton  = "ui.menu.start_button";
        public const string UI_ResultVictory = "ui.result.victory";
        public const string UI_ResultDefeat  = "ui.result.defeat";

        // Toast
        public const string Toast_Disconnect    = "ui.toast.disconnect";
        public const string Toast_HostMigrated  = "ui.toast.host_migrated";

        // Error
        public const string Error_ConnectionLost = "error.connection_lost";
        public const string Error_RoomFull       = "error.room_full";
    }
}
```

## 8. LocalizationTable (SO)

```csharp
// Assets/Scripts/Shared/Localization/Adapter/LocalizationTable.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    [CreateAssetMenu(fileName = "LocalizationTable",
                     menuName = "ProjectSD/Data/LocalizationTable")]
    public class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public string ko;
            public string en;
            public string ja;
            public string zh;
            public string note; // 게임 미사용
        }

        [SerializeField] private List<Entry> entries = new();

        // 빌드 시 OnEnable 에서 채워짐 (런타임 조회용 캐시)
        private Dictionary<string, Entry> lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public void RebuildLookup()
        {
            lookup = new Dictionary<string, Entry>(entries.Count);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.key)) continue;
                lookup[e.key] = e; // 중복 키는 마지막이 이김 (임포터에서 경고)
            }
        }

        public string Get(string key, Locale locale)
        {
            if (lookup == null) RebuildLookup();
            if (!lookup.TryGetValue(key, out var entry))
                return key; // 키 없으면 키 자체 반환 (디버깅)

            return locale switch
            {
                Locale.KO_KR => string.IsNullOrEmpty(entry.ko) ? entry.en : entry.ko,
                Locale.EN_US => string.IsNullOrEmpty(entry.en) ? entry.ko : entry.en,
                Locale.JA_JP => string.IsNullOrEmpty(entry.ja) ? entry.en : entry.ja,
                Locale.ZH_CN => string.IsNullOrEmpty(entry.zh) ? entry.en : entry.zh,
                _ => entry.en ?? key,
            };
        }

#if UNITY_EDITOR
        // Editor 임포터 전용 — 인스펙터 노출 안 함
        public List<Entry> EditorEntries => entries;
#endif
    }
}
```

**Fallback 정책:** 현재 Locale 의 번역이 비어 있으면 EN 으로 fallback, EN 도 없으면 KO. 둘 다 없으면 키 자체. 이 우선순위는 의도적 — KO 가 작업 1순위, EN 이 글로벌 기준.

## 9. LocalizationManager (싱글턴 / 동기 API)

```csharp
// Assets/Scripts/Shared/Localization/Adapter/LocalizationManager.cs
using System;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    public class LocalizationManager : ILocalizationService
    {
        private readonly LocalizationTable table;
        public Locale CurrentLocale { get; private set; }
        public event Action OnLocaleChanged;

        public LocalizationManager(LocalizationTable table, Locale initialLocale)
        {
            this.table = table;
            this.table.RebuildLookup();
            CurrentLocale = initialLocale;
        }

        public string Get(string key) => table.Get(key, CurrentLocale);

        public string Get(string key, Locale locale) => table.Get(key, locale);

        public string GetFormat(string key, params (string name, object value)[] args)
        {
            string raw = Get(key);
            if (args == null || args.Length == 0) return raw;

            // {name} → 값 치환. C# string.Format 의 {0} 인덱스 형식은 안 씀.
            foreach (var (name, value) in args)
                raw = raw.Replace("{" + name + "}", value?.ToString() ?? string.Empty);
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
```

**WHY 동기 API:** Survivors-like 는 매 프레임 다수의 데미지 팝업·HUD 갱신이 발생. 텍스트 조회가 비동기면 깜빡임/race condition 위험. SO 는 이미 메모리 상주이므로 Dictionary 조회 O(1).

## 10. LocalizationBootstrap (씬 진입점)

```csharp
// Assets/Scripts/Shared/Localization/Adapter/LocalizationBootstrap.cs
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    public class LocalizationBootstrap : MonoBehaviour
    {
        public static LocalizationBootstrap Instance { get; private set; }
        public static ILocalizationService Service { get; private set; }

        [SerializeField] private LocalizationTable table;
        [SerializeField] private LocaleFontMap fontMap;
        [SerializeField] private Locale defaultLocale = Locale.KO_KR;

        public LocaleFontMap FontMap => fontMap;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // PlayerPrefs 또는 PlatformBootstrap.Service.LoadData("settings.locale") 로 사용자 설정 로드
            var saved = LoadSavedLocale(defaultLocale);
            Service = new LocalizationManager(table, saved);
            Debug.Log($"[Localization] Initialized: {saved}");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Service = null;
            Instance = null;
        }

        private Locale LoadSavedLocale(Locale fallback)
        {
            // Phase A: PlayerPrefs. Phase B 에서 PlatformBootstrap 클라우드 세이브로 이관 가능.
            int v = PlayerPrefs.GetInt("settings.locale", -1);
            return v < 0 ? fallback : (Locale)v;
        }

        public static void SaveLocalePref(Locale locale)
        {
            PlayerPrefs.SetInt("settings.locale", (int)locale);
            PlayerPrefs.Save();
        }
    }
}
```

**셋업:** MenuScene 진입점 (또는 NetworkManager 와 동일 GameObject)에 컴포넌트 추가 + 인스펙터에서 `LocalizationTable` / `LocaleFontMap` 할당.

## 11. LocalizedText 컴포넌트

```csharp
// Assets/Scripts/Shared/Localization/Adapter/LocalizedText.cs
using TMPro;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private bool applyFontPerLocale = true;

        private TMP_Text tmp;
        private ILocalizationService service;

        private void Awake() => tmp = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            service = LocalizationBootstrap.Service;
            if (service != null)
            {
                service.OnLocaleChanged += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (service != null) service.OnLocaleChanged -= Refresh;
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
                var fontMap = LocalizationBootstrap.Instance?.FontMap;
                var font = fontMap?.GetFont(service.CurrentLocale);
                if (font != null) tmp.font = font;
            }
        }
    }
}
```

**동적 텍스트 (placeholder 있는 경우):**
- `LocalizedText` 는 정적 키만 처리.
- 동적 텍스트(데미지 팝업 등)는 호출부에서 직접 `LocalizationBootstrap.Service.GetFormat(key, args)` 호출.

## 12. LocaleFontMap (SO)

```csharp
// Assets/Scripts/Shared/Localization/Adapter/LocaleFontMap.cs
using TMPro;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    [CreateAssetMenu(fileName = "LocaleFontMap",
                     menuName = "ProjectSD/Data/LocaleFontMap")]
    public class LocaleFontMap : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset koFont;
        [SerializeField] private TMP_FontAsset enFont;
        [SerializeField] private TMP_FontAsset jaFont;
        [SerializeField] private TMP_FontAsset zhFont;

        public TMP_FontAsset GetFont(Locale locale) => locale switch
        {
            Locale.KO_KR => koFont,
            Locale.EN_US => enFont,
            Locale.JA_JP => jaFont,
            Locale.ZH_CN => zhFont,
            _ => enFont,
        };
    }
}
```

**폰트 에셋 준비:** 1차 4개 언어 모두 NotoSans 패밀리(SIL OFL 라이선스, 상업 사용 OK) 권장. CJK 폰트는 글리프 수가 많아 TMP 다이나믹 모드 사용 또는 사전 atlas 생성.

## 13. Editor 임포터

```csharp
// Assets/Scripts/Shared/Localization/Editor/LocalizationSheetImporter.cs
using System.Collections.Generic;
using System.Net.Http;
using UnityEditor;
using UnityEngine;
using SwDreams.Shared.Localization.Adapter;

namespace SwDreams.Shared.Localization.EditorTools
{
    public class LocalizationSheetImporter : EditorWindow
    {
        private const string PrefKey_SheetId = "ProjectSD.Localization.SheetId";
        private const string PrefKey_Gid = "ProjectSD.Localization.Gid";

        private string sheetId;
        private string gid;
        private LocalizationTable targetTable;

        [MenuItem("ProjectSD/Localization/Import from Google Sheet")]
        public static void Open() => GetWindow<LocalizationSheetImporter>("Localization Importer");

        private void OnEnable()
        {
            sheetId = EditorPrefs.GetString(PrefKey_SheetId, "");
            gid = EditorPrefs.GetString(PrefKey_Gid, "0");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Google Sheet", EditorStyles.boldLabel);
            sheetId = EditorGUILayout.TextField("Sheet ID", sheetId);
            gid = EditorGUILayout.TextField("GID (tab id)", gid);
            targetTable = (LocalizationTable)EditorGUILayout.ObjectField(
                "Target Table", targetTable, typeof(LocalizationTable), false);

            EditorGUILayout.Space();
            if (GUILayout.Button("Pull & Import"))
            {
                EditorPrefs.SetString(PrefKey_SheetId, sheetId);
                EditorPrefs.SetString(PrefKey_Gid, gid);
                _ = PullAndImport();
            }
        }

        private async System.Threading.Tasks.Task PullAndImport()
        {
            if (string.IsNullOrEmpty(sheetId) || targetTable == null)
            {
                Debug.LogError("[Localization] Sheet ID 또는 Target Table 미설정");
                return;
            }
            string url = $"https://docs.google.com/spreadsheets/d/{sheetId}/export?format=csv&gid={gid}";
            using var http = new HttpClient();
            string csv;
            try { csv = await http.GetStringAsync(url); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Localization] CSV 다운로드 실패: {ex.Message}");
                return;
            }

            var rows = ParseCsv(csv);
            ImportRows(rows);
            EditorUtility.SetDirty(targetTable);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Localization] Imported {rows.Count - 1} rows into {targetTable.name}");
        }

        // 컬럼 인덱스: key=0, ko_auto=1, en_auto=2, ja_auto=3, zh_auto=4,
        //              ko_final=5, en_final=6, ja_final=7, zh_final=8, note=9
        private void ImportRows(List<List<string>> rows)
        {
            const int Col_Key = 0;
            const int Col_KoFinal = 5;
            const int Col_EnFinal = 6;
            const int Col_JaFinal = 7;
            const int Col_ZhFinal = 8;
            const int Col_Note = 9;

            var entries = targetTable.EditorEntries;
            entries.Clear();

            var seen = new HashSet<string>();
            for (int i = 1; i < rows.Count; i++) // 첫 행은 헤더
            {
                var row = rows[i];
                if (row.Count <= Col_Key || string.IsNullOrWhiteSpace(row[Col_Key])) continue;
                string key = row[Col_Key].Trim();
                if (!seen.Add(key))
                {
                    Debug.LogWarning($"[Localization] 중복 키: {key} (row {i})");
                    continue;
                }
                entries.Add(new LocalizationTable.Entry
                {
                    key  = key,
                    ko   = row.Count > Col_KoFinal ? row[Col_KoFinal] : "",
                    en   = row.Count > Col_EnFinal ? row[Col_EnFinal] : "",
                    ja   = row.Count > Col_JaFinal ? row[Col_JaFinal] : "",
                    zh   = row.Count > Col_ZhFinal ? row[Col_ZhFinal] : "",
                    note = row.Count > Col_Note    ? row[Col_Note]    : "",
                });
            }
            targetTable.RebuildLookup();
        }

        // RFC 4180 호환 CSV 파서 — 따옴표 안 콤마/줄바꿈 처리
        private List<List<string>> ParseCsv(string csv)
        {
            var rows = new List<List<string>>();
            var cur = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cell.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { cur.Add(cell.ToString()); cell.Clear(); }
                    else if (c == '\n') { cur.Add(cell.ToString()); cell.Clear(); rows.Add(cur); cur = new List<string>(); }
                    else if (c == '\r') { /* skip */ }
                    else cell.Append(c);
                }
            }
            if (cell.Length > 0 || cur.Count > 0) { cur.Add(cell.ToString()); rows.Add(cur); }
            return rows;
        }
    }
}
```

**WHY EditorPrefs 에 sheetId 저장:** 팀원마다 시트 ID 가 같으므로 EditorPrefs(머신 단위) 가 적절. 시트 ID 자체는 비밀이 아님(시트는 public read), 다만 git 에 박지 않아 깔끔.

## 14. SO 통합 (스킬/패시브/혼돈)

**기존:**
```csharp
// SkillData.cs (예시)
[SerializeField] private string skillName;     // 한국어 하드코딩
[SerializeField] private string description;
```

**변경:**
```csharp
// SkillData.cs
[SerializeField] private string nameKey;       // "skill.longsword.name"
[SerializeField] private string descKey;       // "skill.longsword.desc"

public string GetName() => LocalizationBootstrap.Service?.Get(nameKey) ?? nameKey;
public string GetDescription() => LocalizationBootstrap.Service?.Get(descKey) ?? descKey;
```

호출부(`SkillCardUI.Refresh()` 등)는 `skill.GetName()` / `skill.GetDescription()` 으로 변경. 빌드타임에 키 자동 생성 규칙(`$"skill.{skillId}.name"`)을 SkillData 의 `OnValidate` 에서 강제하면 키 누락 방지.

**점진 마이그레이션 가능:** `nameKey` 가 비면 기존 `skillName` fallback. 한 번에 전체 SO 갈아엎을 필요 없음.

## 15. 호출 후크 — 기존 코드 변경 위치

| 파일 | 변경 내용 | 목적 |
|---|---|---|
| MenuScene 진입점 (또는 `NetworkManager.cs` 같은 GameObject) | `LocalizationBootstrap` 컴포넌트 추가 | 부트 |
| `Features/UI/Presentation/SkillCardUI.cs` | `card.title.text = skill.skillName` → `card.title.text = skill.GetName()` | 스킬 카드 |
| `Features/UI/Presentation/InGameHUD.cs` | 모든 한국어 라벨에 `LocalizedText` 컴포넌트 추가 + 키 매핑 | HUD 라벨 |
| `Features/UI/Presentation/LevelUpPanel.cs` | 동일 | 레벨업 패널 |
| `Features/UI/Presentation/ResultPanelUI.cs` | "승리"/"패배" 같은 하드코딩 → `LocalizedText` | 결과 화면 |
| `Features/UI/Presentation/DamagePopup.cs` | (선택) 크리티컬 라벨 등에 `GetFormat` 사용 | 데미지 팝업 |
| `Features/UI/Adapter/Menu/*Controller.cs` | 모든 버튼/타이틀 텍스트에 `LocalizedText` | 메뉴 UI |
| `Features/UI/Presentation/UImanager.cs` 의 ShowToast | 토스트 메시지를 키 기반으로 받도록 시그니처 추가: `ShowToastByKey(string key)` | 토스트 |
| `Shared/Managers/ResultManager.cs` | RPC_ShowResult 등의 한국어 메시지 → 키 | 결과 |
| 옵션 UI (신규 또는 기존 설정 패널) | 언어 드롭다운 → `LocalizationBootstrap.Service.SetLocale(...)` + `SaveLocalePref(...)` | 사용자 전환 |

**주의:** TMP_Text 가 들어간 모든 프리팹에 `LocalizedText` 추가는 한번에 안 됨 — 점진적으로. 키 없이 비워두면 기존 텍스트 유지(`Refresh()` 에서 키 빈 경우 early return).

## 16. 네트워크

네트워크 기본 규약은 [network-sync.md](network-sync.md) 참조.

- **Locale 은 클라이언트 로컬.** 네트워크 동기화 안 함. 같은 룸에서 KO 유저와 EN 유저가 각자 자기 언어로 봄.
- **RPC 인자에 텍스트 직접 보내지 말 것.** 키만 보내고 수신측에서 `Service.Get(key)` 로 해석. 예: 토스트 RPC 는 `string toastKey` 를 받음.
- 예외: 사용자 입력 텍스트(닉네임, 채팅)는 그대로 전송 — 번역 대상 아님.

## 17. 테스트

- **단위 테스트:** `LocalizationTable.Get()` 의 fallback 동작(번역 없을 때 EN → KO → 키), `LocalizationManager.GetFormat()` 의 named placeholder 치환.
- **플레이 모드 시나리오:**
  - MenuScene 에서 언어 전환 → 모든 `LocalizedText` 가 즉시 갱신되는지
  - GameScene 진입 후 언어 전환 → HUD/결과창 갱신
  - 멀티 룸에서 호스트=KO, 클라=EN 일 때 각자 다른 언어로 보이는지
  - 키 누락 시 키 자체가 화면에 노출되는지(디버깅 용이성 확인)
- **회귀 체크:** 스킬 카드 텍스트, 보스 알림, 결과창 승/패 메시지.

## 18. 도메인 순수성 / 아키텍처 규칙

CLAUDE.md §2 의존성 방향 엄수:

| 파일 | 허용 import | 금지 import |
|---|---|---|
| `Shared/Localization/Domain/ILocalizationService.cs` | (없음) | `UnityEngine`, `Photon.*`, TMPro |
| `Shared/Localization/Domain/Locale.cs` | (없음) | 위와 동일 |
| `Shared/Localization/Domain/LocalizationKey.cs` | (없음) | 위와 동일 |
| `Shared/Localization/Adapter/LocalizationManager.cs` | `UnityEngine` (Debug.Log 만) | `Photon.*`, TMPro |
| `Shared/Localization/Adapter/LocalizationTable.cs` | `UnityEngine` | TMPro (분리) |
| `Shared/Localization/Adapter/LocaleFontMap.cs` | `UnityEngine`, `TMPro` | `Photon.*` |
| `Shared/Localization/Adapter/LocalizedText.cs` | `UnityEngine`, `TMPro` | `Photon.*` |
| `Shared/Localization/Adapter/LocalizationBootstrap.cs` | `UnityEngine` | `Photon.*` |
| `Shared/Localization/Editor/*.cs` | UnityEditor, System.Net | - |

**검증 방법:** Phase A 완료 후 `architecture-guardian` 서브에이전트 호출.

## 19. 구현 단계

### Phase A — 코어 시스템 + 임포터 (1.5~2일)

**범위:**
- §4 폴더/파일 생성
- §7 Domain 인터페이스/Locale enum
- §8~12 Adapter 일체 (Manager, Table, FontMap, LocalizedText, Bootstrap)
- §13 Editor 임포터
- 빈 `LocalizationTable.asset` + `LocaleFontMap.asset` 생성
- Google Sheet 템플릿 작성 + 1~2개 키로 임포트 테스트

**산출물:** 게임 동작 변화 없음. 메뉴씬 런타임에 `[Localization] Initialized: KO_KR` 로그.

### Phase B — UI 키 매핑 (수일~1주, 점진적)

**범위:**
- MenuScene UI(타이틀, 룸리스트, 대기실, 캐릭터 선택)에 `LocalizedText` 추가 + 키 매핑
- InGameHUD, LevelUpPanel, ResultPanel, FrameToast 메시지
- 옵션 패널 언어 드롭다운 추가
- 시트 KO 컬럼 채우기 (기존 한국어 텍스트 마이그레이션)

### Phase C — SO 통합 (1주)

**범위:**
- SkillData/PassiveSkillData/ChaosSkillData 에 `nameKey`/`descKey` 필드 추가 + `OnValidate` 자동 채움
- SkillData 별 시트 행 추가 (스킬 24+패시브 19+혼돈 19 = 62개)
- SkillCardUI 등 호출부 변경
- 자동 번역 적용(EN/JA/ZH-CN 자동 채움)

### Phase D — 검수 & 폰트 (Steam 출시 전)

**범위:**
- 시트 `*_final` 컬럼 검수 (도메인 용어 우선: 스킬 이름, "혼돈", "정수" 등)
- TMP_FontAsset 4종 셋업 (NotoSans 패밀리)
- LocaleFontMap.asset 에 매핑
- 4개 언어 전부 플레이 모드 검증

## 20. Phase A 검증 체크리스트

- [ ] `Shared/Localization/Domain/` 3 파일 컴파일 OK
- [ ] `Shared/Localization/Adapter/` 5 파일 컴파일 OK
- [ ] `Shared/Localization/Editor/` 1 파일 컴파일 OK
- [ ] Domain 3 파일에 `using UnityEngine` / `using Photon` / `using TMPro` 없음 (Grep 검증)
- [ ] `architecture-guardian` 서브에이전트 통과
- [ ] MenuScene 진입 시 Console 에 `[Localization] Initialized: KO_KR` 출력
- [ ] `LocalizationBootstrap` 이 씬에 없어도 NRE 발생 없이 게임 정상 동작 (`?.` 안전 호출 검증)
- [ ] 임포터 메뉴 `ProjectSD/Localization/Import from Google Sheet` 노출
- [ ] 테스트 시트 1개 키 임포트 → SO 인스펙터에 항목 1개 추가 확인
- [ ] CLAUDE.md §3 폴더 지도에 `Shared/Localization/` 추가
- [ ] `docs/architecture/implementation-roadmap.md` 의 Phase A 항목 ✅ 처리

## 21. 비범위 (Phase A~D 에서 안 함)

- Pluralization (`{count, plural, one, other}` 같은 ICU 형식)
- Gender / 어형 변화
- RTL 언어 (아랍어/히브리어)
- 음성 더빙 (TTS)
- 시간/통화/숫자 로컬 포맷 (CultureInfo) — 필요 시 별도 계산
- 시트 → 게임 자동 빌드 파이프라인 (CI 통합)
- 출시 후 OTA 텍스트 업데이트 (런타임 fetch) — 본 시스템은 빌드 타임 임포트만

## 22. 마이그레이션 (백엔드 교체 시)

`ILocalizationService` 만 만족하면 백엔드 교체 가능. 출시 후 다음 트리거 발생 시 Unity Localization Package 로 이관 검토:

- 지원 언어 7개 이상
- Pluralization / Gender 변형이 절반 이상 키에 필요
- 외주 번역가가 표준 워크플로 요구

이관 시 변경 범위: `LocalizationManager.cs` 만 교체, `LocalizedText.cs` 와 호출부 변경 없음.

## 23. 알려진 제약 / 트레이드오프

- [x] **자동 번역 품질** — 자동 번역만으로는 도메인 용어("혼돈", "정수", 스킬 이름) 어색. `*_final` 컬럼 수동 검수 필수
- [x] **CJK 폰트 사이즈** — NotoSans CJK 글리프 atlas 가 수십 MB. TMP 다이나믹 모드로 우회 시 첫 등장 시 1프레임 hitch 가능
- [x] **placeholder 보존** — 자동 번역이 `{playerName}` 을 깨뜨릴 수 있음. 임포트 시 검증 로직 추가 권장 (Phase D)
- [x] **Locale 별 폰트 크기 차이** — 동일 텍스트의 픽셀 폭이 언어마다 다름 (영어 ↔ 중국어). UI 레이아웃에 여유 공간 필요 (디자인 가이드)
- [x] **사용자 텍스트(닉네임)** — 번역 대상 아님. 현재 `PhotonNetwork.NickName` 그대로 사용
- [ ] **다단 폰트 폴백** — 한 텍스트에 KO + EN 혼합 시 (예: "Steam 에 오신 것을") TMP fallback 폰트 체인 별도 구성 필요

## 24. 기존 코드 참조

| 파일 | 용도 |
|---|---|
| `Assets/Scripts/Shared/Platform/Adapter/PlatformBootstrap.cs` | 부트스트랩 패턴 레퍼런스 (싱글턴 + Service 정적 프로퍼티) |
| `Assets/Scripts/Shared/Domain/Interfaces/IDamageable.cs` | 인터페이스 패턴 레퍼런스 |
| `Assets/Scripts/Features/Skill/Adapter/Data/SkillData.cs` | `nameKey`/`descKey` 필드 추가 대상 |
| `Assets/Scripts/Features/UI/Presentation/SkillCardUI.cs` | 호출부 변경 위치 |
| `Assets/Scripts/Editor/SkillDataEditor.cs` | 키 필드 추가 시 동시 업데이트 (CLAUDE.md 메모리 — Custom Editor Sync) |
| `CLAUDE.md` §2 | 의존성 방향 규칙 |
| `docs/systems/platform-integration.md` | 부트스트랩/SDK 추상화 설계 패턴 참고 |

## 25. 외부 참고

- TextMesh Pro 폰트 에셋 가이드: https://docs.unity3d.com/Packages/com.unity.textmeshpro@latest/manual/FontAssetsCreator.html
- Noto Sans CJK (Google, SIL OFL): https://fonts.google.com/noto/specimen/Noto+Sans+KR
- RFC 4180 (CSV): https://datatracker.ietf.org/doc/html/rfc4180
