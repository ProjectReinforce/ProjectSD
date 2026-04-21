# Network Sync — Photon 동기화 규약 (SSOT)

Sweepin' Dreams 의 모든 네트워크 동기화는 이 문서의 규약을 따른다. 각 Feature 문서(스킬/적/보스)는 "이 규약을 따른다"만 적고 세부는 여기서 관리.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `network-sync` |
| 분류 | 네트워크 |
| 의존 레이어 | Adapter (Network, Entity, Skill) |
| 최종 업데이트 | 2026-04-18 |

## 2. 네트워크 구조

**Photon PUN 2 기반 호스트-클라이언트 구조.**

- **호스트 (MasterClient):** 방을 생성한 플레이어. 게임 로직 판정 주체.
- **클라이언트:** 렌더링·입력 주체. 자신의 이동은 로컬 예측, 호스트와 동기화.
- **Steam P2P** 로 연결 (최대 10MB/s, 실제 부담 ~0.022MB/s).

## 3. 역할 분담

### 호스트의 역할
- 적 스폰 판정·관리 (타이밍, 위치, 종류)
- 적 AI (추적 대상 선정, 이동 경로)
- **데미지 판정** (스킬 hit → 적 체력 감소)
- 적 사망·경험치 분배
- 보스 패턴 실행·페이즈 전환
- 모든 레벨업 판정, 스킬 선택지 생성
- 상태 전이 (`GameState`) 판정

### 클라이언트의 역할
- **자신의 플레이어 이동 로컬 예측**, 호스트와 동기화
- **자신의 스킬 발동 이펙트/쿨타임 로컬 처리**, 히트 판정만 호스트로
- 호스트로부터 받은 적 정보 렌더링
- **적 위치 보간(interpolation)** 으로 부드러운 이동
- 히트 이펙트/사운드 재생

## 4. 동기화 데이터

### 호스트 → 클라이언트 (주기 전송, 20Hz)

| 데이터 | 크기 |
|---|---|
| 적 리스트 (ID 4B, 위치 12B, 체력 4B, 상태 1B) | 21B × N |
| 보스 (위치, 체력, 페이즈, 다음 공격 타이밍) | ~30B |
| 다른 플레이어 위치 (호스트 중계) | 20B × (N-1) |
| 경험치/레벨 | 수 byte |

**예측 (2인 기준):** 적 50 × 21B = 1050B, 플레이어 2 × 20 = 40B, 보스 30B → **약 1.1KB/틱**. 20Hz → **약 22KB/s** = 0.022MB/s.

### 클라이언트 → 호스트 (이벤트 기반)

- 플레이어 이동 입력
- 스킬 발동 알림 (어떤 스킬, 어디서)
- 강화 선택 결과 (레벨업 시)

## 5. 주요 RPC / 이벤트 목록 (실제 코드 기준)

> 본 표는 실제 `[PunRPC]` 어트리뷰트가 붙은 메서드의 실측 목록이다. 새 RPC 를 추가/제거하면 본 표를 동기화한다.

### 5-1. 게임 진행 (Shared/Managers/)

| 명칭 | 정의 위치 | 용도 |
|---|---|---|
| `RPC_SyncExp(int currentExp, int requiredExp, int level, int levelUpCount)` | `Shared/Managers/GameManager.cs:117` | 호스트 → 전체. 팀 경험치/레벨 동기화 |
| `RPC_ChangeState(int stateInt)` | `Shared/Managers/GameManager.cs:151` | 호스트 → 전체. `GameState` 전이 (Playing/Paused/BossFight/GameClear/GameOver) |
| `RPC_SendBuildToHost(int actorNumber, string playerName, int characterId, int[] skillIds, int[] skillLevels, int[] chaosIds)` | `Shared/Managers/ResultManager.cs:139` | 클라 → 호스트. 종료 시 로컬 빌드 데이터 전송 |
| `RPC_ShowResult(bool isCleared, float playTime, int teamLevel, int totalKills, int totalDeaths, int bossChaos, int[] buildPayload)` | `Shared/Managers/ResultManager.cs:222` | 호스트 → 전체. 결과 화면 표시 |

### 5-2. 스폰 / 데미지 (Shared/Managers/SpawnManager.cs)

| 명칭 | 라인 | 용도 |
|---|---|---|
| `RPC_SpawnEnemy(int enemyId, int enemyTypeInt, Vector2 position, float hpMultiplier)` | 409 | 호스트 → 전체. 일반 적 스폰 |
| `RPC_SpawnSwarm(int enemyId, Vector2 position, float hpMultiplier, float baseAngle)` | 443 | 호스트 → 전체. 무리형 스폰 |
| `RPC_SpawnRanged(int enemyId, int variantIdx, Vector2 position, float hpMultiplier)` | — | 호스트 → 전체. 원거리형 스폰. **⚠ variantIdx = `SpawnManager.rangedVariants[]` 배열 인덱스. 배열 순서 변경 금지 (리모트 간 variant 불일치). 요소는 말미에만 추가** |
| `RPC_SpawnElite(int enemyId, int eliteIdx, Vector2 position, float hpMultiplier)` | — | 호스트 → 전체. 엘리트 스폰. **⚠ eliteIdx = `SpawnManager.eliteVariants[]` 배열 인덱스. rangedVariants 와 동일 '순서 고정' 계약** |
| `RPC_SpawnEnemyProjectile(Vector2 pos, Vector2 dir, float speed, int damage, float lifetime)` | — | 호스트 → 전체. 원거리 적 투사체 스폰. 이동은 각 클라 로컬, 데미지는 호스트 판정 |
| `RPC_SpawnTelegraph(Vector2 pos, float duration, float radius, int damage)` | — | 호스트 → 전체. 경고존 스폰. Strike(데미지) 판정은 호스트만 |
| `RPC_RequestDamage(int enemyId, int damage, int actorNumber)` | 506 | 클라 → 호스트. 적 피해 요청 (C안 데미지 요청) |
| `RPC_RequestKnockback(int enemyId, Vector2 sourcePos, float force)` | 513 | 클라 → 호스트. 넉백 요청 |

### 5-3. 스킬 트리거 (Shared/Network/NetworkAdapter.cs)

| 명칭 | 라인 | 용도 |
|---|---|---|
| `RPC_NotifySkillTriggered(int actorNumber, int skillId)` | 19 | 호스트 → 전체. 스킬 발동 통지 (이펙트/사운드용) |
| `RPC_NotifyDamageApplied(int targetViewId, float damage)` | 25 | 호스트 → 전체. 데미지 적용 통지 (DamagePopup 표시용) |

### 5-4. 보스 (Features/Boss/Adapter/)

| 명칭 | 정의 위치 | 용도 |
|---|---|---|
| `RPC_RequestBossDamage(int damage)` | `Boss.cs:196` | 클라 → 호스트. 보스 피해 요청 |
| `RPC_SyncHP(int hp, int maxHp, int phaseInt, bool phaseChanged)` | `Boss.cs:217` | 호스트 → 전체. 보스 HP / Phase 동기화 |
| `RPC_BossDied()` | `Boss.cs:247` | 호스트 → 전체. 보스 처치 |
| `RPC_SetBossChaosSkill(int chaosTypeInt)` | `BossChaosApplicator.cs:109` | 호스트 → 전체. 보스 혼돈 적용 |
| `RPC_ShowCircleWarning(float x, float y, float radius, float delay)` | `BossPhaseManager.cs:318` | 호스트 → 전체. 보스 패턴 경고 비주얼 |
| `RPC_ShowExplosion(float x, float y, float radius)` | `BossPhaseManager.cs:341` | 호스트 → 전체. 폭발 이펙트 |
| `RPC_ApplyGlobalSlow(float multiplier, float duration)` | `BossPhaseManager.cs:362` | 호스트 → 전체. 전역 슬로우 |
| `RPC_RemoveGlobalSlow()` | `BossPhaseManager.cs:373` | 호스트 → 전체. 슬로우 해제 |
| `RPC_BossWarning(float duration)` | `BossSpawner.cs:225` | 호스트 → 전체. 보스 등장 사전 경고 |
| `RPC_InitBoss(int bossViewID, int maxHP)` | `BossSpawner.cs:240` | 호스트 → 전체. 보스 인스턴스 초기화 |

### 5-5. 레벨업 / 진행도 (Features/Progression/Adapter/Levelupmanager.cs)

| 명칭 | 라인 | 용도 |
|---|---|---|
| `RPC_ReceiveChoices(int[] choiceIds, bool isChaos)` | 263 | 호스트 → 각 클라. 레벨업 선택지 전달 |
| `RPC_StartTimer(float duration)` | 289 | 호스트 → 전체. 선택 타이머 시작 |
| `RPC_PlayerSelected(int actorNumber, int skillId)` | 323 | 클라 → 호스트. 선택 결과 보고 |
| `RPC_LevelUpEnded()` | 531 | 호스트 → 전체. 레벨업 종료 (게임 재개) |
| `RPC_ForceChoice(int skillId)` | 547 | 호스트 → 전체. 타임아웃 자동 선택 |
| `RPC_SyncSkillAcquisition(int actorNumber, int skillId)` | 568 | 호스트 → 전체. 스킬 획득 동기화 |

### 5-6. 부활 (Features/Character/Adapter/RespawnManager.cs)

| 명칭 | 라인 | 용도 |
|---|---|---|
| `RPC_StartRespawnCountdown(int photonViewID, float totalTime)` | 216 | 호스트 → 전체. 부활 카운트다운 시작 |
| `RPC_ExecuteRespawn(int photonViewID, int respawnHP, float posX, float posY)` | 227 | 호스트 → 전체. 부활 실행 (위치/HP) |

### 5-7. 씬 전환 / 룸 상태

| 명칭 | 방향 | 용도 |
|---|---|---|
| `PhotonNetwork.LoadLevel("GameScene")` | 호스트 → 전체 | 씬 전환 (AutomaticallySyncScene=true) |
| `OnMasterClientSwitched` | Photon 자동 | 호스트 마이그레이션. `Shared/Managers/HostMigrationHandler.cs` 가 처리 |
| `SetCustomProperties({characterId})` | 플레이어 | 캐릭터 선택. 키 `NetworkManager.CharacterIdKey` |
| `SetCustomProperties({isReady})` | 플레이어 | 준비 상태. 키 `NetworkManager.IsReadyKey` |
| `SetCustomProperties({hasPw, pw})` | 호스트(룸 생성 시) | 비밀번호 방. 키 `HasPasswordKey`, `PasswordKey` |
| `SetCustomProperties({startCountdownActive, startCountdownEndTime})` | MasterClient | 대기실 카운트다운 상태. 상세 [waiting-room.md § 3](waiting-room.md) |
| `PhotonNetwork.CloseConnection(player)` | MasterClient → 대상 | 대기실 강퇴. `NetworkManager.Awake` 에서 `EnableCloseConnection = true` 로 선활성화 (PUN 기본값 false) |

각 RPC 의 실제 시그니처는 위 정의 위치 (`Shared/Managers/`, `Shared/Network/`, `Features/{Boss,Progression,Character}/Adapter/`) 의 코드를 참조한다. 새 RPC 추가 시 `photon-sync-auditor` 서브에이전트가 본 표 기준으로 감사한다.

## 6. 소유권 / 권한

| 오브젝트 | 소유자 | 비고 |
|---|---|---|
| Player | 해당 클라 | 이동 로컬 예측 |
| Enemy | Scene / 호스트 | AI 호스트 실행 |
| Boss | Scene / 호스트 | |
| Projectile | 각 클라 로컬 | **동기화 안 함.** 히트 판정만 호스트 |
| ExperienceOrb | Scene / 호스트 | 흡수 판정 호스트 |
| Area/Placed | Scene / 호스트 | 틱 판정 호스트 |

## 7. 런타임 TriggerEffect 추가 규약

[trigger-effects.md § 5](trigger-effects.md) 와 동기. 정수/무기/혼돈 스킬은 런타임에 `SkillTriggerSystem.AddRuntimeEffect(source, effect)` 로 추가되며, **호스트가 권위.** 각 클라는 호스트 결정을 RPC로 동기화 받는다.

source 명명 규칙:
- `essence_{name}` — 정수 속성
- `weapon_{name}` — 무기 부가효과
- `chaos_{name}` — 혼돈 스킬
- `buff_{name}` — 일시 버프

## 8. 최적화 전략

### 데이터 압축
- 화면 밖 적: 위치만, 체력 생략
- 이동하지 않는 적: 20Hz → 10Hz
- Delta compression: 변경 값만 전송

### 클라이언트 예측
- 플레이어 이동: 로컬 즉시 반영 + 호스트 보정
- 적 이동: 마지막 속도 벡터로 예측, 호스트 데이터로 보간
- 스킬 발동: 로컬 즉시 이펙트, 데미지만 호스트 확인
- **Dead Reckoning** — 커밋 `b40a9e5d0` 참조 (멀티플레이어 동기화 2차 리팩토링).

## 9. 실패 / 동기화 오류 처리

- 패킷 손실 시: 이전 프레임 데이터로 보간
- **200ms 초과 지연**: 클라이언트에 경고 UI
- **호스트 연결 끊김:** 게임 일시정지 → 5초 재연결 대기 → 실패 시 새 호스트 전환 + 비상 보스전. 상세는 [../game-design/rules.md § 6](../game-design/rules.md).
- **동기화 오류 (체력 불일치 등):** **호스트 데이터를 정답으로 간주.**
- **플레이어 연결 끊김:** 사망 처리 + 30초 재접속 대기. 실패 시 영구 퇴장.

## 10. 테스트

- ParrelSync 로 2~4 인스턴스 멀티 테스트
- `photon-sync-auditor` 서브에이전트를 네트워크 관련 PR 전에 호출
- 플레이테스트: 200ms 지연·패킷 손실·호스트 이탈 시나리오

## 11. 기존 코드 참조

- `Assets/Scripts/Shared/Network/NetworkAdapter.cs` — 트리거/데미지 통지 RPC
- `Assets/Scripts/Shared/Managers/NetworkManager.cs` — Photon 연결·룸 콜백·CustomProperties 키 정의
- `Assets/Scripts/Shared/Managers/HostMigrationHandler.cs` — MasterClient 전환 처리
- `Assets/Scripts/Features/Character/Adapter/Player*.cs`, `Features/Enemy/Adapter/Enemy.cs`, `Features/Boss/Adapter/Boss.cs` — PhotonView 부착 컴포넌트

## 12. 알려진 제약

- [ ] 호스트 마이그레이션 시 기존 적 스폰 리스트의 소유권 이전이 매끄러운지 검증 필요
- [ ] 클라이언트 스킬 발동 → 호스트 히트 판정의 레이턴시 허용 범위 수치화 필요
- [ ] 부동소수점 오차로 클라 간 미세 차이 가능 — 허용 범위 문서화 필요
