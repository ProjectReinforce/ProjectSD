# Implementation Roadmap

Sweepin' Dreams 의 구현 단계 로드맵. 현재 코드 진행 상태를 반영하여 **Phase별 완료/진행 표시**를 명확히 한다.

최종 업데이트: 2026-04-19 (현재 브랜치: `Hyeon-Woo` — Feature-first 전환 완료 후)

> 본 문서는 "**언제·무엇을·어떤 순서로 구현하는가**" 의 SSOT. 게임 설계 자체는 [../game-design/overview.md](../game-design/overview.md) 참조. 개별 스킬·적·시스템 설계는 해당 폴더 문서.

## 진행 요약

| Phase | 상태 | 비고 |
|---|---|---|
| 0. 프로젝트 셋업 | ✅ 완료 | Unity 2D + PUN2, SO 템플릿 |
| 1. 네트워크 + 메뉴 플로우 | ✅ 거의 완료 | MenuScene 패널 전환, 대기실, 캐릭터 선택까지 작동 |
| 2. 기본 전투 | ✅ 완료 | Enemy, ProjectileEffect, 경험치, 풀링 구현 |
| 3. 적 AI + 스폰 고도화 | 🟡 진행 중 | 4종 적은 있으나 난이도 곡선 튜닝 중 |
| 4. 레벨업 시스템 | 🟡 진행 중 | FrameToast/Frame_PopUp 도입, 카드 UI 작업 중 |
| 5. 나머지 스킬 + 혼돈 | 🟡 진행 중 (**현재 브랜치 `Skill_Refactor`**) | 2차 리팩터링 + 장검 진화 Phase2 복구까지 |
| 6. 보스 + 네트워크 고급 | ⬜ 대기 | 보스 3페이즈, 사망/부활, 호스트 이탈 |
| 7. 마무리 + 밸런싱 | ⬜ 대기 | |
| 8. 출시 인프라 (Voice / Platform SDK) | ⬜ 대기 (설계만) | [voice-chat.md](../systems/voice-chat.md), [platform-integration.md](../systems/platform-integration.md) |

최근 관련 커밋:
- `1f225a555` fix: 장검 진화 Phase2 발사 동작 복구 → Phase 5 영역
- `84dfb3b3f` feat: FrameToast → Phase 4 UI 영역
- `6d6112763` feat: Frame_PopUp → Phase 4 UI 영역
- `b40a9e5d0` refactor: 멀티플레이어 동기화 대규모 리팩토링 — Dead Reckoning + C안 데미지 요청
- `deb23b669`~`3422f8def` refactor: 스킬 시스템 2차 리팩터링

---

## Phase 0 — 프로젝트 셋업 ✅

### 0-1. 프로젝트 기반 ✅
- [x] Unity 2D 프로젝트, Git, PUN2 + App ID, ParrelSync
- [x] 폴더 구조 (Adapter / Domain / Data / Application / BootStrap 등)

### 0-2. ScriptableObject 템플릿 ✅
- [x] `CharacterData`, `SkillData` (서브타입 7종: Projectile/Area/Orbital/Placed/Debuff/Passive/Chaos), `EnemyData`, `BossData`, `DifficultyData`, `GameplayConfig`, `AudioLibrary`

### 0-3. 씬 기본 구조 ✅
- [x] MenuScene / GameScene 2개 씬
- [x] Build Settings 등록

---

## Phase 1 — 네트워크 + 메뉴 플로우 ✅ (거의)

### 1-1. NetworkManager 싱글톤 ✅
- [x] DontDestroyOnLoad 싱글톤, Photon 콜백 중앙 처리, 연결 상태 노출

### 1-2. MenuScene 패널 전환 ✅
- [x] `MenuSceneManager`, `TitlePanelController`, `RoomListPanelController`, `WaitingRoomPanelController`
- [x] 패널 간 전환 흐름

### 1-3. 대기실 핵심 기능 ✅
- [x] 캐릭터 선택 (CustomProperties 동기화) — `CharacterSelectUI`
- [x] 준비 상태 토글
- [x] 전원 준비 → **호스트 수동 Start** → 3초 카운트다운 → 씬 전환
- [x] 카운트다운 취소 처리
- [x] **월드 공간 LobbyPlayer + 오버헤드 UI(이름/Host/Ready) + 호스트 Kick** — [../systems/waiting-room.md](../systems/waiting-room.md)

### 1-4. GameScene 기본 + 플레이어 ✅
- [x] `GameManager` + GameState
- [x] `Player` 프리팹, 이동, 카메라 추적, `AutomaticallySyncScene`

### 1-5. 결과 → 대기실 복귀 ✅
- [x] `returnToWaitingRoom` 플래그 기반 복귀
- [x] 에러 처리 (호스트 퇴장, 연결 끊김)

---

## Phase 2 — 기본 전투 ✅

### 2-1. Enemy 기본 클래스 ✅
- [x] `Enemy`, ChaseMovement, 호스트 AI, 접촉 데미지

### 2-2. PoolManager + SpawnManager 기초 ✅
- [x] 투사체 풀링 (`ProjectileSpawner`), 기본 스폰

### 2-3. 스킬 시스템 기초 ✅
- [x] `Skill`, `SkillExecutor`, `ISkillSpawner` 추상 + 구현체 (`ProjectileSpawner`, `AreaSpawner`, `OrbitalSpawner`, `DebuffSpawner`, `PlacedSpawner`)
- [x] `ProjectileEffect` 구현
- [x] 표창 기본 동작 (및 진화형 폭렬 표창 복구)
- [x] 쿨다운 시스템

### 2-4. DamageCalculator + 데미지 판정 ✅
- [x] DealDamage 핸들러, 호스트 히트 판정 구조

### 2-5. 경험치 시스템 ✅
- [x] `ExperienceOrb`, 자석 흡수, 팀 공유 경험치, 레벨업 판정

---

## Phase 3 — 적 AI + 스폰 고도화 🟡 진행 중

### 3-1. 나머지 적 타입 🟡
- [x] 기본 추적형, 빠른형 (부분) 구현
- [ ] 둔한형 넉백 저항 튜닝
- [ ] 무리형 그룹 스폰 완성
- [ ] **원거리형 / 엘리트형 — 미구현**

### 3-2. 난이도 곡선 🟡
- [ ] 시간대별 스폰 테이블 (0-3/3-5/5-7/7-10분)
- [ ] 체력 배율 시간별 증가
- [ ] 적 타입별 등장 비율 (60/20/10/10)

### 3-3. 멀티플레이어 스케일링 🟡
- [x] 기본 스케일링 구조
- [ ] 인원수별 체력/적 수/경험치 배율 튜닝

### 3-4. 풀링 고도화 ✅ (일부)
- [x] 투사체 풀링
- [x] 이펙트 풀링 (부분)

상세 스폰 규칙은 추후 [../systems/spawn-rules.md](../systems/spawn-rules.md) 작성 시 여기에 링크.

---

## Phase 4 — 레벨업 시스템 🟡 진행 중

### 4-1. 레벨업 UI 🟡
- [x] **FrameToast / Frame_PopUp 프리팹 도입** (커밋 `84dfb3b3f`, `6d6112763`) → 알림/팝업 프레임 기반
- [x] `LevelUpPanel`, `SkillCardUI`
- [ ] 선택지 3장 RPC 전파 완성
- [ ] 타임아웃 자동 선택
- [ ] 전원 선택 완료 재개 동기화 검증

> 원래 "카드 3장 UI"로 계획했으나, 실제로는 **FrameToast/Frame_PopUp 기반 UI 프레임**으로 재설계. [../systems/ui-frame.md](../systems/ui-frame.md) 참조.

### 4-2. SkillManager 🟡
- [ ] 6슬롯 제한 관리
- [ ] 슬롯 풀일 때 기존 스킬 레벨업만
- [ ] 만렙 시 능력치 선택지 전환

### 4-3. 진화 시스템 🟡
- [ ] `EvolutionData` 테이블 참조
- [ ] 액티브 + 패시브 최대 레벨 감지
- [ ] 2슬롯 → 1슬롯 처리
- [x] **장검 진화 Phase2 복구** (커밋 `1f225a555`)

### 4-4. 패시브 스킬 적용 🟡
- [ ] 패시브 효과를 Player 스탯에 반영 (`applicableStats` 필터 경로)

---

## Phase 5 — 나머지 스킬 + 혼돈 스킬 🟡 진행 중 (**현재 브랜치 `Skill_Refactor`**)

### 5-1. Spawner 타입 완성 ✅
- [x] `AreaSpawner` (개미지옥, 성역, 번개)
- [x] `OrbitalSpawner` (장검)
- [x] `PlacedSpawner` (자동포탑) → `PlacedTurret`
- [x] `DebuffSpawner` → `DebuffMark`

### 5-2. 나머지 액티브 스킬 🟡
- [x] 매직 미사일 (+ 체인 비행 시스템)
- [x] 번개 (+ 뇌전역)
- [x] 부메랑 (`BoomerangTrajectory`)
- [x] 회오리바람 (+ Spiral)
- [ ] 스킬 #11~24 구현 (설계만 있음)
- [ ] 각 스킬별 레벨 스케일링

### 5-3. 혼돈 스킬 🟡
- [ ] 혼돈 스킬 선택 UI
- [ ] 혼돈 스킬 19종 구현 (6종 우선)
- [ ] 런타임 효과 추가 경로 (`essence_*`, `weapon_*`, `chaos_*`) — `SkillTriggerSystem.AddRuntimeEffect()`

### 5-4. 진화 스킬 🟡
- [x] 폭렬 표창, 검무(Phase2 복구), 체인 미사일, 뇌전역, 나락, 그래비톤 부메랑, 심판의 성역, 미니건 포탑, 역병 인형, 대선풍 — 각각 부분 또는 완성
- [ ] 진화 조합 데이터 SO 완성

### 5-5. 리팩터링 잔여 🟡
- [x] Trajectory/TriggerEffect 조합 체계 정착 (Trajectory enum 7종: Straight/Homing/Boomerang/Tornado/Spiral/Zigzag/SinWave)
- [x] 스킬 서브클래스 6개 삭제 (HomingProjectile/BoomerangProjectile/TornadoProjectile/SpiralTornadoProjectile/ExplodingProjectile/ChainProjectile)
- [x] `applicableStats` 필터 호출부 완성 (`PlayerStats.GetFilteredXxx` 8종 → `SkillExecutor.BuildContext`)
- [x] `SpawnProjectileHandler` 서브 프리팹 SO 필드화 (`SkillData.subProjectilePrefab` → `TriggerContext.subProjectilePrefab`)
- [ ] `IFireRecorder` 호출부·구현체·`RefireHandler` 작성 (메아리 #17 연동 시)
- [ ] 디버그 로그 정리 (Projectile/ExplodeHandler/ChainHandler 등)

---

## Phase 6 — 보스 + 네트워크 고급 ⬜ 대기

### 6-1. Boss 클래스
- [ ] 보스 기본 스펙 + 3페이즈 패턴
- [ ] 보스 등장 연출 + 체력 바 UI

### 6-2. 보스 혼돈 스킬
- [ ] 마지막 혼돈 선택 → 미선택 중 랜덤 1개 보스 부여
- [ ] 6가지 보스 변형 효과 (나머지 13종 TBD)

### 6-3. 플레이어 사망/부활
- [ ] 체력 0 → 10초 부활 타이머 → 안전 지점 (HP 50%)
- [ ] 전원 사망 → 게임 오버

### 6-4. 호스트 이탈 처리
- [ ] 5초 재연결 대기
- [ ] 실패 시 새 호스트 전환 + 비상 보스전

### 6-5. 인게임 HUD 완성
- [ ] 체력/경험치/타이머/스킬 슬롯/팀원 상태/혼돈 스킬 등

---

## Phase 7 — 마무리 + 밸런싱 ⬜ 대기

### 7-1. 결과 화면
- [ ] 클리어/실패 통계, 빌드 요약

### 7-2. 밸런싱
- [ ] 경험치 곡선 (보스 등장 시점 레벨 18-22 도달 목표 — 현재 15분 기준)
- [ ] 적 스펙, 스킬, 보스 난이도, 인원 스케일링

### 7-3. 비주얼 + 사운드
- [ ] 픽셀 아트, Bloom, BGM/SFX, 스킬 이펙트

### 7-4. 버그 수정 + 최적화
- [ ] 플레이테스트, 네트워크 엣지 케이스, 프로파일링, 빌드 테스트

---

## Phase 8 — 출시 인프라 ⬜ 대기 (설계만 완료)

출시 순서: **Stove Indie → Steam** (한국 게임 등급 분류를 Stove 로 먼저 획득).

### 8-1. Platform 추상화 (Phase A) — 선행 가능
- [ ] `Assets/Scripts/Shared/Platform/{Domain,Adapter}/` 폴더 생성
- [ ] `IPlatformService` 인터페이스 + `PlatformUserProfile` VO + `AchievementId` 상수 (Domain)
- [ ] `LocalPlatformService` (Debug.Log stub) + `PlatformBootstrap` 싱글턴 (Adapter)
- [ ] `ResultManager`, `GameStatTracker`, `Boss` 에 후크 4~5곳 추가 (`?.` 안전 호출)
- [ ] `architecture-guardian` 통과 확인

> **컨텐츠 작업과 병행 가능.** 코드 변경 최소, 게임 동작 변화 없음. 상세 [../systems/platform-integration.md § 12](../systems/platform-integration.md)

### 8-2. Photon Voice 2 통합
- [ ] Photon 대시보드에서 Voice AppId 발급
- [ ] Asset Store 에서 "Photon Voice 2" 임포트
- [ ] `PhotonVoiceNetwork` 컴포넌트 씬 배치
- [ ] Player 프리팹에 `PhotonVoiceView` + `Recorder` + `Speaker` 추가
- [ ] `Features/Voice/Adapter/VoiceController.cs` 작성 (PTT / Open Mic)
- [ ] 인게임 HUD 에 마이크 토글 UI 추가
- [ ] ParrelSync 4인스턴스 테스트

> 상세 [../systems/voice-chat.md](../systems/voice-chat.md)

### 8-3. Stove Indie 출시 (Phase B)
- [ ] Stove 인디 개발자 등록 + AppId 발급
- [ ] Stove SDK Unity 패키지 임포트
- [ ] `StovePlatformService.cs` 구현 (`IPlatformService` 충족)
- [ ] Stove 포털에 실적·통계 등록 (AchievementId 상수와 동일)
- [ ] 한국 게임 등급 분류 신청
- [ ] 빌드 설정 분기 (`PLATFORM_STOVE` define)

### 8-4. Steam 출시 (Phase C)
- [ ] Steamworks 파트너 등록 + AppId 발급
- [ ] Steamworks.NET 임포트
- [ ] `SteamPlatformService.cs` 구현
- [ ] Steam 파트너 사이트에 실적·통계 등록 (동일 ID)
- [ ] Steam 페이지 작성 + 출시 심사
- [ ] 빌드 설정 분기 (`PLATFORM_STEAM` define) + `steam_appid.txt` 배치

**선결 조건:** 8-1 완료, 컨텐츠 안정화, Stove 등급 획득

---

## 확정된 주요 설계 (2026-04-18)

- **혼돈 스킬 선택 레벨:** 레벨 10 / 20 / 30. 세부 수치는 밸런싱.
- **보스 등장 시점:** 현재 15분 기준. 밸런싱에서 10분으로 단축 가능성.

기타 밸런싱 대기 항목은 [../game-design/overview.md § 10](../game-design/overview.md) 참조.

## 병렬 작업 가이드

개발자 2명이 동시 진행 가능한 구간:

| 영역 | 개발자 A | 개발자 B |
|---|---|---|
| Phase 3/4 | 적 AI 고도화 + 난이도 곡선 | 레벨업 UI (FrameToast/Frame_PopUp 기반) + SkillManager |
| Phase 5 | 혼돈 스킬 / applicableStats 필터 완성 | 나머지 스킬 (#11~24) + 진화 조합 |
| Phase 6 | Boss 3페이즈 + 혼돈 적용 | 사망/부활 + 호스트 이탈 |
| Phase 8 선행 | 8-1 Platform 추상화 (컨텐츠 독립) | 8-2 Photon Voice 2 통합 (Player 프리팹 접근만 필요) |

## 주의사항

1. **Phase 1은 이미 통과.** 네트워크 기반은 대규모 리팩토링까지 거친 상태 (`b40a9e5d0`).
2. **Phase 5(현재 브랜치)가 최우선.** 스킬 시스템 2차 리팩토링 완료 이후 남은 잔여 작업에 집중.
3. **ScriptableObject 데이터를 먼저 채우기.** #11~24 스킬 개별 SO 에셋을 먼저 채워놓으면 구현할 때 바로 테스트 가능.
4. **Phase 5 마감 전 블로킹 설계 확정.** 혼돈 스킬 선택 레벨·보스 타이머는 더 미루지 말 것.
