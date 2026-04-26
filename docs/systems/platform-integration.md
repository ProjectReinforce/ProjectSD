# Platform Integration — Stove / Steam SDK 추상화

플랫폼 SDK(실적, 클라우드 세이브, 유저 식별, 통계 제출) 통합을 위한 `IPlatformService` 추상화. 본 문서는 **다른 세션이 이 문서만 보고 Phase A 구현을 끝낼 수 있도록** 작성되었다.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `platform-integration` |
| 분류 | 인프라 |
| 의존 레이어 | Domain (인터페이스), Adapter (SDK 구현) |
| 최종 업데이트 | 2026-04-26 |
| 구현 상태 | ⬜ 미구현 — 진행 체크리스트 [implementation-roadmap.md § 8-1, 8-3, 8-4](../architecture/implementation-roadmap.md) |

## 2. 목적

**왜 추상화 먼저:** Stove Indie SDK 와 Steamworks 는 API 형태가 전혀 다르다. 만약 컨텐츠 작업이 다 끝난 시점에 통합하면 `PhotonNetwork.LocalPlayer.NickName`, `BroadcastResult()`, `RecordKill()` 같은 곳들이 직접 호출 형태로 굳어 있어 양쪽 플랫폼 분기 코드를 게임 루프 곳곳에 박는 리팩터링 지옥이 발생한다. **인터페이스 + Stub 만 미리 깔아두면**, 컨텐츠 작업 중에는 stub로 진행하고 출시 직전에 SDK 구현체를 plug-in 형태로 추가할 수 있다.

## 3. 출시 순서 (확정)

| 순서 | 플랫폼 | 이유 |
|---|---|---|
| 1 | **Stove Indie** | 한국 게임 등급 분류 절차가 Stove 인디를 통해 진행. 등급 획득 후 다른 플랫폼 출시 가능 |
| 2 | **Steam** | Stove 등급 결과를 근거로 Steam 출시. 글로벌 노출 확장 |

**WHY 이 순서가 설계에 영향:** 1차 검증 대상이 `StovePlatformService`. 인터페이스 메서드 시그니처를 설계할 때 **Stove SDK 가 제공하는 기능을 1순위 기준**으로 설계해야 한다. (Steam 만 가능한 기능을 인터페이스에 넣으면 Stove 빌드에서 빈 구현이 너무 많아짐)

## 4. 폴더 구조

```
Assets/Scripts/Shared/Platform/
├── Domain/                              ← UnityEngine / Photon import 금지
│   ├── IPlatformService.cs              ← 핵심 인터페이스
│   ├── PlatformUserProfile.cs           ← 유저 정보 VO
│   ├── PlatformType.cs                  ← enum: Local, Stove, Steam
│   └── AchievementId.cs                 ← 실적 ID 상수
└── Adapter/                             ← Unity / SDK 의존 OK
    ├── PlatformBootstrap.cs             ← MonoBehaviour 싱글턴
    ├── LocalPlatformService.cs          ← Phase A: Debug.Log stub
    ├── StovePlatformService.cs          ← Phase B: Stove SDK 연동
    └── SteamPlatformService.cs          ← Phase C: Steamworks.NET 연동
```

**WHY `Shared/Platform/`:** `Shared/Domain/Interfaces/IDamageable.cs` 와 동일 패턴. 인프라 레벨이라 단일 Feature 가 아님 → `Features/Platform/` 보다 `Shared/Platform/` 자연스러움.

## 5. 인터페이스 정의

```csharp
// Assets/Scripts/Shared/Platform/Domain/IPlatformService.cs
// ⚠ UnityEngine, Photon import 절대 금지

using SwDreams.Shared.Domain;  // GameResult 만 허용

namespace SwDreams.Shared.Platform.Domain
{
    /// <summary>
    /// 플랫폼 SDK 추상화. Stove / Steam / Local Stub 구현체가 따른다.
    /// 호출자는 PlatformBootstrap.Service 를 통해 접근.
    /// </summary>
    public interface IPlatformService
    {
        // ===== 라이프사이클 =====
        void Initialize();
        void Shutdown();
        bool IsInitialized { get; }

        // ===== 유저 식별 =====
        /// <summary>
        /// 로컬 유저 정보. SDK 미초기화 시 Stub 반환 (NickName 등).
        /// PhotonNetwork.LocalPlayer.NickName 을 대체하지 않음 — 추가 경로.
        /// </summary>
        PlatformUserProfile GetLocalUser();

        // ===== 실적 (Achievement) =====
        void UnlockAchievement(string achievementId);
        bool IsAchievementUnlocked(string achievementId);

        // ===== 통계 (Stats) =====
        /// <summary>누적 통계. Steam 의 SetStat(... + delta) 와 동등.</summary>
        void IncrementStat(string statId, int delta);

        /// <summary>한 판 결과 제출. Stove/Steam 의 리더보드/통계 API 로 매핑.</summary>
        void SubmitRunResult(GameResult result);

        // ===== 클라우드 세이브 =====
        /// <summary>메타 진행도 저장 (key=설정 키, json=직렬화된 페이로드).</summary>
        void SaveData(string key, string json);

        /// <summary>저장된 데이터 로드. 없으면 null 반환.</summary>
        string LoadData(string key);
    }
}
```

```csharp
// Assets/Scripts/Shared/Platform/Domain/PlatformUserProfile.cs
namespace SwDreams.Shared.Platform.Domain
{
    /// <summary>
    /// 플랫폼 유저 정보. 순수 C# VO.
    /// </summary>
    public class PlatformUserProfile
    {
        public string UserId { get; set; }       // Steam ID / Stove ID / "local-{photonActor}"
        public string DisplayName { get; set; }  // 닉네임
        public PlatformType Source { get; set; } // 어디서 왔는가
    }
}
```

```csharp
// Assets/Scripts/Shared/Platform/Domain/PlatformType.cs
namespace SwDreams.Shared.Platform.Domain
{
    public enum PlatformType
    {
        Local = 0,   // Editor / 개발용 stub
        Stove = 1,
        Steam = 2,
    }
}
```

```csharp
// Assets/Scripts/Shared/Platform/Domain/AchievementId.cs
namespace SwDreams.Shared.Platform.Domain
{
    /// <summary>
    /// 실적 ID 상수. Stove/Steam 양쪽에 동일 ID로 매핑.
    /// 실제 SDK 등록 시 이 문자열을 그대로 사용.
    /// </summary>
    public static class AchievementId
    {
        // 클리어
        public const string FirstClear         = "FIRST_CLEAR";
        public const string ClearWithoutDeath  = "CLEAR_NO_DEATH";

        // 보스
        public const string BossKilled         = "BOSS_KILLED";
        public const string BossKilledChaos    = "BOSS_KILLED_WITH_CHAOS";

        // 캐릭터별 클리어 (예시 — 캐릭터 추가 시 여기에)
        public const string Clear_Character_01 = "CLEAR_CHAR_01";
        public const string Clear_Character_02 = "CLEAR_CHAR_02";

        // 진화
        public const string FirstEvolution     = "FIRST_EVOLUTION";
        public const string AllEvolutions      = "ALL_EVOLUTIONS_DISCOVERED";

        // 통계 마일스톤
        public const string Survive10Min       = "SURVIVE_10_MIN";
        public const string Survive15Min       = "SURVIVE_15_MIN";
        public const string Kills_1000         = "TOTAL_KILLS_1000";

        // 통계 statId (IncrementStat 용)
        public const string Stat_TotalKills    = "stat_total_kills";
        public const string Stat_TotalDeaths   = "stat_total_deaths";
        public const string Stat_TotalRuns     = "stat_total_runs";
        public const string Stat_TotalClears   = "stat_total_clears";
    }
}
```

## 6. PlatformBootstrap 설계

```csharp
// Assets/Scripts/Shared/Platform/Adapter/PlatformBootstrap.cs
using UnityEngine;
using SwDreams.Shared.Platform.Domain;

namespace SwDreams.Shared.Platform.Adapter
{
    /// <summary>
    /// 플랫폼 서비스 싱글턴. 다른 매니저들이 PlatformBootstrap.Service 로 접근.
    ///
    /// 셋업: GameScene 또는 MenuScene 진입 시 빈 GameObject + 이 컴포넌트.
    /// 또는 NetworkManager 와 동일 GameObject (DontDestroyOnLoad).
    /// </summary>
    public class PlatformBootstrap : MonoBehaviour
    {
        public static PlatformBootstrap Instance { get; private set; }
        public static IPlatformService Service { get; private set; }

        [SerializeField] private PlatformType platformType = PlatformType.Local;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Service = CreateService(platformType);
            Service.Initialize();
            Debug.Log($"[Platform] Initialized: {platformType}");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Service?.Shutdown();
            Service = null;
            Instance = null;
        }

        private static IPlatformService CreateService(PlatformType type)
        {
            return type switch
            {
                PlatformType.Local => new LocalPlatformService(),
                PlatformType.Stove => new StovePlatformService(),  // Phase B
                PlatformType.Steam => new SteamPlatformService(),  // Phase C
                _ => new LocalPlatformService(),
            };
        }
    }
}
```

```csharp
// Assets/Scripts/Shared/Platform/Adapter/LocalPlatformService.cs
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Shared.Domain;
using SwDreams.Shared.Platform.Domain;

namespace SwDreams.Shared.Platform.Adapter
{
    /// <summary>
    /// Editor / 개발용 stub. 실제 저장 안 함.
    /// 모든 호출을 Debug.Log + 메모리 캐시로만 처리.
    /// </summary>
    public class LocalPlatformService : IPlatformService
    {
        public bool IsInitialized { get; private set; }

        private readonly HashSet<string> unlockedAchievements = new();
        private readonly Dictionary<string, int> stats = new();
        private readonly Dictionary<string, string> savedData = new();

        public void Initialize()
        {
            IsInitialized = true;
            Debug.Log("[Platform/Local] Initialized");
        }

        public void Shutdown()
        {
            IsInitialized = false;
            Debug.Log("[Platform/Local] Shutdown");
        }

        public PlatformUserProfile GetLocalUser()
        {
            // PhotonNetwork.LocalPlayer 가 있으면 그것을 차용, 없으면 dummy
            string nick = "LocalPlayer";
            int actor = 0;
            if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer != null)
            {
                nick = PhotonNetwork.LocalPlayer.NickName;
                actor = PhotonNetwork.LocalPlayer.ActorNumber;
            }
            return new PlatformUserProfile
            {
                UserId = $"local-{actor}",
                DisplayName = string.IsNullOrEmpty(nick) ? "LocalPlayer" : nick,
                Source = PlatformType.Local,
            };
        }

        public void UnlockAchievement(string achievementId)
        {
            if (unlockedAchievements.Add(achievementId))
                Debug.Log($"[Platform/Local] Achievement: {achievementId}");
        }

        public bool IsAchievementUnlocked(string achievementId)
            => unlockedAchievements.Contains(achievementId);

        public void IncrementStat(string statId, int delta)
        {
            stats.TryGetValue(statId, out int v);
            stats[statId] = v + delta;
            Debug.Log($"[Platform/Local] Stat: {statId} += {delta} (total {stats[statId]})");
        }

        public void SubmitRunResult(GameResult result)
        {
            Debug.Log($"[Platform/Local] SubmitRunResult: cleared={result.IsCleared}, " +
                      $"time={result.PlayTime:F1}s, kills={result.TotalKills}");
        }

        public void SaveData(string key, string json)
        {
            savedData[key] = json;
            Debug.Log($"[Platform/Local] SaveData[{key}] = {json.Length} chars");
        }

        public string LoadData(string key)
            => savedData.TryGetValue(key, out var v) ? v : null;
    }
}
```

**WHY `LocalPlatformService` 가 `using Photon.Pun` 을 가져도 OK:** Adapter 레이어이기 때문. Domain 레이어(`Shared/Platform/Domain/`)만 외부 import 금지.

## 7. 호출 후크 (코드 변경 최소)

다른 세션이 1줄씩만 추가하면 끝나도록 정확한 위치 명시.

| 파일 | 라인(현재 기준) | 추가할 호출 | 목적 |
|---|---|---|---|
| `Assets/Scripts/Shared/Managers/ResultManager.cs` | `BroadcastResult()` 메서드 끝 (line 220 직후, RPC 호출 다음 줄) | `PlatformBootstrap.Service?.SubmitRunResult(localResult);` | Run 종료 시 결과 제출 |
| `Assets/Scripts/Shared/Managers/ResultManager.cs` | `RPC_ShowResult()` 내 (line 244 `UIManager.Instance?.ShowResult(localResult);` 다음) | 결과에 따라 `UnlockAchievement` (FirstClear, ClearWithoutDeath 등) | 클리어 실적 |
| `Assets/Scripts/Features/Boss/Adapter/Boss.cs` | `RPC_BossDied()` 메서드 line 251 직후 | `PlatformBootstrap.Service?.UnlockAchievement(AchievementId.BossKilled);` | 보스 처치 실적 |
| `Assets/Scripts/Shared/Managers/GameStatTracker.cs` | `RecordKill()` line 42 직후 (TotalKills++ 다음) | `PlatformBootstrap.Service?.IncrementStat(AchievementId.Stat_TotalKills, 1);` | 누적 킬 |
| `Assets/Scripts/Shared/Managers/GameStatTracker.cs` | `RecordDeath()` line 52 직후 | `PlatformBootstrap.Service?.IncrementStat(AchievementId.Stat_TotalDeaths, 1);` | 누적 데스 |
| `Assets/Scripts/Shared/Managers/NetworkManager.cs` (또는 기동 진입점) | `Awake()` 내, PUN 연결 전 | (선택) `PlatformBootstrap.Service?.GetLocalUser()` 결과의 DisplayName 을 `PhotonNetwork.NickName` 에 주입 | 닉네임 통일 |

**중요:** `?.` 안전 연산자 사용. `PlatformBootstrap` 이 씬에 없어도 NRE 발생 안 함. Phase A 미완 상태에서도 게임 동작.

### 7-1. 실적 발사 예시 (ResultManager `RPC_ShowResult` 내부)

```csharp
// 기존 RPC_ShowResult 끝부분
UIManager.Instance?.ShowResult(localResult);

// ===== 추가할 코드 =====
var platform = PlatformBootstrap.Service;
if (platform != null)
{
    platform.SubmitRunResult(localResult);
    platform.IncrementStat(AchievementId.Stat_TotalRuns, 1);

    if (localResult.IsCleared)
    {
        platform.UnlockAchievement(AchievementId.FirstClear);
        platform.IncrementStat(AchievementId.Stat_TotalClears, 1);

        if (localResult.TotalDeaths == 0)
            platform.UnlockAchievement(AchievementId.ClearWithoutDeath);
    }
}
```

## 8. 유저 식별 전략

| 용도 | 사용 API | 비고 |
|---|---|---|
| 룸 내 플레이어 구분 | `PhotonNetwork.LocalPlayer.ActorNumber` | **유지**. Photon 룸 내 로컬 ID |
| 표시 닉네임 | `PhotonNetwork.LocalPlayer.NickName` | **유지**. 추가로 `IPlatformService.GetLocalUser().DisplayName` 으로도 접근 가능 |
| 플랫폼 전역 ID | `IPlatformService.GetLocalUser().UserId` | **신규**. Steam/Stove ID. 룸 외부(클라우드 세이브, 친구 초대) 용도 |
| 친구 초대 | (Phase B/C) `IPlatformService.InviteFriend(...)` | 인터페이스 확장 검토 (Phase B 에서 추가) |

**원칙:** PhotonNetwork 호출은 **제거하지 않음**. `IPlatformService` 는 추가 경로일 뿐. 게임 로직(룸 입장, RPC 라우팅)은 Photon ID 그대로 사용.

플랫폼 ID 를 룸 내 다른 플레이어와 공유하려면 `PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "platformId", platformId } })`.

## 9. 실적 ID 카탈로그

§5 `AchievementId.cs` 참조. **초안**이며, Phase B (Stove SDK 연동) 시점에 다음을 확정:

- 캐릭터별 클리어 실적: 캐릭터 N명 × 1 = N개
- 진화 스킬별 발견: 진화 10종 × 1 = 10개
- 보스 처치 (혼돈 미선택 / 혼돈 적용 별도) × 보스 종류
- 시간 마일스톤: 5분, 10분, 15분 생존
- 킬 마일스톤: 100, 500, 1000, 5000

**Stove 측 등록:** Stove 인디 개발자 포털 → 실적 등록 → 위 ID 와 동일 문자열 사용.
**Steam 측 등록:** Steamworks 파트너 사이트 → Achievements → 동일 ID.

## 10. 클라우드 세이브 범위

**저장하는 것:**
- 언락된 캐릭터 ID 목록
- 누적 통계 (`Stat_TotalKills` 등 — SDK 자체 stat 외에 백업용)
- 사용자 설정 (PTT 키, 마이크 디바이스, 볼륨, 키바인딩)
- 발견한 진화 스킬 목록

**저장 안 하는 것:**
- Run 도중 상태 (HP, 위치, 스킬 빌드) — Run 종료 시 휘발
- Photon 룸 정보
- 게임 시드

**키 명명 규칙:**
- `meta.unlocked_characters` — JSON int 배열
- `meta.discovered_evolutions` — JSON int 배열
- `settings.input` — JSON 객체
- `settings.audio` — JSON 객체

## 11. 도메인 순수성 / 아키텍처 규칙

CLAUDE.md §2 의존성 방향 엄수:

| 파일 | 허용 import | 금지 import |
|---|---|---|
| `Shared/Platform/Domain/IPlatformService.cs` | `SwDreams.Shared.Domain` (GameResult) | `UnityEngine`, `Photon.*`, Steamworks, Stove |
| `Shared/Platform/Domain/PlatformUserProfile.cs` | (없음) | 위와 동일 |
| `Shared/Platform/Domain/PlatformType.cs` | (없음) | 위와 동일 |
| `Shared/Platform/Domain/AchievementId.cs` | (없음) | 위와 동일 |
| `Shared/Platform/Adapter/PlatformBootstrap.cs` | `UnityEngine` | - |
| `Shared/Platform/Adapter/LocalPlatformService.cs` | `UnityEngine`, `Photon.Pun` (NickName 차용용) | - |
| `Shared/Platform/Adapter/StovePlatformService.cs` | `UnityEngine`, Stove SDK | - |
| `Shared/Platform/Adapter/SteamPlatformService.cs` | `UnityEngine`, Steamworks.NET | - |

**검증 방법:** Phase A 완료 후 `architecture-guardian` 서브에이전트 호출.

## 12. 구현 진행 → 별도 SSOT

**3단계 구현 계획 (Phase A/B/C) 과 검증 체크리스트는 [implementation-roadmap.md § Phase 8-1, 8-3, 8-4](../architecture/implementation-roadmap.md) 에서 관리.**

본 문서는 **spec(인터페이스/SDK 매핑/AchievementId)** 만 다루고, **roadmap(언제/어디까지)** 은 분리. 운영 룰 (2026-04-26):
- 구현 진행 중 → roadmap 의 § 8-1/8-3/8-4 체크리스트 토글
- 모든 Phase ✅ 완료 → 본 문서 §1 "구현 상태" 헤더 ⬜ → ✅ 갱신 + completed-work.md 1줄 추가

## 13. 비범위 (Phase A 에서 안 함)

- Stove / Steam SDK 임포트
- 실제 클라우드 세이브 직렬화 로직 (Phase A 는 메모리 캐시만)
- 친구 초대, 리치 프레즌스
- DLC 체크
- DRM
- 캐릭터 언락 시스템 자체 (저장 인프라만 준비, 언락 시스템은 Phase 7 이후)

## 14. 기존 코드 참조

| 파일 | 용도 |
|---|---|
| `Assets/Scripts/Shared/Domain/GameResult.cs` | `SubmitRunResult` 파라미터 타입. 이미 순수 도메인 |
| `Assets/Scripts/Shared/Domain/Interfaces/IDamageable.cs` | 인터페이스 패턴 레퍼런스 |
| `Assets/Scripts/Shared/Managers/ResultManager.cs` | Run 결과 후크 위치 (line 174~245) |
| `Assets/Scripts/Shared/Managers/GameStatTracker.cs` | 통계 후크 위치 (line 39, 49) |
| `Assets/Scripts/Features/Boss/Adapter/Boss.cs` | 보스 처치 후크 (line 248 RPC_BossDied) |
| `Assets/Scripts/Shared/Managers/NetworkManager.cs` | 부트 시점 (Awake) 참조 |
| `CLAUDE.md` §2 | 의존성 방향 규칙 |

## 15. 외부 참고

- Stove Indie 개발자 포털: https://indie.onstove.com/
- Steamworks 파트너: https://partner.steamgames.com/
- Steamworks.NET (C# 래퍼): https://steamworks.github.io/

## 16. 알려진 제약

- [ ] Stove SDK 의 보이스챗·친구 API 가능 여부는 파트너 등록 후 확인. 본 인터페이스는 음성/친구 메서드 없음 — Phase B 에서 인터페이스 확장 검토
- [ ] Steam Family Sharing 시 실적 동기화 정책 별도 검토 (Phase C)
- [ ] 동일 게임 내 Stove/Steam 크로스플레이는 이번 출시 범위 아님 (Photon 룸은 호환되지만 플랫폼 ID 가 다르므로 친구 초대 등 제한)
