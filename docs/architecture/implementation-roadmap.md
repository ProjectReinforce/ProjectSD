# Implementation Roadmap

Sweepin' Dreams 의 구현 단계 로드맵. 현재 코드 진행 상태를 반영하여 **Phase별 완료/진행 표시**를 명확히 한다.

최종 업데이트: 2026-04-25 (R10 클라이언트 적 위치 수렴 추가)

> 본 문서는 "**언제·무엇을·어떤 순서로 구현하는가**" 의 SSOT. 게임 설계 자체는 [../game-design/overview.md](../game-design/overview.md) 참조. 개별 스킬·적·시스템 설계는 해당 폴더 문서.
>
> 완료된 항목 ledger = [completed-work.md](completed-work.md). 알려진 버그 = [known-issues.md](known-issues.md). 본 문서는 **잔여**만 다룬다.

## 진행 요약

| Phase | 상태 | 비고 |
|---|---|---|
| 0. 프로젝트 셋업 | ✅ 완료 | Unity 2D + PUN2, SO 템플릿 |
| 1. 네트워크 + 메뉴 플로우 | ✅ 완료 | 메뉴/대기실/에러 처리 포함 |
| 2. 기본 전투 | 🟡 거의 완료 | 스폰 위치 명세 미달성 (캐릭터 반경 → 맵 경계로 환원) |
| 3. 적 AI + 스폰 고도화 | 🟡 진행 중 | 난이도 곡선 완료. 화면 밖 AI 간소화 잔여 |
| 4. 레벨업 시스템 | ✅ 완료 | 6슬롯/진화/패시브/수치 표시/타임아웃/StatBoost 전환 모두 동작 (2026-04-25 검증) |
| 5. 나머지 스킬 + 혼돈 | 🟡 진행 중 (**현재 브랜치 `Hyeon-Woo`**) | Phase 8-A/B 리팩터 완료. 스킬 #11~24 + 혼돈 13종 잔여 |
| 6. 보스 + 네트워크 고급 | 🟡 거의 완료 | 보스 6종 변형, 사망/부활, 호스트 이탈 동작. UI 표시 잔여 |
| 7. 마무리 + 밸런싱 | 🟡 부분 시작 | 결과 화면/경험치 곡선 완료. 수치 튜닝/비주얼/플레이테스트 잔여 |
| 8. 출시 인프라 (Voice / Platform SDK / Localization) | ⬜ 대기 (설계만) | [voice-chat.md](../systems/voice-chat.md), [platform-integration.md](../systems/platform-integration.md), [localization.md](../systems/localization.md). R3 마이크 필터 아이템 8-2 와 함께 |
| 신규 잔여 (R/U) | 🟡 미시작 | 방어력/자연회복/i-frame/메뉴 등 — 본 문서 § R, § U 참조 |

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

### 2-2. PoolManager + SpawnManager 기초 🟡
- [x] 투사체 풀링 (`ProjectileSpawner`), 기본 스폰
- [ ] **스폰 위치: 맵 경계 랜덤** — 현재 "캐릭터 기준 일정 반경" 으로 구현됨. 명세는 "맵 경계 기준 랜덤"이므로 미완료 환원 (2026-04-25)

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
- [x] 무리형 겹침 허용 (`EnemyData.resolveOverlap=false`) — Phase A
- [x] 원거리형 4변형 (고정·추격 × 투사체·경고) — Phase B
- [x] 엘리트형 — 스탯 강화 + Essence 드랍 **훅만** (Essence 시스템은 별건) + `visualScaleMultiplier` — Phase C

### 3-2. 난이도 곡선 ✅
- [x] 시간대별 스폰 테이블 (0-3/3-5/5-7/7-10분)
- [x] 체력 배율 시간별 증가 ([DifficultyManager.cs:59-65](../../Assets/Scripts/Shared/Managers/DifficultyManager.cs#L59-L65) AnimationCurve 1.0x→2.0x)
- [x] 적 타입별 등장 비율 (60/20/10/10)

### 3-3. 멀티플레이어 스케일링 🟡
- [x] 기본 스케일링 구조 + 인원수별 체력/적 수/경험치 배율
- [ ] 인원수별 배율 튜닝 (밸런싱)

### 3-4. 풀링 고도화 🟡
- [x] 투사체 풀링
- [x] 이펙트 풀링
- [ ] **화면 밖 적 간소화 AI** — 카메라 시야 외 적은 path 갱신을 N프레임마다 또는 단순 직진. 90마리 동시 운영 성능 마진

상세 스폰 규칙은 추후 [../systems/spawn-rules.md](../systems/spawn-rules.md) 작성 시 여기에 링크.

---

## Phase 4 — 레벨업 시스템 ✅ (2026-04-25 검증)

### 4-1. 레벨업 UI ✅
- [x] FrameToast / Frame_PopUp 프리팹 도입 (커밋 `84dfb3b3f`, `6d6112763`)
- [x] `LevelUpPanel`, `SkillCardUI`
- [x] 선택지 3장 RPC 전파 (`RPC_ReceiveChoices` / `RPC_ReceiveStatBoostChoices`)
- [x] 타임아웃 자동 선택 (`HandleTimeout` 미선택 플레이어 랜덤 처리)
- [x] 전원 선택 완료 재개 동기화 (`CheckAllSelected` → `EndLevelUpSequence`)

> 원래 "카드 3장 UI"로 계획했으나, 실제로는 **FrameToast/Frame_PopUp 기반 UI 프레임**으로 재설계. [../systems/ui-frame.md](../systems/ui-frame.md) 참조.

### 4-2. SkillManager ✅
- [x] 6슬롯 제한 (`MaxSlots=6` + `HasEmptySlot`/`EmptySlots` API)
- [x] 슬롯 풀일 때 기존 스킬 레벨업만 (`AcquireSkill` → `LevelUpExisting` 분기)
- [x] 만렙 시 능력치 선택지 전환 (스킬 풀 고갈 → `SendStatBoostChoices` 자동 분기)

### 4-3. 진화 시스템 ✅
- [x] EvolutionData 테이블 — `SkillData.evolutionPair`/`evolvedSkill` 필드로 SO 단위 관리
- [x] 액티브 + 패시브 최대 레벨 감지 (`CheckEvolution` + 역방향 검사)
- [x] 2슬롯 → 1슬롯 처리 (`PerformEvolution` + `PreservePassiveForEvolution`)
- [x] 장검 진화 Phase2 복구 (커밋 `1f225a555`)

### 4-4. 패시브 스킬 적용 ✅
- [x] `PlayerStats.RegisterPassive` + `applicableStats` 필터 경로 완성

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
- [x] 각 스킬별 레벨 스케일링 (`damagePerLevel[]`, `cooldownPerLevel[]`)
- [ ] 스킬 #11~24 구현 (설계만 있음)

### 5-3. 혼돈 스킬 🟡
- [x] 혼돈 스킬 선택 UI (레벨 10/20/30)
- [x] 혼돈 스킬 6종 구현 (유리대포/연쇄폭발/폭주모드/가속엔진/단결/도박꾼)
- [ ] 혼돈 스킬 나머지 13종 구현
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

## Phase 6 — 보스 + 네트워크 고급 🟡 대부분 완료

### 6-1. Boss 클래스 ✅
- [x] 보스 기본 스펙 + 3페이즈 패턴
- [x] 보스 등장 연출 + 체력 바 UI

### 6-2. 보스 혼돈 스킬 🟡
- [x] 마지막 혼돈 선택 → 미선택 중 랜덤 1개 보스 부여
- [x] 6가지 보스 변형 효과 (`BossChaosEffects.cs`)
- [ ] **보스 등장 시 혼돈 스킬 UI 표시** — `BossWarningUI.Show()` 호출은 있으나 실 표시 검증 필요
- [ ] 나머지 13종 보스 변형 (Phase 5-3 19종 확장과 함께)

### 6-3. 플레이어 사망/부활 ✅
- [x] 체력 0 → 10초 부활 타이머 → 안전 지점 (HP 50%)
- [x] 전원 사망 → 게임 오버

### 6-4. 호스트 이탈 처리 ✅
- [x] 5초 재연결 대기
- [x] 실패 시 새 호스트 전환 + 비상 보스전

### 6-5. 인게임 HUD 완성 ✅
- [x] 체력/경험치/타이머/스킬 슬롯/팀원 상태/혼돈 스킬 아이콘

---

## Phase 7 — 마무리 + 밸런싱 🟡 부분 시작

### 7-1. 결과 화면 ✅
- [x] 클리어/실패 통계, 빌드 요약, 보스 혼돈 스킬 표시

### 7-2. 밸런싱 🟡
- [x] 경험치 곡선 (보스 등장 시점 레벨 18-22 도달 목표)
- [ ] 적 스펙 조정
- [ ] 스킬 데미지/쿨타임 조정
- [ ] 보스 난이도 조정 (혼돈 스킬별)
- [ ] 2~4인 스케일링 검증

### 7-3. 비주얼 + 사운드 🟡
- [x] BGM + 효과음 적용 (AudioManager + AudioLibrary)
- [x] 캐릭터/적 아웃라인 — 단 스프라이트 애니메이션 호환성/퍼포먼스 검토는 [§ R4](#신규-잔여-작업-r) 참조
- [ ] 픽셀 아트 에셋 적용
- [ ] Bloom 후처리 (드림 테마)
- [ ] 스킬 이펙트 비주얼

### 7-4. 버그 수정 + 최적화
- [ ] 플레이테스트
- [ ] 네트워크 엣지 케이스 처리
- [ ] 성능 프로파일링
- [ ] 빌드 테스트

---

## 신규 잔여 작업 (R) — 2026-04-25 정리

`docs/check/` 의 두 임시 문서에서 통합한 잔여 + 사용자 추가 신규 항목.

### R1. 플레이어 방어력 적용한 피격 데미지 계산식 ✅ (2026-04-25)
- [PlayerHealth.ApplyDamage](../../Assets/Scripts/Features/Character/Adapter/PlayerHealth.cs) 진입점에서 `PlayerStats.DefenseMultiplier` 곱해 RPC 송신.
- 의미: DefenseMultiplier = "받는 데미지 배율" (1.0 기본, 0.95 = 5% 감소).
- 패시브 입력은 직관적 "방어력 +5%" 의도이므로 `PlayerStats.RegisterPassive` 에서 부호 반전 후 modifier 등록.
- 관련 SSOT: [../systems/damage-formula.md](../systems/damage-formula.md) — 적→플레이어 경로(§ 5) 만 적용. 적 측 방어력은 별도 작업.

### R2. 체력 자연회복 패시브 ✅ (2026-04-25)
- `StatType.HpRegen` + `PassiveBonusType.HpRegen` 신설. 단위는 HP/초.
- HP 자체는 int 유지, **누적기만 float** (`PlayerHealth.hpRegenAccumulator`). 1.0 이상 차면 정수 부분 `Heal` RPC 송신.
- HealMultiplier 곱하지 않음 (별도 산출 — 명세 준수).
- 호스트만 누적 + 송신, 모든 클라가 RPC 수신해 HP 증가.

### R3. 마이크 필터 드랍 아이템 (재미 요소)
- 드랍 시 랜덤 플레이어의 마이크에 일정 시간 필터(LowPass / Distortion 등) 적용.
- **Photon Voice 2 가능 확인됨**: 수신 측 `Speaker` AudioSource 에 Unity AudioFilter 컴포넌트 부착. 적용 대상은 RPC 동기화.
- **Phase 8-2 보이스챗 도입과 함께 진행**.
- 관련 SSOT: [../systems/voice-chat.md](../systems/voice-chat.md), [../game-design/items.md](../game-design/items.md)

### R4. 캐릭터/적 아웃라인 — 스프라이트 애니메이션 호환성 + 퍼포먼스 검토
- 현재 아웃라인은 적용됐으나, 스프라이트 애니메이션이 적용된 상태에서도 정상 반영되는지 검증 필요.
- 퍼포먼스 부하 측정 (특히 동시 90마리 적 + 아웃라인 셰이더).

### R5. 혼돈 스킬 글로벌 설정을 GameplayConfig 로 이전 적절성 검토
- 현재 연쇄폭발/단결 등 글로벌 효과는 캐릭터 프리팹의 Skills 오브젝트에 설정됨.
- 게임 설정(`GameplayConfig.asset`) 으로 이전이 적절한지 검토. **일단 기록만** (의사결정 보류).

### R6. 회오리/끌어당김 `pullRadius` 패시브 반응
- [known-issues.md B1](known-issues.md) 과 같은 코드 수정 단위. SkillRange 패시브에 영향 받도록 `pullRadius * (1 + ctx.skillRangeBonus)`.

### R7. 플레이어 무적 시간 (i-frame) ✅ (2026-04-25)
- `StatType.IFrameDuration` + `PassiveBonusType.IFrameDuration` 신설. base 0.4s.
- `PlayerHealth.ApplyDamage` 가드: `iFrameTimer > 0` 이면 데미지 무시. 호스트 측 가드(데미지 일관성).
- `PlayerVisual.HitFlashRoutine` 이 IFrameDuration 길이만큼 깜빡임 (짧으면 단일 플래시, 길면 alpha 토글).
- 패시브로 IFrameDuration 연장 가능 (Add op).

### R8. 시작 스킬: 스폰 딜레이 동안 발동 차단 ✅ (2026-04-25)
- `SpawnManager.IsReady` 정적 프로퍼티 + `RPC_NotifySpawnReady` AllBuffered 송신으로 모든 클라 동기화.
- `Skill.Update` 에 `SpawnManager.Instance.IsReady` 가드 추가 — false 면 발동 자체 차단.
- 후입장 클라(중도 참가) 도 AllBuffered 덕에 자동 수신.

### R9. 데미지 공식 — 치명타 확률·치명타 데미지 적용

**현재 상태:** 인프라가 반쯤 깔려 있으나 **일반 스킬 적중에서 미적용**. `PlacedTurret` 만 `alwaysCritical=true` 로 강제 치명타 처리 중. 명세는 [../systems/damage-formula.md § 4.3](../systems/damage-formula.md) 에 이미 정의됨 — 본 작업은 그 공식을 코드에 반영.

**이미 있는 것:**
- `StatType.CritDamage`/`CritChance` enum
- `PlayerStats.baseCritDamage` + `CritDamageMultiplier` 프로퍼티 + `PassiveBonusType.CritDamage` 매핑
- `CharacterData.critDamage` 필드
- `SkillExecutor.BuildContext` → `ctx.critDamageMultiplier` 주입 ([SkillExecutor.cs:319-322](../../Assets/Scripts/Features/Skill/Adapter/SkillExecutor.cs#L319-L322))
- `DamageResult.IsCritical`, `DamagePopup.Setup(isCrit)` (구조만)

**잔여 작업:**
- [ ] **데이터 스키마:**
  - [ ] `CharacterData.critChance` 필드 추가 (기본 0.05~0.10, 밸런싱)
  - [ ] `PlayerStats.baseCritChance` + `CritChanceProbability` 프로퍼티 — `CritDamageMultiplier` 동일 패턴(`modifiers.Calculate(StatType.CritChance, baseCritChance)`)
  - [ ] `PassiveBonusType.CritChance` enum 추가 + `PlayerStats.RegisterPassive` switch 매핑 (현재 line 329 옆)
- [ ] **컨텍스트 주입:**
  - [ ] `TriggerContext.critChance` 필드 추가 (현재 `critDamageMultiplier` 만 있음)
  - [ ] `SkillExecutor.BuildContext` 에 `data.IsStatApplicable(StatType.CritChance)` 가드로 `ctx.critChance` 주입 (line 319 옆 동일 패턴)
- [ ] **판정 로직:**
  - [ ] [DealDamageHandler.cs](../../Assets/Scripts/Features/Skill/Adapter/TriggerEffects/Handlers/DealDamageHandler.cs) 에 치명타 판정 — `Random.value < ctx.critChance` → `finalDamage *= ctx.critDamageMultiplier`, `isCrit=true` 마킹
  - [ ] `ExplodeHandler` / `ChainHandler` / `ApplyDoTHandler` 등 데미지 발생 핸들러에도 동일 정책 적용
  - [ ] `PlacedTurret.alwaysCritical` 분기를 `ctx.critChance=1f` 강제 설정으로 통일 (코드 경로 단일화) — [PlacedTurret.cs:172-173](../../Assets/Scripts/Features/Skill/Adapter/Effects/PlacedTurret.cs#L172-L173) 정리
- [ ] **네트워크 동기화 (호스트 권위):**
  - [ ] 호스트 판정 `isCrit` 결과를 데미지 RPC payload 에 포함 → 각 클라 `DamagePopup.Spawn(..., isCrit)` 동일 색상·크기 표시
  - [ ] `photon-sync-auditor` 호출 (RPC 시그니처 변경)
- [ ] **튜닝 상수:**
  - [ ] `GameplayConfig` 에 `CRIT_MULT_BASE` (1.5), `CRIT_CHANCE_BASE` (0.05) 노출 (현재 하드코딩) — [damage-formula.md § 10](../systems/damage-formula.md) 정렬
- [ ] **UI:**
  - [ ] `SkillCardDescriptionFormatter` 에 `PassiveBonusType.CritChance` 라벨 추가 (line 123, 143 옆)
  - [ ] `SkillData.applicableStats` 인스펙터에 `CritChance` 항목 노출 — `SkillDataEditor` 동시 업데이트 (메모리: Custom Editor Sync)
- [ ] **치명타 정책 (확정 — [damage-formula.md § 9](../systems/damage-formula.md) 준수):**
  - [ ] **단일 적중 내 1회:** 동시 여러 치명타 소스가 있어도 `critMult` 1회만 적용
  - [ ] **체인 / 연쇄폭발 노드별 재판정:** ChainHandler 다음 전이마다, 연쇄폭발 새 노드마다 `Random.value < critChance` 새로 굴림. `critMult` 도 노드별 독립
  - [ ] **DoT 부착 시점 스냅샷:** ApplyDoTHandler 가 부착 1회 판정 후 결과를 인스턴스에 스냅샷, 모든 틱에 동일 적용 (틱마다 재판정 안 함)
- [ ] **테스트:**
  - [ ] 단위: `critChance=1.0` 일 때 항상 `final = base * critDamageMultiplier`
  - [ ] 플레이 모드: 패시브 #15 (치명타 확률) 획득 후 데미지 팝업 색상이 호스트/클라 동일하게 표시되는지

**관련:** [damage-formula.md § 4.3](../systems/damage-formula.md), [skill-executor.md](../systems/skill-executor.md) (applicableStats 필터)

### R10. 클라이언트 적 위치 수렴 — Convergence Damping

**증상:** 적이 뭉친 상태에서 **클라이언트 측에서만** 적 위치가 떨림 (rubber-banding). 호스트는 정상.

**원인 가설:** 클라의 추적 + `ResolveEnemyOverlap` 결과가 호스트와 미세 발산 → 클라가 이동 방향으로 앞서나감 → RPC 도착 시 [ApplyNetworkCorrection](../../Assets/Scripts/Features/Enemy/Adapter/EnemyMovement.cs#L311-L330) 의 Lerp 가 뒤로 끌어당김 → 진동 사이클. 현재 Lerp 는 대칭이라 진동 자체를 못 잡음.

**정책 (확정):** 기존 Lerp **유지** + 이동축 수렴 가중치 추가. 역할 분리. 상세 [../systems/network-sync.md § 8.1](../systems/network-sync.md).

**잔여 작업:**
- [ ] **Strategy 시그니처 변경 (옵션 i):**
  - [ ] `IEnemyMovementStrategy.UpdateMovement(...)` 반환형 `void` → `Vector2` (이번 프레임 이동 델타)
  - [ ] `ChaseMovement` / `SwarmMovement` / `StationaryMovement` / `KiteMovement` 4개 구현체 모두 반환값 채움
- [ ] **EnemyMovement 가중치 적용:**
  - [ ] `Update` 에서 Strategy 반환값을 `lastMovement` 에 캐싱
  - [ ] 신규 메서드 `ApplyConvergenceDamping()` — `aheadness = -Dot(error, moveDir.normalized)`, 양수일 때만 `transform.position -= moveDir.normalized * aheadness * convergenceK * dt`
  - [ ] `ApplyNetworkCorrection` 호출 직전(또는 직후) 에 끼워넣기. 클라이언트 + `hasNetworkTarget` 가드 동일
  - [ ] `convergenceK` 인스펙터 노출 (`[SerializeField] float convergenceK = 7f;` 시작)
- [ ] **기존 Lerp 약화:**
  - [ ] [EnemyMovement.cs:61](../../Assets/Scripts/Features/Enemy/Adapter/EnemyMovement.cs#L61) `CorrectionSpeed = 5f` → `2~3f` 로 하향 (가중치와 역할 분리)
- [ ] **검증:**
  - [ ] `photon-sync-auditor` 호출 (네트워크 보정 로직 변경)
  - [ ] ParrelSync 4 인스턴스 테스트:
    - [ ] 적 50+ 마리 뭉친 상태에서 클라 측 떨림 사라지는지
    - [ ] 직각 드리프트(예: 넉백 후)는 Lerp 가 잡는지 — 가중치만으로 부족할 수 있음
    - [ ] 호스트 측 시각적 동작에 영향 없는지 (`!IsMasterClient` 가드 확인)
    - [ ] `convergenceK` 튜닝 — 너무 높으면 추적이 끊겨 보임, 너무 낮으면 효과 없음

**후속 (가중치 도입 후 효과 봐서 분기):**
- 떨림이 잔존하면 → 호스트/클라 `ResolveEnemyOverlap` 발산이 진짜 원인. 별건 작업으로:
  - 클라 측 ResolveOverlap 비활성화 (호스트 위치만 신뢰)
  - 또는 결정론적 ResolveOverlap (seed/순서 고정)

**관련:** [network-sync.md § 8.1](../systems/network-sync.md), [EnemyMovement.cs](../../Assets/Scripts/Features/Enemy/Adapter/EnemyMovement.cs)

---

## 메뉴 / UI 잔여 (U)

### U1. 방 리스트에서 플레이 중인 방 표시 안함
- `RoomListPanelController` 의 filter 조건에 `Room.IsOpen && !Room.CustomProperties["InGame"]` 추가. 방 시작 시 InGame 플래그 set.

### U2. 인원수 별 방 정렬
- 인원 많은 순으로 정렬, 만원방은 가장 아래.
- `RoomListView` 정렬 로직: `OrderByDescending(r => r.IsFull ? -1 : r.PlayerCount)`.

### U3. 혼자하기에서 나가기 → 방 리스트로
- 솔로 모드 LeaveRoom 시 `MenuSceneManager.ShowRoomList()` 라우팅.

### U4. ESC 인게임 일시정지 메뉴
- 인게임: ESC → `GameState.Paused` + 메뉴 UI (재개/설정/방 나가기). 메인씬: ESC = 뒤로가기.
- 멀티플레이 정지 정책 결정 필요 (혼자 정지 vs 전원 정지 vs 메뉴만 표시).

### U5. 결과창 "나가기" → 방 리스트로
- 현재 Title 경유. RoomList 직행으로 라우팅 (`ResultManager.OnExit` → `ShowRoomList`).

### U6. 설정창 구조잡기
- `TitlePanelController.OnClickSettings()` 가 TODO 상태. 볼륨/그래픽/키바인딩 등 항목 정리.

---

## 보류 (D) — 장기/저우선

- **랜덤입장 (자동 매치메이킹)** — 참여 가능 방 탐색 + UI 제공. 출시 후 추가.
- **이펙트 SortingLayer/OrderInLayer** — 이펙트가 Player/Enemy 보다 아래로. 코드보다 프리팹 일괄 수정 비중 큼.

---



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
- [ ] **마이크 필터 드랍 아이템 ([§ R3](#r3-마이크-필터-드랍-아이템-재미-요소))** — 수신 측 `Speaker` AudioSource 에 Unity AudioFilter 부착 + RPC 동기화

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

### 8-5. Localization — 다국어 텍스트 (KO/EN/JA/ZH-CN)

자체 구현 (Unity Localization Package 미사용). Google Sheets 가 작업용 SSOT, 빌드타임에 SO 임포트. 상세 [../systems/localization.md](../systems/localization.md).

**Phase A — 코어 시스템 + 임포터** (1.5~2일, 컨텐츠 독립 / 선행 가능)
- [ ] `Assets/Scripts/Shared/Localization/{Domain,Adapter,Editor}/` 폴더 생성
- [ ] `ILocalizationService` + `Locale` enum + (선택) `LocalizationKey` 상수 (Domain)
- [ ] `LocalizationManager` (동기 API) + `LocalizationTable` (SO) + `LocaleFontMap` (SO) + `LocalizedText` (TMP 컴포넌트) + `LocalizationBootstrap` (싱글턴, DontDestroyOnLoad) (Adapter)
- [ ] `LocalizationSheetImporter` 에디터 윈도우 — public CSV export URL 기반 (Service Account JSON 불필요)
- [ ] 빈 `Assets/Data/Localization/LocalizationTable.asset` + `LocaleFontMap.asset` 생성
- [ ] Google Sheet 템플릿 작성 (헤더: `key`, `ko_auto`, `en_auto`, `ja_auto`, `zh_auto`, `ko_final`, `en_final`, `ja_final`, `zh_final`, `note`)
- [ ] 1~2개 키로 임포트 동작 확인
- [ ] `architecture-guardian` 통과 (Domain 3 파일에 `UnityEngine`/`Photon`/`TMPro` import 없음)

**Phase B — UI 키 매핑** (수일~1주, 점진적)
- [ ] MenuScene UI(Title/RoomList/WaitingRoom/CharacterSelect)에 `LocalizedText` + 키 매핑
- [ ] InGameHUD, LevelUpPanel, ResultPanel 의 한국어 라벨에 `LocalizedText`
- [ ] `UImanager.ShowToast` → `ShowToastByKey(string key)` 시그니처 추가 (RPC 인자에 텍스트 직접 전송 금지, 키만 전달)
- [ ] 옵션 패널에 언어 드롭다운 추가 → `LocalizationBootstrap.Service.SetLocale(...)` + `SaveLocalePref(...)`
- [ ] 시트의 `ko_final` 컬럼을 기존 한국어 텍스트로 채우기

**Phase C — SO 통합 (스킬/패시브/혼돈)** (1주)
- [ ] `SkillData`/`PassiveSkillData`/`ChaosSkillData` 에 `nameKey`/`descKey` 필드 추가
- [ ] `OnValidate` 에서 키 자동 채움 (`$"skill.{skillId}.name"` 규칙)
- [ ] `SkillCardUI` 등 호출부 → `skill.GetName()` / `skill.GetDescription()` 으로 변경
- [ ] **`SkillDataEditor` 동시 업데이트** (메모리 — Custom Editor Sync)
- [ ] 시트에 스킬 24+패시브 19+혼돈 19 = 62개 행 추가 + 자동 번역 컬럼 적용

**Phase D — 검수 & 폰트** (Steam 출시 전)
- [ ] 시트 `*_final` 컬럼 검수 (도메인 용어 우선: 스킬 이름, "혼돈", "정수")
- [ ] TMP_FontAsset 4종 셋업 (NotoSans 패밀리, SIL OFL 라이선스)
- [ ] `LocaleFontMap.asset` 에 폰트 매핑
- [ ] 4개 언어 전부 플레이 모드 검증 (UI 픽셀 폭 차이로 인한 레이아웃 깨짐 확인)
- [ ] CJK 글리프 atlas 사이즈 / TMP 다이나믹 모드 hitch 측정

**선결 조건:** 없음 (Phase A 는 컨텐츠와 병행 가능). Phase D 는 Stove 출시 직전.

**비범위:** Pluralization, Gender, RTL, 시간/통화 로컬 포맷, 런타임 OTA 업데이트, RU/ES/PT-BR 등 1차 외 언어. 7개 언어 이상 + Pluralization 필요해진 시점에 Unity Localization Package 로 마이그레이션 검토.

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
| Phase 8 선행 추가 | 8-5 Localization Phase A (코어 + 임포터, 컨텐츠 독립) | 8-5 Localization Phase B (UI 키 매핑) — A 완료 후 |

## 주의사항

1. **Phase 1은 이미 통과.** 네트워크 기반은 대규모 리팩토링까지 거친 상태 (`b40a9e5d0`).
2. **Phase 5(현재 브랜치)가 최우선.** 스킬 시스템 2차 리팩토링 완료 이후 남은 잔여 작업에 집중.
3. **ScriptableObject 데이터를 먼저 채우기.** #11~24 스킬 개별 SO 에셋을 먼저 채워놓으면 구현할 때 바로 테스트 가능.
4. **Phase 5 마감 전 블로킹 설계 확정.** 혼돈 스킬 선택 레벨·보스 타이머는 더 미루지 말 것.
