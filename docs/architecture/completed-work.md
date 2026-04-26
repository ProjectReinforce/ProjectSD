# 완료 작업 ledger

ProjectSD(Sweepin' Dreams) 의 완료된 작업 회고/추적용 ledger.
`docs/check/` 폴더의 임시 두 문서(`수정 사항.md`, `작업 진행 상황.md`)에서 체크된 항목 + 2026-04-25 정리 시 신규 확정한 항목들을 한 곳에 모았다.

진행 중인 잔여 작업 = [implementation-roadmap.md](implementation-roadmap.md)
알려진 버그 = [known-issues.md](known-issues.md)

---

## 시스템 / 아키텍처

- Feature-first + Clean Architecture 전환 (커밋 `78853035f`)
- 스킬 시스템 2차 리팩터링 (`deb23b669` ~ `3422f8def`)
- 멀티플레이어 동기화 대규모 리팩토링 — Dead Reckoning + C안 데미지 요청 (`b40a9e5d0`)
- 혼돈 스킬 4등급 체계 — `paramsByRarity[4]` (Phase 7-8-A, 커밋 `027604f01`)
- StatBoost 통합 SO 리팩터 (Phase 7, 커밋 `60f7dc950`)
- Hook Registry 인프라 + ChainExplosion handler 이전 (Phase 8-B, 커밋 `d9fae5665`)
- Gambler handler 이전 + 등급 상승 로직 (Phase 8-B3, 사용자 확인)
- StatBoost SO 통합 (`StatBoost_AttackAdd/AttackMultiple/HPAdd/...` 단일 SO)
- 플레이어 방어력 적용 데미지 공식 (R1, 2026-04-25) — PlayerHealth.ApplyDamage 진입점 + RegisterPassive 부호 반전
- 체력 자연회복 패시브 (R2, 2026-04-25) — StatType.HpRegen + 호스트 누적기 + Heal RPC
- 플레이어 i-frame (R7, 2026-04-25) — StatType.IFrameDuration + ApplyDamage 가드 + PlayerVisual 깜빡임
- 시작 스킬 발동 가드 (R8, 2026-04-25) — SpawnManager.IsReady AllBuffered 동기화 + Skill.Update 가드
- 치명타 확률·데미지 적용 (R9, 2026-04-26) — `StatType.CritChance` + `PlayerStats.baseCritChance/CritChanceProbability` + `CharacterData.critChance` + `TriggerContext.critChance/critDamageMultiplier` + `SkillExecutor.BuildContext` 주입. 신규 `CritJudgment.Roll` 헬퍼로 데미지 사이트(Projectile/AreaZone/OrbitalObject/PlacedTurret) + 핸들러(Deal/Explode/Chain/DoT/Nearby) 적용. `PlacedTurret.alwaysCritical` → `critChance=1f` 통일. RPC `RequestDamage(...,isCrit)` / `Boss.RequestDamageFromClient(damage,isCrit)` 호스트 화면 색상 동기화 (Phase A: self-judging, 다른 클라 동기화는 Phase B 보류). `GameplayConfig.critMultBase/critChanceBase` 노출. `damage-formula.md § 11` Phase A 정책 명문화. 부산물: `PassiveBonusType.CritChance` enum 끝 위치(시프팅 방지) / `SkillCardDescriptionFormatter.IsPercentPassive` CritChance 라벨 / DoTEffect Update GameState 가드(정지 중 도트 정지) / `SkillDataEditor.applicableStats` 자동 노출.
- N3 빨간색 고착 수정 (2026-04-25) — PlayerVisual.originColor 정적 캡처 + StopCoroutine 직후 즉시 복원
- N2 피격 이펙트 (0,0) 잔상 (2026-04-25) — 실제 원인은 사용자가 prefab 교체 시 root GameObject 에 HitEffect 스크립트 미부착. 코드 측은 견고화: `GetComponentsInChildren<ParticleSystem>(true)` 로 부모-자식 모든 ps 캐싱 + 명시 Play(true) + AudioSource 캐싱 + SetActive 를 Play(position) 안으로 이동(소리 위치 정확).
- Phase 6 퀘스트 MVP 인프라 (2026-04-25) — QuestType/State/Data SO + QuestZone 상태 머신 + RewardDispatcher + LevelUpManager.RequestQuestReward
- Phase 6 격리 몹 + 킬 카운트 연결 (2026-04-25) — `SpawnManager.SpawnQuestBarriers`/`DespawnEnemies` + `QuestZone.activeZones` 레지스트리 + `OnEnemyDied` 통지 경로 + F2 보상 큐잉
- F7 격리 몹 KillTarget 이중 카운트 가드 (2026-04-25) — `questBarrierIds` 기반 1줄 가드
- R6/B1 pullRadius 패시브 반응 (2026-04-25) — `TrajectoryFactory.Create` 에 `skillRangeBonus` 인자 추가, `effectivePullRadius = data.pullRadius + ctx.skillRangeBonus`
- B6 EnemyContact Pause 가드 (2026-04-25) — Playing/BossFight 외 상태에서 접촉 데미지 발생 차단
- B7 HitEffect Pause 가드 (2026-04-25) — Update 에서 ParticleSystem.Pause/Play + returnTimer 정지
- N1 대기실 입장 대기 디폴트 (2026-04-25) — `readyToggle.SetIsOnWithoutNotify(false)` 즉시 동기화
- 스킬 새로고침 시스템 (2026-04-25) — 일반 스킬 패널 한정, 카운트 기반(기본 `GameplayConfig.baseSkillRefreshCharges=2` + 혼돈 스킬 `LevelUpManager.AddRefreshChargesToAll(N)` 진입점). 호스트 권위 + 본인 클라 캐시 + RPC_SyncRefreshRemaining 으로 UI 동기화. LevelUpPanel 인스펙터에 refreshButton/refreshCountText 슬롯.
- **R11 World Indicator UI** (2026-04-26) — 화면 안 머리 위 이름표 / 화면 밖 가장자리 화살표+테두리색+이름. 히스테리시스 β 표준(ε=0.05). 클라이언트 로컬(네트워크 동기화 없음). `Features/UI/Adapter/Indicator/` (IWorldIndicatorTarget / IndicatorPolicy / PlayerColorPalette / WorldIndicatorManager) + `Features/UI/Presentation/Indicator/WorldIndicatorView` + 어댑터 3종(`PartyMemberIndicatorAdapter` / `BossIndicatorAdapter` / `QuestIndicatorAdapter`). Manager 가 정적 `pendingTargets` 큐로 Awake race 차단. 파티원=AlwaysShow(슬롯 4색) / 보스=OffScreenOnly(빨강) / 랜덤 퀘스트=OffScreenOnly(자주). `QuestData.isRandom` 플래그로 맵 고정/랜덤 분류. `WorldIndicator.prefab` 신규 + GameScene 에 Manager + Canvas 2종(World/Screen) 배치. 상세 [world-indicator.md](../systems/world-indicator.md). 부산물 fix: 퀘스트 흰원 InProgress 진입 시 숨김 + RPC_SyncState 클라 동기화 / 팀원 HP UI disconnect entry 자동 정리 / LevelUpManager OnPlayerLeftRoom override 로 선택지 도중 disconnect 즉시 재개.

---

## Phase 0 — 프로젝트 셋업

- Unity 2D 프로젝트, Git, PUN2 + App ID, ParrelSync
- 폴더 구조 (Adapter/Domain/Data/Application/BootStrap)
- ScriptableObject 템플릿 (`CharacterData`, `SkillData` 7서브타입, `EnemyData`, `BossData`, `DifficultyData`, `GameplayConfig`, `AudioLibrary`)
- MenuScene / GameScene 2개 씬 + Build Settings 등록

## Phase 1 — 네트워크 + 메뉴 플로우

- `NetworkManager` DontDestroyOnLoad 싱글톤, Photon 콜백 중앙 처리
- `MenuSceneManager`, `TitlePanelController`, `RoomListPanelController`, `WaitingRoomPanelController` + 패널 전환
- 캐릭터 선택 (CustomProperties 동기화) — `CharacterSelectUI`
- 준비 상태 토글, 전원 준비 → 호스트 수동 Start → 3초 카운트다운 → 씬 전환
- 카운트다운 취소 처리
- 월드 공간 `LobbyPlayer` + 오버헤드 UI(이름/Host/Ready) + 호스트 Kick — [waiting-room.md](../systems/waiting-room.md)
- `GameManager` + GameState, `Player` 프리팹/이동/카메라/`AutomaticallySyncScene`
- 결과 → 대기실 복귀 (`returnToWaitingRoom` 플래그)
- 에러 처리 (호스트 퇴장, 연결 끊김) — 사용자 확인

## Phase 2 — 기본 전투

- `Enemy`, ChaseMovement, 호스트 AI, 접촉 데미지
- 투사체 풀링 (`ProjectileSpawner`)
- `Skill`, `SkillExecutor`, `ISkillSpawner` + 5종 구현체 (Projectile/Area/Orbital/Debuff/Placed)
- 표창 + 진화형 폭렬 표창 복구
- 쿨다운 시스템
- DealDamage 핸들러, 호스트 히트 판정
- `ExperienceOrb`, 자석 흡수, 팀 공유 경험치, 레벨업 판정

## Phase 3 — 적 AI + 스폰 고도화 (대부분 완료)

- 빠른형/둔한형/무리형 적 (부분~완성)
- 무리형 겹침 허용 (`EnemyData.resolveOverlap=false`)
- 원거리형 4변형 (고정·추격 × 투사체·경고)
- 엘리트형 — 스탯 강화 + Essence 드랍 훅 + `visualScaleMultiplier`
- 시간대별 스폰 테이블, 적 등장 비율, 인원수 스케일링
- 체력 배율 시간별 증가 (`DifficultyManager.GetHealthMultiplier()` AnimationCurve 1.0x→2.0x)
- 투사체 풀링 + 이펙트 풀링 (`PoolManager` + `IPoolable`)
- 멀리 있는 적 사망 사운드 거리 기반 컷오프 — 사용자 확인

## Phase 4 — 레벨업 시스템

- FrameToast / Frame_PopUp 도입 (커밋 `84dfb3b3f`, `6d6112763`)
- `LevelUpPanel`, `SkillCardUI` 카드 UI
- 6슬롯 제한, 슬롯 풀일 때 기존 스킬 레벨업만, 만렙 시 능력치 선택지 전환 (포션으로 채움 — 사용자 확인)
- `EvolutionData` 테이블 + 액티브+패시브 최대 레벨 감지 + 2슬롯→1슬롯
- 장검 진화 Phase2 복구 (커밋 `1f225a555`)
- 패시브 효과 Player 스탯 반영 (`applicableStats` 필터)
- 스킬 획득 시 수치 표시 — 사용자 확인

## Phase 5 — 스킬 시스템

- Spawner 타입 5종 완성 (Projectile/Area/Orbital/Debuff/Placed)
- 액티브 스킬: 매직 미사일(체인), 번개(뇌전역), 부메랑, 회오리바람(Spiral)
- 각 스킬별 레벨 스케일링 (`damagePerLevel[]`, `cooldownPerLevel[]`)
- 진화 스킬: 폭렬 표창, 검무, 체인 미사일, 뇌전역, 나락, 그래비톤 부메랑, 심판의 성역, 미니건 포탑, 역병 인형, 대선풍 (각 부분/완성)
- 진화 SO `triggerEffects` — 코드상 구현 완료 (사용자 확인). 신규 진화 추가 시 별도 작업 필요
- Trajectory enum 7종 (Straight/Homing/Boomerang/Tornado/Spiral/Zigzag/SinWave) + TriggerEffect 조합 체계
- 스킬 서브클래스 6개 통합 삭제 (HomingProjectile/BoomerangProjectile/TornadoProjectile/SpiralTornadoProjectile/ExplodingProjectile/ChainProjectile)
- `applicableStats` 필터 8종 호출부 완성
- `SpawnProjectileHandler` 서브 프리팹 SO 필드화
- 혼돈 스킬: 유리 대포, 연쇄 폭발, 폭주 모드, 가속 엔진, 단결, 도박꾼 (6종 완성)
- 혼돈 스킬 선택 UI (레벨 10/20/30)
- 스킬 maxInstances 제한 (Area/PlacedSpawner 큐)
- 호스트-클라 스킬 동기화: 투사체 로컬 시뮬레이션 + 호스트 판정, 토네이도 결정적 로컬 이동

## Phase 6 — 보스 + 네트워크 고급 (부분 완료)

- 보스 기본 스펙 + 3페이즈 패턴, 등장 연출, 체력 바 UI
- 6가지 보스 변형 효과 (`BossChaosEffects.cs` — GlassCannon/ChainExplosion/Berserk/AccelEngine/Unity/Gambler)
- 레벨 30 마지막 미선택 혼돈 → 보스 부여
- 플레이어 사망/부활 (10초 타이머 + 안전 지점 + HP 50% + 전원 사망 게임 오버)
- 호스트 이탈 처리 (5초 재연결 대기 + 새 호스트 전환 + 비상 보스전)
- 인게임 HUD: 체력/경험치/타이머/스킬 슬롯/팀원 상태/혼돈 스킬 아이콘

## Phase 7 — 마무리 (부분 시작)

- 결과 화면: 클리어/실패 통계, 빌드 요약, 보스 혼돈 스킬 표시
- 경험치 곡선 조정 (보스 등장 시점 레벨 18-22 도달 목표)
- BGM + 효과음 적용 (AudioManager + AudioLibrary)
- 캐릭터/적 아웃라인 처리 — **재검토 필요** ([known-issues.md](known-issues.md) 참조)

---

## 메뉴 / UI

- 방 생성 시 리스트 표시 (UI 프리팹 기반)
- 방 클릭 진입 + 비밀번호 팝업 (`Slot_RoomList` + `Frame_InputPassWord`)
- 검색/생성 인풋필드 초기화 (방 진입/뒤로가기/팝업 닫기)
- 방찾기 플로우 (RoomList → PasswordUI → 진입)
- 새로고침 시 방 추가/삭제 + 인원수 동기화 (Diff 방식)
- 새로고침 중 버튼 비활성화 (쿨다운)
- 모든 유저 레디 시 호스트 시작 가능 (`CanMasterStartGameInCurrentRoom`)
- 방장이 유저 강퇴 (`KickPlayer` RPC + MasterClient 검증)
- 결과창 다시하기/나가기 분기 (`ResultManager.OnExit/OnRetry`)
- 혼돈 스킬 HUD 아이콘 (`ChaosIcon.prefab` + `RefreshChaosIcons`)
- 스킬 아이콘 갱신 (`RefreshAllSkillSlots` Destroy + 재생성) — 실 동작은 [known-issues.md V2](known-issues.md) 검증
- 레벨업 패널 블러 페이드인 (`UIBackgroundBlur` + DOTween) — 단, 캡처 타이밍 어긋남 재발 ([known-issues.md N4](known-issues.md))
- 부활 대기 UI 표시
- 결과창 팀원 빌드 표시
- 보스 UI 클라 동기화
- 클라 경험치/레벨 디버그 텍스트 동기화
- 호스트 마이그레이션 시 호스트 연결 대기 UI

---

## 대기실 / 캐릭터 선택

- 뒤로가기 → 방 리스트
- 게임 시작 시 캐릭터 선택 잠금
- 캐릭터 선택 = 선택 버튼 + 확인 버튼 확정
- 캐릭터 선택 안해도 준비 가능 → 디폴트 캐릭터 A로 해결 (사용자 확인)
- 카운트다운 중 입장 차단
- 혼자 다시하기 후 팀원 미복귀 시작 차단
- 타이틀 → 캐릭터 정보 넘기기

## 적 / 보스 / 전투

- 캐릭터·적 아웃라인 처리 (스프라이트 애니메이션과의 호환·퍼포먼스 검토는 잔여 — [implementation-roadmap.md R4](implementation-roadmap.md))
- 적 피격 시 색 변화
- 데미지/이펙트 처리
- 넉백 적용
- 보스전 시작 시 적 멈춤 안 함 (있던 적은 유지)
- 부메랑 전방위
- 토네이도 방향 (확인됨) — 단 호스트/클라 시야 차이는 잔여 ([known-issues.md B5](known-issues.md))
- 토네이도 클라 끌어당김
- 토네이도 진화 표시 + 따라오기
- 보스전 시 스킬 멈춤 후 재개
- 클라 적 죽는 타이밍 동기화 (핑 영향)
- 클라 적 사망 사운드
- 패시브 사라질 때 효과 지속 → 진화 시 깔끔히 종료
- 레벨업 중 보스 폭발 공격 차단
- 클라 적 버벅임 보정
- 신규 스킬 핑 텍스트
- 죽은 상태 호스트 나가면 패배 처리
- 클라 피격 데미지/이펙트/넉백
- 번개 같은 자리 여러 번 (호스트/클라 동일 시드는 잔여 — [known-issues.md V1](known-issues.md))
- 유리대포: 본인만 적용 + 최대체력 절반 처리
- 장검 진화 발사 페이즈 (커밋 `1f225a555`)
- 장검 진화 투사체 갯수 적용
- 몬스터 군집 스티어링 알고리즘 (레이트 업데이트로 겹침 처리)

## 사망 / UI 잔존

- 사망 시 UI 표시
- UI 출력 시 백그라운드 블러
- 최대 체력 증가 적용
- 부활 대기 UI 표시
- 데미지/이펙트/넉백 처리

---

## 정책 결정 (2026-04-25)

- 방 재입장 시 캐릭터 정보 = **초기화** (출시 후 유저 피드백 시 재검토)
- 준비 완료 후 캐릭터 변경 = **불가** (UI 잠금)
- 장검 등 시작 스킬 = **스폰 딜레이 동안 스킬도 미발동** (명세 변경 — 작업은 [implementation-roadmap.md R8](implementation-roadmap.md))
- 혼돈 스킬 글로벌 설정(연쇄폭발/단결) 위치 = **현재 캐릭터 프리팹의 Skills 오브젝트** (이전 적절성 검토는 [implementation-roadmap.md R5](implementation-roadmap.md) 보류)

---

## 환원된 항목 (다시 잔여로)

- **Phase 2-2: 스폰 위치 맵 경계 랜덤** — 이전에 체크됐으나, 현재 구현은 "캐릭터 기준 일정 반경 스폰"이라 명세("맵 경계 기준 랜덤")와 다름. [implementation-roadmap.md Phase 2-2](implementation-roadmap.md) 로 환원.

---

## 드랍 시스템 구현 (Phase 0 ~ 7)

`drop-system-roadmap.md` (2026-04-21 승인본) 의 단계별 완료 내역을 ledger 로 통합. 잔여 작업은 [implementation-roadmap.md](implementation-roadmap.md) 의 해당 섹션 + § R / § U 로 흡수.

### Priority 0 — 선행 버그픽스
- **P0.1 게임 시작 시 경험치 UI 이미 차있는 현상** ✅ — 원인 식별 + 수정 + 회귀 테스트
- **P0.2 경험치 오브 동시 존재 수량 제한** ✅ — `GameplayConfig.maxActiveExpOrbs=200` + `SpawnManager.activeOrbs` FIFO 추적 + `OnExpOrbReturned` 알림. 상한 도달 시 정책: **드랍 생략** (병합 아님)

### Phase 0 — 공통 인프라 ✅
- `Rarity` enum + `RarityWeightedRoller` (순수 C# 정적 유틸) + `RarityPoolChoiceGenerator` (공통 등급 선정기 — 카드 3장 동일 등급 규칙 SSOT)
- `IPickup` / `PickupType` / `PickupItemBase` (Pickup Feature 베이스. `IPoolable` + 자석 + GameState 체크 + 호스트 권위)
- `EnemyDropTable` SO (Shared/Data 승격) + `DropSpawner` (호스트 전용)
- `EventCode_DropSpawnBatch = 13` (`DropSpawnBatch.cs`)
- `GameplayConfig` / `EnemyData.dropTable` 필드 연결

### Phase 1 — ExperienceOrb 리팩터링 ✅
- `ExperienceOrb` → `PickupItemBase` 상속 전환. 자석/호스트 체크 베이스 위임. `OnPickedUpByPlayer` 만 `GameManager.AddExp` 호출
- `SpawnManager.SpawnExpOrb` 경로 유지 (XP는 100% 드랍이라 DropSpawner 경로 불필요)

### Phase 2 — 자석 / 물약 ✅
- `MagnetPickup` (`RPC_ActivateMagnet` 브로드캐스트, ExperienceOrb 만 끌어오는 필터)
- `PotionPickup` (호스트 권위 `PlayerHealth.Heal(baseHeal × HealMultiplier)`, 획득자만 회복)
- `Magnet.prefab` / `Potion.prefab`
- `SpawnManager.OnEnemyDied` → `DropSpawner.TrySpawnDropsForEnemy` 연동
- `EventCode_DropSpawnBatch` 송수신
- `IHealable` 포트 (Pickup → Character 경계)
- 자석 RPC 1프레임 지연 (RPC/RaiseEvent 채널 순서 보장)
- `HostMigrationHandler.DropSpawner.ResetForMigration` 연동
- `GameplayConfig.dropScatterRadius` (드랍 위치 분산)

### Phase 3 — 정수(Essence) 시스템 ✅ (HUD 일부 잔여)
- `EssenceType` enum (Ice/Fire/Lightning, Domain)
- `EssenceData` SO + `EssenceDatabase` SO (`GameManager.EssenceDB` SSOT)
- `EssencePickup` + `PlayerEssenceInventory` (최대 2슬롯, AllBuffered RPC)
- `SkillTriggerSystem.AddRuntimeEffect` 주입 + source 슬롯 네이밍 + Stack2 시너지
- 상호작용 기반 획득 UX (Space 키 + `CanBePickedUpBy` 2슬롯 가득 시 차단)
- OnHit/OnKill 트리거 전 스킬 일관화 (Projectile / AreaZone / PlacedTurret / OrbitalObject)
- `DamageNearbyHandler` 신규 액션 (번개 정수용. `primary=반경, secondary=수, tertiary=데미지`)
- `DebugOverlay` — Essence + `T:{base}+{runtime} H:{onHit}` 표시
- 잔여: `EssenceSlotsUI` HUD, `EssenceCombo` VO (조합 히든 효과 — 설계서 TBD)

### Phase 4 — 무기(Weapon) 시스템 ✅ 코드 완료 (유저 Unity 배선 대기)
- **W2 포트 추출**: `IRuntimeEffectSink` (`Shared/Domain/Interfaces`) — `SkillTriggerSystem` 구현, `PlayerEssenceInventory` 도 포트 의존으로 전환
- `WeaponStatEntry` / `WeaponCombineRecipe` VO (Domain, 순수 C#)
- `WeaponData` SO + `WeaponDatabase` SO (`GameManager.WeaponDB`)
- `WeaponPickup` (`PickupItemBase` 상속, `RequiresInteraction` + `PromptExtraInfo` 조합 프리뷰)
- `PlayerWeaponInventory` (4슬롯, AllBuffered `RPC_EquipWeapon`/`RPC_CombineWeapon`, 조합 매처)
- `DropSpawner` Weapon 분기 — `WeaponDatabase.All` 인덱스를 `dataIdHash` 로 전송
- **데미지 공식 재설계 (2026-04-24)**: `PlayerStats.ApplyAttackTo(skillBase, skillData)` — `(skillBase + ΣAdd + skillBase × ΣPercentBonus) × ΠMultiplicative × baseAttackMultiplier`. AttackMultiplier 패시브는 `PercentBonus` op 자동 등록 (`bonusPerLevel` 그대로 "+N%" 해석)
- **3-op ModifierOp 전환**: `Multiply` → `Multiplicative` 리네임 + `PercentBonus` 신설. 모든 혼돈 호출처는 `Multiplicative` 매핑
- **Per-entry isUnique**: `WeaponStatEntry.isUnique` + 신규 `WeaponTriggerEntry { effect, isUnique }`. `triggerEffects` → `triggerEntries` 교체
- **Source 네이밍**: unique = `weapon_{id}_u_e{entryIdx}` (슬롯 무관 1회분), non-unique = `weapon_{id}_s{slotUid}_e{entryIdx}` (슬롯별 독립)
- **slotUid 결정성**: 호스트가 할당 후 RPC 에 실어 전달. host migration 후 어긋남 차단
- **RPC 이름 변경**: `RPC_Equip` → `RPC_EquipWeapon`, `RPC_Combine` → `RPC_CombineWeapon` (Essence `RPC_Equip(int)` 충돌 차단)
- 잔여: `WeaponSlotsUI` / 조합 프리뷰 HUD, **유저 Unity 배선** (WeaponData SO 5~8종, WeaponDatabase, GameManager 할당, Weapon.prefab, DropSpawner.weaponPrefab, Player 자식 PlayerWeaponInventory + PhotonView)

### Phase 5 — 능력치(StatBoost) 시스템 ✅ 코드 완료 (유저 Unity 배선 대기)
- `StatBoostData` SO / `StatBoostDatabase` SO
- `StatBoostManager` (`IPlayerStatsMutator` 포트 경유 — Character.Adapter 직접 참조 없음)
- `StatBoostChoiceService` (Phase 0 `RarityPoolChoiceGenerator` 재사용)
- `LevelUpManager` — "만렙" = **스킬 풀 고갈** 감지. `RPC_ReceiveChoices` 시그니처 `bool isChaos` → `int panelKindInt` 확장 (`ChoicePanelKind { Skill, Chaos, StatBoost }` enum)
- RPC 3종: `RPC_PlayerBoostSelected` / `RPC_SyncBoostAcquisition` / `RPC_ForceBoostChoice` (타임아웃 랜덤 선택 분기)
- `UIManager.ShowLevelUpStatBoost` / `LevelUpPanel.SetupStatBoost` / `SkillCardUI.SetupAsStatBoost` — 카드 프리팹 재사용
- `GameManager.StatBoostDB` SSOT
- `DebugOverlay` StatBoosts 섹션
- **통합 SO 방식 (2026-04-24)**: `StatBoostData` 1개가 `valueByRarity[4]` 보유 → SO 수 1/4 감소, 한 SO 에서 등급 밸런싱
- **중복 누적**: source = `stat_{boostId}_{rarity}_{localCounter}` 로 자연 누적
- 잔여: 유저 Unity 배선 (StatBoostData SO + Database + GameManager 슬롯 + Player 자식 StatBoostManager)

### Phase 6 — 퀘스트(Quest) 시스템 🟡 코드 측 핵심 완료 (HUD/핸들러 잔여)
- `QuestType` / `QuestState` (Domain enum)
- `QuestData` SO (`Features/Quest/Adapter/Data/QuestData.cs`)
- `QuestZone` 호스트 권위 상태머신 (KillTarget MVP, `RPC_SyncState` OthersBuffered)
- `QuestRewardDispatcher` → `LevelUpManager.RequestQuestReward` (StatBoost 경로 재사용)
- `LevelUpManager.RequestQuestReward` 신규 public API + `pendingQuestRewards` 큐 ([known-issues.md F2](known-issues.md))
- `SpawnManager.SpawnQuestBarriers(EnemyData, center, radius, count)` + `DespawnEnemies(int[])` + `RPC_SpawnQuestBarrier`
- `SpawnManager.OnEnemyDied` → `QuestZone.NotifyEnemyKilledToAllActive()` 정적 호출 + `activeZones` 레지스트리
- F7 격리 몹 KillTarget 이중 카운트 가드 (`questBarrierIds` 1줄 가드)
- 잔여 (→ implementation-roadmap.md): QuestProgressUI HUD / DodgeFalling·Defend·KillInTime 핸들러 / 맵 배치 / 유저 Unity 배선

### Phase 7 — 혼돈 스킬 등급 적용 ✅ 코드 완료 (유저 Unity 등급 재지정 대기)
- `SkillData.rarity` 필드 추가 (Active/Passive/Chaos 공통, 기본 Common)
- `SkillManager.GenerateChaosChoices` → `RarityPoolChoiceGenerator` 전환 (StatBoost 와 동일 공통기, 카드 3장 항상 동일 등급)
- `SkillCardUI.SetupTypeBadge` 혼돈 분기 — 라벨 `혼돈 · {Rarity}` + 등급 색상
- 잔여: `BossChaosApplicator` 등급 가중치 (선택 — 별도 기획), 유저 Unity 등급 재지정 (혼돈 SO 19종)

### Phase 8 — 혼돈 스킬 하드코딩 제거 (W5)
- **8-A** ✅ (커밋 `027604f01`) — `ChaosSkillData.paramsByRarity[4]` + 혼돈별 독립 modifier + 올바른 op 전환
- **8-B 인프라** ✅ (커밋 `d9fae5665`) — `IChaosHookBus` / `IChaosEffectHandler` / `ChaosEffectRegistry` 신설. 4종 훅 (EnemyKilled / PlayerTakeDamage / PlayerDeath / LevelUpChoice). `ChaosSkillManager` 가 hook bus 구현
- **8-B ChainExplosion handler 이전** ✅ — `ChainExplosionHandler` 가 `EnemyKilled` 훅 구독 + 프레임 리셋 자체 관리. `ChaosSkillManager` 의 switch case / `hasChainExplosion` flag / `GetChainExplosionConfig` / `TriggerExplosionDamage` / `SpawnExplosionVisual` / `IsLocalPlayer` 제거
- **8-B3 Gambler handler 이전** ✅ (2026-04-25) — `GamblerHandler` 가 `IsActive` 플래그만 노출, `ChaosSkillManager.IsGambler` 는 registry 조회 래퍼
- **Gambler rarity bump 소비자** ✅ — `GamblerRarityBumper` 정적 + `LevelUpManager.ResolveGamblerOverride` / `IsAnyPartyGambler`. `StatBoostChoiceService.GenerateChoices` / `SkillManager.GenerateChaosChoices` 에 `overrideRarity` 옵션 파라미터. 분포표 bump (Common 100% +1, Rare 90/10, Epic 80/20, Legendary 70/20/10)
- **8-C StatWatcher 공용 컴포넌트** ✅ (2026-04-25) — `StatWatcher` 추상 + `HpThresholdWatcher` (Berserk) / `TimerRampWatcher` (Accel) / `NearbyCountWatcher` (Unity) 3종. `ChaosSkillManager` 의 `Check*` 메서드 + 캐시 필드 전부 제거 → watcher 리스트 일괄 Tick

### 추가 포트 추출 (Clean Architecture 정합성)
- **W4** ✅ — `IPlayerStatsMutator` / `ISkillRegistry` 포트 추출 (`Shared/Domain/Interfaces/`). `PlayerStats` / `SkillManager` 가 각 포트 구현. Essence/Weapon Inventory 는 Character.Adapter / Skill.Adapter 직접 참조 완전 제거
  - `ISkillRegistry.EffectSinks` (`IReadOnlyList<IRuntimeEffectSink>`) + `OnSinkAdded` 이벤트
  - `SkillManager.cachedSinks` + `RefreshSinkCache()` (Acquire/Remove/Evolution 3 경로)
  - `PlayerEssenceInventory`: `SkillManager` → `ISkillRegistry`. `HandleSkillAdded(Skill)` → `HandleSinkAdded(IRuntimeEffectSink)`
  - `PlayerWeaponInventory`: `PlayerStats` → `IPlayerStatsMutator`

### 공통 의사결정 기록 (드랍 시스템 SSOT)
1. **드랍 오브젝트 네트워크 모델**: 로컬 생성(모든 클라 동일 좌표) + 호스트 권위 픽업. `PhotonNetwork.Instantiate` 사용 안 함
2. **Source 접두사 컨벤션** (`PlayerStats.modifiers`): `passive_ / evolution_ / chaos_ / essence_ / essence_combo_ / weapon_ / stat_ / buff_`
3. **`SkillTriggerSystem.AddRuntimeEffect` source 컨벤션**: `essence_{id} / weapon_{id} / chaos_{id}`
4. **SO 생성 메뉴 루트**: `SwDreams/Data/{Essence|Weapon|Quest|StatBoost|EnemyDropTable}`
5. **배치 이벤트 코드**: `EnemyDeathBatch=11` / `EnemyRemoveBatch=12` / `DropSpawnBatch=13` (이후 `LoadSceneEvent=15` / `LobbyRefreshEvent=16` 추가)
6. **풀링**: 모든 픽업 프리팹 `PoolManager.Prewarm` 게임 시작 시 warm-up

### 드랍 시스템 SO 입력값 SSOT (2026-04-24 동기화)

| 항목 | 값 | 출처 |
|---|---|---|
| 일반 적 자석 드랍 | 1% | `EnemyDropTable.magnetChance = 0.01` |
| 일반 적 물약 드랍 | 1% | `EnemyDropTable.potionChance = 0.01` |
| 일반 적 무기 드랍 | 100% | `EnemyDropTable.weaponChance = 1` |
| 일반 적 정수 드랍 | 0% | `EnemyDropTable.essenceChance = 0` |
| 엘리트 정수 드랍 | 100% | `EliteDropTable.essenceChance = 1` |
| 엘리트 무기 드랍 | 0.01% | `EliteDropTable.weaponChance = 0.0001` |
| 무기 등급 가중치 | 60/25/12/3 | `weaponRarityWeights` |
| 경험치 오브 동시 상한 | 200 | `GameplayConfig.maxActiveExpOrbs` |
| 자석 범위 / 속도 | 0.7 / 2 | `GameplayConfig.magnetRange` / `magnetSpeed` |
| 공용 4등급 가중치 | 60/25/12/3 | `GameplayConfig.defaultRarityWeights` |
