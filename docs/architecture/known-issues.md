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

### N1. 최초 대기실 입장 시 대기/준비 상태가 아님 → 대기 디폴트

**증상**: 방 입장 직후 상태가 모호. 대기 상태가 디폴트가 되어야 함.

**처리 방향**: `WaitingRoomPanelController` 의 입장 시 초기 상태를 명시적 "대기"로 set.

---

### N2. 적 피격 이펙트가 (0,0) 에서 생성됨

**증상**: HitEffect 가 가끔 화면 원점(0,0)에서 보임.

**조사 결과**: 호출부는 모두 `transform.position` 정상 전달.
- [Enemy.cs:169](../../Assets/Scripts/Features/Enemy/Adapter/Enemy.cs#L169), [Enemy.cs:186](../../Assets/Scripts/Features/Enemy/Adapter/Enemy.cs#L186)
- [PlayerHealth.cs:91](../../Assets/Scripts/Features/Character/Adapter/PlayerHealth.cs#L91)
- [Projectile.cs:241](../../Assets/Scripts/Features/Skill/Adapter/Projectile/Projectile.cs#L241), [Projectile.cs:256](../../Assets/Scripts/Features/Skill/Adapter/Projectile/Projectile.cs#L256)
- [Boss.cs:155](../../Assets/Scripts/Features/Boss/Adapter/Boss.cs#L155), [Boss.cs:227](../../Assets/Scripts/Features/Boss/Adapter/Boss.cs#L227)

**가능한 원인**:
1. Pool Get 직후 1프레임 prefab 기본 위치(0,0) 노출 (transform.position 적용 전 ParticleSystem 한 번 emit)
2. ParticleSystem `simulationSpace = World` 인데 spawn 시점 위치 transform 미반영
3. `OnEnable` 에서 `ps.Play()` 가 위치 set 보다 먼저 실행

**처리 방향**: [HitEffect.cs:77-93](../../Assets/Scripts/Features/Character/Adapter/HitEffect.cs#L77-L93) 의 `Play()` 에서 `transform.position = position` 후 `ps.Clear() → ps.Play()` 순서 보장. ParticleSystem `simulationSpace = Local` 로 변경 검토.

---

### N3. 플레이어 피격 후 빨간색에서 원래대로 안 돌아옴

**증상**: 가끔 피격 후 플레이어 스프라이트가 빨간색으로 고착.

**원인 식별 완료**: [PlayerVisual.cs:51-63](../../Assets/Scripts/Features/Character/Adapter/PlayerVisual.cs#L51-L63)
- `OnHit` → `StopCoroutine(hitFlashCoroutine)` 으로 이전 routine 끊기 → **이때 색을 원본으로 복원하지 않음**
- 새 routine 이 시작하며 `original = spriteRenderer.color` 캡처 → 이 값이 **빨간색** (0.2초 내 재피격이라 이전 routine 복원 전)
- 이후 영구히 빨간 색을 original로 인식 → 복원해도 빨간색

**처리 방향**: `OnHit` 시 `StopCoroutine` 직후 `spriteRenderer.color = original` 로 즉시 복원, 또는 `original` 을 정적 필드로 보관 후 매번 같은 값 사용.

---

### N4. 레벨업 패널 블러 — 캡처 시점과 게임 정지 시점 어긋남 (재발)

**증상**: 레벨업 패널 진입 시 배경 블러에 보이는 적 위치가 실제 정지된 게임 상태와 어긋남. 패널 반투명도라 시각적 어긋남이 가중됨.

**원인 추정**: `UIBackgroundBlur` 가 BlitTexture 캡처하는 프레임 ↔ `GameState.Paused` 전환 프레임 사이 1~N 프레임 갭. 그 사이 적이 한 칸 이동한 화면이 캡처됨.

**처리 방향**: 두 작업을 같은 프레임에서 atomic 하게 묶기. 옵션:
1. `LevelUpPanel.Show()` 진입 직전에 1프레임 `WaitForEndOfFrame` → `GameState.Paused` set → 다음 프레임 `UIBackgroundBlur.Capture()`
2. `UIBackgroundBlur` 가 마지막 정지 직전 프레임의 RT 를 보관하도록 매 프레임 cache

---

## B — 기존 미체크 버그

### B1. 끌어당기는 스킬 — 스킬 범위 증가 시 영향 범위도 같이 늘어나야 함

**증상**: 회오리/토네이도 등 끌어당김 효과의 영향 반경이 SkillRange 패시브에 반응하지 않음.

**원인**: [PullTrajectories.cs:57-75](../../Assets/Scripts/Features/Skill/Adapter/Trajectories/PullTrajectories.cs#L57-L75) — `pullRadius` 가 ctor 고정값. `ctx.skillRangeBonus` 미반영.

**처리 방향**: `pullRadius * (1 + ctx.skillRangeBonus)` 적용. 오브젝트 자체 시각 크기와 함께 영향 반경도 스케일.

---

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

### B6. 의문사 — 레벨업 중 적이 근접한 경우 데미지 들어옴

**증상**: 레벨업 패널 표시 중인데 가끔 플레이어가 데미지를 입음. 적이 근접해 있을 때 발생.

**원인 추정**: `EnemyContact.OnTriggerStay2D` 가 `GameState.Paused` 를 가드하지 않음. Paused 상태에서도 `damageCooldown` 만료 시 데미지 적용.

**처리 방향**: [EnemyContact](../../Assets/Scripts/Features/Enemy/Adapter/EnemyContact.cs) 의 데미지 트리거에 `GameManager.Instance.CurrentState != Playing/BossFight` 가드 추가.

---

### B7. 레벨업 중에도 피격 파티클 재생됨

**증상**: 게임 정지 중에도 HitEffect 파티클이 계속 재생.

**원인**: [HitEffect.cs:95-105](../../Assets/Scripts/Features/Character/Adapter/HitEffect.cs#L95-L105) `Update()` 에 `GameState.Paused` 가드 없음.

**처리 방향**: [TelegraphZone.cs:166-170](../../Assets/Scripts/Features/Enemy/Adapter/Attack/TelegraphZone.cs#L166-L170) 와 동일한 패턴. `Playing/BossFight` 가 아니면 `ps.Pause()` + timer 누적 건너뜀, 복귀 시 `ps.Play()`. `AnimatedEffectAutoReturn` 도 같은 가드 — 공통 `PausableEffect` 컴포넌트로 묶어 처리하는 방향 검토.

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

## V — 검증 필요

### V1. 번개 랜덤 위치가 호스트/클라 동일한지

**상태**: 사용자 메모 — "아직 확인 못함"

**검증 방법**: 호스트와 클라에서 번개 낙하 위치 비교. 시드 동기화 여부 확인. RNG 가 시드 없이 `Random.Range` 라면 양쪽이 다르게 결정됨 → 호스트가 위치 RPC 동기화.

---

### V2. 스킬 아이콘 이전 것 미삭제 버그

**상태**: 코드상 완료로 보임. 실 동작 검증 필요.

**확인 위치**: [InGameHUD.cs:242-273](../../Assets/Scripts/Features/UI/Presentation/InGameHUD.cs#L242-L273) — `RefreshAllSkillSlots` 가 `Destroy(entry.obj)` 후 재생성.

**검증 방법**: 신규 스킬을 아이콘 없는 SkillData 로 획득 시 이전 슬롯 아이콘이 그대로 남는지 재현 테스트.
