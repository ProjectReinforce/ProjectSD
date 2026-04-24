# Flow Design — 화면 전환·UI·네트워크 이벤트

최종 업데이트: 2026-04-24

> **SSOT:** 이 문서는 플로우를 설명하며, 모든 구체 수치는 `Assets/Data/GameplayConfig.asset` 의 복제본이다.
> (예: 부활 10초 = `respawnDelay`, 부활 HP 50% = `respawnHPRatio`, 재연결 5초 = `reconnectWaitTime`, 선택 제한시간 15초 = `selectionTimeout`, 보스 경고 3초 = `bossWarningDuration`)

타이틀 → 로비 → 대기실 → 인게임 → 결과. 전체 시각화는 [flow-diagram.mermaid](flow-diagram.mermaid).

> **SSOT 주의:** 본 문서는 **씬/UI/이벤트 전환 플로우**만 다룬다. 게임 규칙(사망/부활, 6슬롯 등)은 [rules.md](rules.md), 구현 관리자/매니저 구조는 [systems/managers.md](../systems/managers.md), 씬 구조와 패널은 [systems/scene-structure.md](../systems/scene-structure.md), 네트워크 RPC 규약은 [systems/network-sync.md](../systems/network-sync.md) 에서 관리.

## 1. 개요

본 문서는 게임의 전체 플로우를 정의한다. 타이틀부터 결과까지의 모든 화면 전환과 사용자 상호작용, 화면별 UI 요소, 에러/예외 처리까지.

**플로우 단계 요약:**

- **타이틀 화면** — 게임 시작 진입점
- **모드 선택** — 혼자하기 / 같이하기
- **방 리스트** — 방 찾기, 방 만들기, 코드 입장
- **대기실** — 캐릭터 선택, 준비 상태, 게임 시작
- **인게임** — 자동 전투, 레벨업, 혼돈 스킬, 보스전
- **결과 화면** — 클리어/실패 통계, 재시도/나가기

## 2. 화면별 상세

### 2.1 타이틀 화면 (TitlePanel)

| UI 요소 | 타입 | 설명 |
|---|---|---|
| 게임 로고/타이틀 | 이미지 | 화면 중앙 상단 |
| 혼자하기 | 버튼 | Photon 접속 + 비공개 방 자동 생성(maxPlayers=1) → 대기실 |
| 같이하기 | 버튼 | Photon 접속 → 방 리스트 |
| 설정 | 버튼 | 사운드/해상도/키 바인딩 (추후 확장) |
| 종료 | 버튼 | 게임 종료 |
| 버전 정보 | 텍스트 | 우측 하단 |

**네트워크 이벤트**
- 혼자하기: `ConnectUsingSettings()` → `OnConnectedToMaster()` → `CreateRoom(maxPlayers=1, isVisible=false)` → `OnCreatedRoom()` → 대기실
- 같이하기: `ConnectUsingSettings()` → `OnConnectedToMaster()` → `JoinLobby()`

**에러/예외**
- Photon 접속 실패: 연결 실패 팝업 + 재시도. 3회 실패 시 상세 에러 메시지.
- Steam 미실행: Steam 실행 안내 팝업.

### 2.2 방 리스트 (RoomListPanel)

| UI 요소 | 타입 | 설명 |
|---|---|---|
| 방 리스트 | 스크롤 리스트 | 방 이름, 인원, 난이도, 맵, 비밀번호 여부(자물쇠). 클릭 시 참가 |
| 방 만들기 | 버튼 | 팝업 호출 |
| 코드 입장 | 버튼 | 팝업 호출 |
| 새로고침 | 버튼 | `OnRoomListUpdate()` 재요청 |
| 뒤로가기 | 버튼 | Photon 접속 해제 → 타이틀 |

**방 만들기 팝업**

| 항목 | 타입 | 설명 |
|---|---|---|
| 방 이름 | 텍스트 | 최대 20자. 빈칸 시 기본값("플레이어닉네임의 방") |
| 비밀번호 | 텍스트 | 선택. 입력 시 비공개(자물쇠 표시) |
| 인원 제한 | 드롭다운 | 2/3/4인 (기본 4) |
| 난이도 | 드롭다운 | TBD, 현재 "보통" 고정 |
| 맵 | 드롭다운 | TBD, 현재 "기본 맵" 고정 |

**방 코드 입장 팝업**: 방 코드 텍스트 입력 → `JoinRoom(code)`.

**네트워크 이벤트**
- 방 리스트 진입: `JoinLobby()` → `OnJoinedLobby()` → `OnRoomListUpdate()`
- 방 만들기: `CreateRoom(roomOptions)` → `OnCreatedRoom()` → 대기실
- 방 참가: `JoinRoom(roomName)` → `OnJoinedRoom()` → 대기실

**에러/예외**
- 방 꽉참: `OnJoinRoomFailed()` → "방이 가득 찼습니다" + 리스트 자동 새로고침
- 방 소멸: "방이 존재하지 않습니다"
- 비밀번호 틀림: 비밀번호 입력 팝업, 재입력
- 유효하지 않은 코드: "유효하지 않은 코드입니다"
- 방 생성 실패: `OnCreateRoomFailed()` → "방 생성에 실패했습니다"

### 2.3 대기실 (WaitingRoomPanel)

| UI 요소 | 타입 | 설명 |
|---|---|---|
| 방 정보 패널 | 텍스트 | 방 이름, 코드(복사), 난이도, 맵 |
| 참가자 리스트 | 리스트 | 이름, 캐릭터 아이콘, 준비 상태, Ping, 호스트 배지 |
| 캐릭터 프리뷰 | 2D 뷰 | 선택 캐릭터 + 스탯/특성 |
| 캐릭터 변경 | 버튼 | 팝업 호출 (준비 상태에서 비활성) |
| 준비/준비 취소 | 토글 | 준비 시 캐릭터 변경 불가 |
| 나가기 | 버튼 | `LeaveRoom` → 방 리스트 (혼자하기는 타이틀) |
| 카운트다운 | 텍스트 | 전원 준비 시 3초. 취소 시 즉시 중단 |
| 마이크 (검토 중) | 토글 | 음성 채팅 |

**캐릭터 선택 팝업**: 캐릭터 카드에 이름, 아이콘, 시작 스킬, 고유 특성, 스탯 보정 표시.

**네트워크 이벤트**
- 참가자 동기화: `OnPlayerEnteredRoom()` / `OnPlayerLeftRoom()` 콜백
- 캐릭터 변경: `SetCustomProperties({ characterId })` → `OnPlayerPropertiesUpdate()` 로 전체 동기화
- 준비 상태: `SetCustomProperties({ isReady })`. 호스트가 전원 준비 확인 후 게임 시작 RPC 전송
- 게임 시작: 호스트가 `PhotonNetwork.LoadLevel("GameScene")` 호출 (AutomaticallySyncScene=true)

**에러/예외**
- 호스트 퇴장(게임 전): `OnMasterClientSwitched()` → 새 호스트 자동 지정. 대기실 유지.
- 카운트다운 중 준비 취소 / 플레이어 퇴장: 카운트다운 즉시 취소.

### 2.4 인게임 (GameHUDPanel)

실제 게임 플레이 화면. 자동 전투, 레벨업, 혼돈 스킬 선택, 보스전 모두 이 화면에서 처리.

#### 2.4.1 인게임 HUD

| UI 요소 | 위치 | 설명 |
|---|---|---|
| 체력 바 | 좌측 상단 | 현재 HP / 최대 HP |
| 경험치 바 | 하단 중앙 | 팀 공유 경험치 |
| 레벨 표시 | 경험치 바 좌측 | 팀 레벨 |
| 타이머 | 우측 상단 | MM:SS, 보스 등장까지 |
| 스킬 슬롯 | 좌측 | 장착 스킬 아이콘 (최대 6) + 레벨 |
| 혼돈 스킬 아이콘 | 스킬 슬롯 하단 | 획득 혼돈 스킬 |
| 팀원 상태 | 우측 | 팀원별 체력 + 사망/부활 |
| 보스 혼돈 스킬 알림 | 중앙 | 보스 등장 시 `bossWarningDuration = 3초`간 표시 |
| 보스 체력 바 | 상단 중앙 | 페이즈 구분선 `phase2Threshold = 0.6`, `phase3Threshold = 0.3` |
| 부활 타이머 | 중앙 | 사망 시 `respawnDelay = 10초` 카운트다운 |

UI 프레임 시스템(FrameToast, Frame_PopUp)은 [systems/ui-frame.md](../systems/ui-frame.md) 참조.

#### 2.4.2 레벨업 강화 선택 플로우

| 단계 | 설명 |
|---|---|
| 1 | 팀 경험치 레벨업 조건 충족 (호스트 판정) |
| 2 | 게임 일시정지. 호스트가 각 플레이어별 랜덤 3개 선택지 생성 → RPC 전송 |
| 3 | 각 플레이어에게 카드 3장 표시 (아이콘, 이름, 설명, 레벨) |
| 4 | 제한시간 내 1개 선택. 미선택 시 랜덤 자동 선택 |
| 5 | 각 플레이어가 선택한 스킬 ID → 호스트 전송 |
| 6 | 전원 선택 완료 → 게임 재개 |

**혼돈 스킬 선택:** **레벨 10 / 20 / 30** 에서 일반 강화 대신 혼돈 스킬 풀에서 3개 제시. 플로우는 동일. **레벨 30 선택 시 미선택 스킬 중 랜덤 1개가 보스에게 부여**된다.

#### 2.4.3 보스전 플로우

| 단계 | 설명 |
|---|---|
| 1 | 게임 타이머 `bossSpawnTime = 900초 (15분)` 도달 → 일반 적 스폰 중단 |
| 2 | 보스 등장 연출 (경고 UI + 혼돈 스킬 아이콘/이름 3초간) |
| 3 | Phase 1 (100~60%): 기본 패턴 (추적 + 충격파) |
| 4 | Phase 2 (60~30%): 강화 (속도 증가 + 원형 지대) |
| 5 | Phase 3 (30~0%): 광폭화 (전체 슬로우 + 최대 공격성) |
| 6 | 처치 → 클리어 / 전원 사망 → 실패 → 결과 화면 |

상세 패턴은 [enemies/boss.md](enemies/boss.md).

#### 2.4.4 사망/부활 플로우

- 체력 0 → 사망 → `respawnDelay = 10초` 부활 타이머 → 안전 지점 부활 (`respawnHPRatio = 0.5`, 50% HP)
- 전원 사망 → 게임 오버 → 결과 화면(실패)
- 상세 규칙은 [rules.md § 3](rules.md).

#### 2.4.5 네트워크 이벤트 (인게임)

| 이벤트 | 발신 | 내용 |
|---|---|---|
| 적 스폰/AI | 호스트만 처리 | 위치/체력/상태 주기 전송 (20Hz) |
| 데미지 판정 | 클라 → 호스트 | 스킬 발동 알림. 호스트에서 히트 판정 + 체력 감소 |
| 투사체 | 각 클라 로컬 | 동기화 안 함 (각자 렌더링) |
| 레벨업 | 호스트 판정 + RPC | 선택지 3개 → 클라 → 선택 결과 RPC |
| 보스 페이즈 전환 | 호스트 판정 + RPC | 전체 클라이언트 동기화 |

RPC 시그니처는 [systems/network-sync.md](../systems/network-sync.md).

**에러/예외**
- 플레이어 연결 끊김: 사망 처리 + 30초 재접속 대기 (구현 측 상수) → 실패 시 영구 퇴장.
- 호스트 이탈: 게임 일시정지 → `reconnectWaitTime = 5초` 재연결 대기 → 실패 시 새 호스트 전환 + 비상 보스전 (`emergencyBossHPRatio = 0.7`).
- 레벨업 타임아웃: 미선택자만 랜덤 자동 선택, 나머지는 정상 재개.
- 동기화 오류: 호스트 데이터를 정답으로 간주. 200ms 초과 시 지연 경고 UI.

### 2.5 결과 화면 (ResultPanel, 오버레이)

| UI 요소 | 타입 | 설명 |
|---|---|---|
| 결과 타이틀 | 텍스트 | "클리어!" / "실패..." |
| 게임 통계 | 패널 | 플레이 타임, 최종 레벨, 총 처치 수, 총 데미지, 사망 횟수 |
| 플레이어별 빌드 요약 | 아이콘 리스트 | 캐릭터, 장착 스킬, 혼돈 스킬, 진화 스킬 |
| 보스 혼돈 스킬 | 아이콘 + 텍스트 | 이번 Run 보스에 적용된 스킬 |
| 다시 하기 | 버튼 | 방 유지 + `LoadLevel("MenuScene")` + `returnToWaitingRoom=true` |
| 나가기 | 버튼 | `LeaveRoom` → 타이틀 |

**네트워크 이벤트**
- 결과 동기화: 호스트가 결과 데이터(클리어/실패, 통계) RPC 전체 전송.
- 다시 하기: 호스트가 방 CustomProperties `returnToWaitingRoom=true` 설정 → `LoadLevel("MenuScene")`.
- 나가기: `PhotonNetwork.LeaveRoom()` → `OnLeftRoom()` → `SceneManager.LoadScene("MenuScene")`.

**에러/예외**
- 결과 화면에서 호스트 퇴장: 새 호스트 자동 전환.
- 팀원 부분 퇴장: 다시 하기 시 남은 인원으로 대기실 구성.

## 3. 상태 전이 테이블 (GameManager)

모든 상태 전환은 **호스트가 판정**하고 클라이언트는 동기화만 받는다.

| 현재 상태 | 이벤트 | 다음 상태 | 비고 |
|---|---|---|---|
| Lobby | 전원 준비 완료 | Loading | 3초 카운트다운 후 씬 로드 |
| Loading | 씬 로드 완료 | Playing | 적 스폰 시작 |
| Playing | 레벨업 판정 | Paused | 강화 선택 UI 표시 |
| Paused | 전원 선택 완료 | Playing | 게임 재개 |
| Playing | 보스 등장 타이머 | BossFight | 일반 적 스폰 중단 |
| BossFight | 보스 처치 | GameClear | 결과 화면 |
| BossFight | 전원 사망 | GameOver | 결과 화면 |
| Playing/BossFight | 호스트 이탈 | Paused → BossFight | 5초 대기 → 비상 보스전 |

## 4. 씬 구조 & 유지 오브젝트

2개 씬: `MenuScene` / `GameScene`. 세부 구조는 [systems/scene-structure.md](../systems/scene-structure.md). DontDestroyOnLoad 오브젝트 목록도 그쪽에 있음.

## 5. 보류 사항

- **마이크/음성 채팅:** 마이크 기능 유력. 구현 시점 미정. Photon Voice SDK 또는 Steam Voice 활용 검토.
- **텍스트 채팅:** 마이크 여부에 따라 결정.
- **난이도/맵 선택:** UI 미리 배치, 콘텐츠 추가 시 활성화.
- **설정 화면:** 기본 틀만 배치, 상세 항목 TBD.
