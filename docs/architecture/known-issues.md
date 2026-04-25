# Known Issues — 버그 트래커

ProjectSD 의 알려진 버그/회귀 트래커. 신규 발견 → 분석 → 수정 → ledger 이동 흐름.

수정 완료된 항목은 [completed-work.md](completed-work.md) 의 "버그 수정 / 적·보스·전투 / 사망·UI" 섹션으로 이동한다.

진행 중 잔여 작업 = [implementation-roadmap.md](implementation-roadmap.md)

분류:
- **N (신규)**: 2026-04-25 정리 시 새로 추가된 버그
- **B (기존 미체크)**: 두 원본 문서에 있었던 버그 중 잔존
- **V (검증 필요)**: 코드상 완료로 보이지만 실 동작 미확인. 검증 후 fix 또는 ledger 이동

마지막 정리: 2026-04-25

---

## N — 신규 추가

### N3. 플레이어 피격 후 빨간색에서 원래대로 안 돌아옴 — 수정 완료

**증상**: 가끔 피격 후 플레이어 스프라이트가 빨간색으로 고착.

**수정 완료** (2026-04-25): [PlayerVisual.cs](../../Assets/Scripts/Features/Character/Adapter/PlayerVisual.cs)
- `originColor` 를 Awake 에서 정적 캡처 (피격 시점 캡처 X).
- `OnHit` 시 `StopCoroutine` 직후 즉시 `spriteRenderer.color = originColor` 복원.
- `OnDeadStateChanged` 도 originColor 기반으로 alpha 만 변경하도록 보강 (사망 중 hit flash 충돌 방지).

i-frame 길이가 길면 빨간 단일 플래시 대신 alpha 깜빡임 (R7 와 통합).

---

### N4. 레벨업 패널 블러 — 캡처 시점과 게임 정지 시점 어긋남 (재발)

**증상**: 레벨업 패널 진입 시 배경 블러에 보이는 적 위치가 실제 정지된 게임 상태와 어긋남. 패널 반투명도라 시각적 어긋남이 가중됨.

**원인 추정**: `UIBackgroundBlur` 가 BlitTexture 캡처하는 프레임 ↔ `GameState.Paused` 전환 프레임 사이 1~N 프레임 갭. 그 사이 적이 한 칸 이동한 화면이 캡처됨.

**처리 방향**: 두 작업을 같은 프레임에서 atomic 하게 묶기. 옵션:
1. `LevelUpPanel.Show()` 진입 직전에 1프레임 `WaitForEndOfFrame` → `GameState.Paused` set → 다음 프레임 `UIBackgroundBlur.Capture()`
2. `UIBackgroundBlur` 가 마지막 정지 직전 프레임의 RT 를 보관하도록 매 프레임 cache

---

### N5. 다른 플레이어 매직 미사일 관통 — 수정 완료

**증상**: 자기 화면에서 다른 플레이어가 발사한 매직 미사일이 적에 hit 한 뒤 사라지지 않고 라이프타임 끝까지 비행 (시각적 관통). 본인 미사일은 정상.

**수정 완료** (2026-04-26): [Projectile.cs](../../Assets/Scripts/Features/Skill/Adapter/Projectile/Projectile.cs) OnTriggerEnter2D 의 "다른 플레이어 투사체" 분기에서 비관통(`!penetratesOverride && !trajectoryBehavior.Penetrates`) + `chainFlightCount == 0` 일 때 `ReturnToPool()` 호출. 부메랑/SinWave 등 관통 trajectory 와 체인 미사일 진화는 그대로 유지해 비행 비주얼 보존.

별개 이슈: **N14 — 매직 미사일 발사 직후 1개 간헐 소실**은 미확인 상태로 분리.

---

### N6. 클라이언트측 정수/무기 획득 효과 미적용 — 수정 완료

**증상**: 클라가 정수/무기 픽업 키 누르면 인터랙션 프롬프트는 뜨지만 키 눌러도 효과 미적용 + 호스트 측에선 픽업 처리 자체가 안 됨.

**진짜 원인**: [PickupItemBase.TryInteract](../../Assets/Scripts/Features/Pickup/Adapter/PickupItemBase.cs) 의 `if (PhotonNetwork.IsMasterClient) OnPickedUpByPlayer(...)` 가드 — 클라이면 `OnPickedUpByPlayer` 호출 안 되고 `isCollected=true` + 풀 반환만 실행됨. 즉 클라 측에선 시각적으로 사라지지만 RPC_Equip / 무기 RequestAddOrCombine 송신 자체가 일어나지 않음. 정수도 무기도 같은 경로.

**수정 완료** (2026-04-26):
- PickupItemBase 의 클라 분기를 `PlayerStub.RequestPickupFromClient(pos, itemId)` RPC 위임으로 변경.
- 호스트 [PlayerStub.RPC_HostPickup](../../Assets/Scripts/Features/Character/Adapter/PlayerStub.cs) 가 위치+itemId 로 자기 측 가까운 인스턴스 찾아 `ProcessPickupAsHost` 처리.
- 처리 후 `DropSpawner.NotifyPickupCollected` (PickupCollectedEvent.EventCode=14, RaiseEvent Receivers.Others) 로 다른 클라에 풀 반환 알림 — 호스트만 자기 풀 정리하면 클라 측 stale 인스턴스로 다음 픽업 매칭 실패.
- PhotonServerSettings RpcList 에 `RPC_HostPickup` 추가.

---

### N7. 다시하기 → 대기실 복귀 시 먼저 도착한 측 화면에 캐릭터 2개 (B8 연관) ⚠️

**증상**: 게임 종료 → 다시하기 → 대기실 복귀 시, 먼저 돌아온 측 화면에만 LobbyPlayer 가 두 개 다 보임 (자기 캐릭터 + 상대 캐릭터 둘 다 자기 위치 근처).

**연관**: [B8](#b8-다시하기-두번째-플로우부터-호스트가-클라-복제체-생성-클라-동작-불가-) 와 같은 뿌리(GameScene → MenuScene 전환 시 PhotonView 정리 누락) 가능성 높음.

**처리 방향**: B8 디버깅 세션과 묶어서 진행. `returnToWaitingRoom` 시점에 GameScene 의 Player PhotonView 가 명시적으로 Destroy 되는지 확인.

---

### N8. 다시하기 → 대기실 복귀 시 우측 플레이어 상태 패널 겹침

**증상**: 양쪽(호스트/클라) 모두 우측 플레이어 상태 UI(호스트 표시, 캐릭터, 강퇴 버튼, 준비 상태)가 한 슬롯에 겹쳐 보임. 클라가 레디 토글 또는 캐릭터 변경하면 정상 복원됨.

**원인 추정**: WaitingRoomPanelController 가 대기실 재진입 시 슬롯 컨테이너를 clear/rebuild 하지 않고 stale 상태로 첫 렌더. CustomProperties 변경 콜백이 들어와야 비로소 리프레시되는 구조로 보임.

**처리 방향**: `WaitingRoomPanelController.OnEnable` 또는 복귀 진입점에서 슬롯 컨테이너 자식 destroy → 현재 룸 인원 기준 강제 rebuild.

---

### N9. 클라이언트 측 새로고침 횟수가 -1 로 표시 + 사용 불가 — 수정 완료

**증상**: 클라가 본 새로고침(스킬 카드 리롤) 카운트가 -1. 클릭해도 카드 갱신 안 됨 + 호스트 콘솔에 RPC parameter mismatch 에러.

**진짜 원인 두 가지**:
1. `clientRefreshRemainingCache = -1` sentinel 이 첫 RPC_SyncRefreshRemaining 도착 전까지 UI 에 그대로 노출.
2. PhotonServerSettings RpcList 에 `RPC_ReceiveRefreshChoices` / `RPC_RequestRefresh` / `RPC_SyncRefreshRemaining` 미등록 → byte index 미스매치로 dispatch 실패.

**수정 완료** (2026-04-26):
- [Levelupmanager.cs](../../Assets/Scripts/Features/Progression/Adapter/Levelupmanager.cs) `LocalPlayerRefreshRemaining` getter 가 캐시 < 0 이면 `BaseRefreshCharges` 로 fallback. 일반 스킬 패널 송신(SendNormalChoices) 시 `SyncRefreshRemainingTo(player)` 도 함께 호출해 첫 sync 즉시 정확값 표시.
- PhotonServerSettings RpcList 에 새로고침 RPC 3종 등록.

---

### N10. 1 사이클 후 다시하기 → 대기실 → 게임 시작 시 호스트 측에 더미 캐릭터 (B8 연관) ⚠️

**증상**: 다시하기 → 대기실 → 게임 시작 시 호스트 측에만 추가 캐릭터 하나 더 등장. 어그로 끌리고 부활도 계속됨. 호스트/클라 둘 다 죽어도 더미가 살아 있어 게임 종료 트리거 안 됨. 스킬도 둘 중 누군가의 것을 복사해서 사용 (정확히 누구건지 미확인, 둘 다였을 가능성 있음).

**위험도**: ⚠️ 매우 높음. 출시 차단.

**연관**: [B8](#b8-다시하기-두번째-플로우부터-호스트가-클라-복제체-생성-클라-동작-불가-), N7 과 동일 뿌리 추정. 이전 라운드의 Player PhotonView 가 cleanup 되지 않고 두 번째 라운드에 잔존.

**처리 방향**: B8/N7/N10 묶어서 별도 디버깅 세션. 후보:
- 씬 언로드 시 강제 Player cleanup
- `PhotonNetwork.RemoveBufferedRPCs()` + Owner Destroy
- Player 풀링 도입 (Instantiate 대신 재사용)
- 게임 종료 트리거가 "살아있는 PhotonPlayer 수" 기준이라면 "AlivePlayerActor 가 PhotonNetwork.PlayerList 에 포함" 가드 추가

---

### N11. Photon RPC / Destroy 미스매치 로그 — N5/N7/N10/B8 정황 증거

**증상**: 콘솔에 다음 4종 경고/에러 동시 발생.

**[클라이언트측]**
```
Received RPC "RPC_Heal" for viewID 2013 but this PhotonView does not exist! View was/is ours. Remote called. By: #01
Received RPC "RPC_TakeDamage" for viewID 1010 but this PhotonView does not exist! Was remote PV. Owner called. By: #01 Maybe GO was destroyed but RPC not cleaned up.
[LevelUpManager] ActorNumber 1의 SkillManager 못 찾음   (Levelupmanager.cs:1171)
[LevelUpManager] Sync 실패 — Actor 1의 SkillManager 없음 (Levelupmanager.cs:1140)
```

**[호스트측]**
```
Ev Destroy Failed. Could not find PhotonView with instantiationId 2012. Sent by actorNr: 2
```

**해석**:
- viewID prefix 2xxx = 클라(actor 2) 스폰, 1xxx = 호스트(actor 1) 스폰
- 호스트→클라 `RPC_Heal(viewID=2013)`: R2 자연회복 RPC. 클라 PlayerHealth(소유 PV) 가 이미 destroy 된 상태에서 호스트가 계속 송신 → **N7/N10/B8 (Player cleanup 누락)** 의 직접 증거.
- 호스트→클라 `RPC_TakeDamage(viewID=1010)`: 호스트가 자기 소유 객체(적 또는 호스트 Player) 에 데미지 RPC 송신. 대상 GO 는 이미 destroy. 미사일이 hit 후에도 계속 데미지 시도 → **N5 (매직 미사일 hit 후 destroy 안됨)** 증거 또는 적 사망 후 잔여 RPC.
- `RPC_SyncSkillAcquisition` 시점에 클라 측에 호스트 Player 의 SkillManager 가 없음 → 다시하기 후 호스트 Player 미스폰 또는 stale destroy → **N10/B8** 동근.
- 호스트의 `Ev Destroy Failed (instantiationId 2012, actorNr 2)`: 클라가 자기 PV destroy 이벤트를 보냈으나 호스트는 이미 그 PV 를 모름 → 양쪽 destroy 시점 비대칭 → **N7/N10/B8** 동근.

**처리 방향**: 단독 항목 아님. N5 / N7+N10+B8 묶음 디버깅 시 이 로그가 **회귀 검증의 카나리아**. 수정 후 위 4종 경고가 콘솔에서 사라져야 완료로 간주.

**부분 진행** (2026-04-26):
- R2 자연회복 RPC 잔존: [PlayerHealth.Update](../../Assets/Scripts/Features/Character/Adapter/PlayerHealth.cs) 의 GameState 가드를 `gm == null` 까지 포함해 강화 → MenuScene 전환 직후 stale Player 에 RPC_Heal 송신 차단.
- 다시하기 cleanup 1차: [ResultManager.OnRetry](../../Assets/Scripts/Shared/Managers/ResultManager.cs) 를 코루틴으로 분리 (PhotonNetwork.Destroy → 1프레임 yield → SendAllOutgoingCommands → LoadScene) + [GamePlayerSpawner](../../Assets/Scripts/Features/Character/Adapter/GamePlayerSpawner.cs) 중복 스폰 가드.
- 잔존: 호스트 측 "Could not find PhotonView with instantiationId ... Sent by actorNr: 2" 일부 잔존, 캐릭터 복제 양상 여전 → N7/N10/B8 다음 세션에서 추가 디버깅.

---

### N12. 성역(회복 장판) 회복 2회 발동 — 수정 완료

**증상**: 클라가 발동한 성역(또는 진화 심판의 성역)에 들어갔을 때 회복이 2회씩 누적 적용. 호스트 발동은 1회 (정상).

**원인**: [AreaZone.Update](../../Assets/Scripts/Features/Skill/Adapter/Effects/AreaZone.cs) 의 가드 `if (!isLocalPlayerOwned && !PhotonNetwork.IsMasterClient) return;` 가 데미지 C안 (클라가 자기 장판도 ApplyTick) 을 위해 자기 장판은 호스트+클라 양쪽에서 ApplyTick 실행. 그런데 `ApplyHealTick` 가 `PlayerHealth.Heal()` 직접 호출 → `RPC_Heal RpcTarget.All` 송신 → 양쪽이 송신해 2회 누적.

**수정 완료** (2026-04-26): `ApplyHealTick` 진입부에 `if (!PhotonNetwork.IsMasterClient) return;` 추가. 회복은 호스트 권위로 통일 (데미지 C안 경로와 분리). 인원 수 무관 1회.

---

### N14. 매직 미사일 발사 직후 간헐 소실 — 미확인

**증상**: 매직 미사일 1회 발사에서 1개가 발사 직후 사라짐. 적과 hit 이 아닌데 즉시 풀 반환되는 듯. 동시에 2개 사라지는 케이스는 미확인.

**원인 추정**: 발사 위치가 플레이어 위치 → 적이 플레이어 collider 안에 있으면 spawn 즉시 OnTriggerEnter2D 트리거 → 비관통 미사일이 첫 hit 으로 판정되어 ReturnToPool. 다만 사용자 보고에 적과 hit 한 것이 아니라 "사라짐"이라 — 본인 미사일도 N5 의 다른 플레이어 분기 fix 영향을 받을 가능성 (chainFlight=0 + 비관통 케이스). 별개 trajectoryBehavior 처리 race 가능.

**처리 방향**: 발사 직후 짧은 grace period (`aliveTime > 0.05f` 가드) 로 spawn 직후 trigger 무시 또는 발사 위치를 `playerPosition + direction * 0.3f` 로 보정. 동작 빈도 낮아 후순위.

---

## B — 기존 미체크 버그

### B2. 토네이도가 보스에 맞으면 프레임 600→100 드랍

**증상**: 토네이도 + 보스 조합에서 큰 프레임 드랍.

**처리 방향**: 프로파일러 측정 필요. 보스에 토네이도 push 시도가 매 프레임 무거운 듯. 보스는 mass scaling 적용 또는 pull 무시 옵션.

---

### B3. 플레이어 나가도 팀원 상태 패널에 사라지지 않음

**증상**: HUD 의 TeammateEntry 가 OnPlayerLeftRoom 시 제거되지 않음.

**처리 방향**: `InGameHUD` 의 TeammateEntry 관리부에 `OnPlayerLeftRoom` 콜백 등록 → 해당 entry destroy.

---

### B4. 스웜 타입은 다른 모든 적과 겹쳐도 됨 (현재 충돌함)

**증상**: 스웜 적이 다른 적 타입과도 충돌 처리됨.

**원인**: `EnemyData.resolveOverlap = false` 는 있으나 Collider2D Layer 분리 미적용.

**처리 방향**: Layer "EnemySwarm" 신설 + Physics2D Matrix 에서 EnemySwarm ↔ 다른 Enemy 충돌 무시.

---

### B5. 토네이도 발사 방향 동기화 안됨

**증상**: 호스트와 클라이언트가 보는 토네이도 발사 방향이 다름.

**처리 방향**: 발사 방향 결정 로직이 로컬 입력/위치에 의존하면 클라마다 다르게 결정됨. 호스트가 RPC 로 방향 전파하거나, 입력 시점 방향을 RaiseEvent 로 동기화.

---

### B8. 다시하기 두번째 플로우부터 호스트가 클라 복제체 생성, 클라 동작 불가 ⚠️

**증상**: 첫 플로우는 정상. 다시하기로 두번째 라운드 진입 시:
- 호스트 측: 클라이언트의 캐릭터 복제체가 같이 보임 (스킬은 클라 것을 따라가지만 데미지는 따로). 부활도 별개.
- 클라이언트 측: 동작 불가. 몬스터도 멈춤. 호스트 캐릭터 안 보임. 레벨업 1회 이후 로직만 동작.

**위험도**: ⚠️ 매우 높음. 출시 차단 버그.

**처리 방향**: GameScene 재진입 시 PhotonView/Player 객체가 정리되지 않는 문제로 추정. 별도 디버깅 세션 필요. 후보:
- 씬 언로드 시 강제 cleanup
- Player 풀링 도입
- `returnToWaitingRoom` 흐름에서 `PhotonNetwork.RemoveBufferedRPCs()` + Owner Destroy

---

### B9. 보스 등장 중 호스트 마이그레이션 시 보스 사라짐

**증상**: 보스가 출현한 상태에서 호스트가 넘어가면 보스가 사라짐.

**원인**: [BossSpawner.cs:197-204](../../Assets/Scripts/Features/Boss/Adapter/BossSpawner.cs#L197-L204) `ResetForMigration` 이 보스 파괴 후 재스폰만 함. 보스 상태(체력/페이즈/위치) 이관 없음.

**처리 방향**: 보스 상태를 RoomCustomProperty 로 보관 → 마이그레이션 후 새 호스트가 복원하여 재스폰.

---

## F — Follow-up (2026-04-25 audit 잔여)

> **운영 정책 (2026-04-25):** 게임 중 플레이어 중도 참가는 지원하지 않음. 모든 플레이어가 대기실에서 시작 → 게임 시작 후 신규 참가 차단. 따라서 중도 참가 관련 RPC 동기화 보강(buffered/late-join) 작업은 우선순위에서 제외.

### F1. iFrame 호스트 마이그레이션 후 부활 미보호
- `PlayerHealth.iFrameTimer` 가 호스트 측 변수. 호스트 마이그레이션 후 새 호스트가 부활한 플레이어 측 iFrame 을 모름.
- 처리 방향: Respawn 시 호스트가 명시적으로 iFrame RPC 송신 또는 RespawnManager 가 부활 직후 N초 무적을 모든 클라에 알림.

### F2. RequestQuestReward 가 isLevelUpActive 시 무시 ✅ (2026-04-25)
- `pendingQuestRewards` 큐 신설. RequestQuestReward 시 진행 중이면 큐 적재 → EndLevelUpSequence 에서 레벨업 큐 비운 뒤 dequeue 처리.

### F3. RequestSpawnReady AllBuffered 마이그레이션 윈도우
- 마이그레이션 직후 1초 startDelay 동안 후입장 클라가 isReady=false 로만 보일 수 있음. 새 호스트가 1초 뒤 다시 AllBuffered 송신 → 그 시점부터 정상.
- 큰 이슈는 아니나 후입장 시 1초 가량 스킬 발동 안 되는 윈도우가 있음.

### F4. AdoptLevelUpSession 이 playerChoices/playerPanelKinds 미복원
- `LevelUpManager.AdoptLevelUpSession` 이 새 호스트로 인수 시 선택지 자체를 복원하지 못해 타임아웃 시 랜덤 선택 불가.
- 별도 디버깅 세션 필요.

### F5. EnemyData 가 다른 Feature(Quest) 에서 직접 참조
- Architecture-guardian 경고. Quest 가 격리몹 SO 를 직접 의존하므로 Feature 격리 위반.
- 처리 방향: `EnemyData` 를 `Shared/Data/` 로 승격 (Spawn/Quest/Boss 모두 소비하기 시작하면 우선순위 상승).

### F7. 격리 몹이 KillTarget 카운트에 이중 잡힐 가능성 ✅ (2026-04-25)
- `OnEnemyDied` 에서 `questBarrierIds.Contains(enemy.EnemyId)` 가드 — 격리 몹은 NotifyEnemyKilledToAllActive 호출에서 제외.

### F8. QuestZone.activeZones 호스트 마이그레이션 stale
- 정적 리스트가 마이그레이션 시 명시적 clear 없음. `ResetForMigration` 류 훅 신설 권장.

### F9. pendingQuestRewards 호스트 마이그레이션 유실
- LevelUpManager 호스트 측 큐. 마이그레이션 시 보상 손실. 빈도 낮아 우선순위 낮음. 필요 시 RoomProperty 로 승격.

---

## V — 검증 필요

### V1. 번개 랜덤 위치가 호스트/클라 동일한지

**상태**: 사용자 메모 — "아직 확인 못함"

**검증 방법**: 호스트와 클라에서 번개 낙하 위치 비교. 시드 동기화 여부 확인. RNG 가 시드 없이 `Random.Range` 라면 양쪽이 다르게 결정됨 → 호스트가 위치 RPC 동기화.

---

### V2. 스킬 아이콘 이전 것 미삭제 버그

**상태**: 코드상 완료로 보임. 실 동작 검증 필요.

**확인 위치**: [InGameHUD.cs:242-273](../../Assets/Scripts/Features/UI/Presentation/InGameHUD.cs#L242-L273) — `RefreshAllSkillSlots` 가 `Destroy(entry.obj)` 후 재생성.

**검증 방법**: 신규 스킬을 아이콘 없는 SkillData 로 획득 시 이전 슬롯 아이콘이 그대로 남는지 재현 테스트.
