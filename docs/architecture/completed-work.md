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
- **Phase 8-2 마이크 SDK 1차 통합** (2026-04-27) — Photon Voice 2 임포트(`Assets/FromStore/Photon/PhotonVoice/`) + `PunVoiceClient` 셋업(MenuScene NetworkManager) + `PhotonAppSettings.AppIdVoice`. `PlayerStub.prefab` 에 `PhotonVoiceView` + `Recorder` (`TransmitEnabled=false`, `MicrophoneType=Photon`) + `Speaker` + 신규 `VoiceController` 부착. `Features/Voice/Adapter/{VoiceController, MicTestService}` + `Features/Voice/Presentation/MicToggleButton` 신규. VoiceController = 자기 PhotonView (IsMine) 만 제어 + 새 Input System (`Keyboard.current[Key.V].isPressed`) PTT + SettingsManager.OnMicChanged 구독으로 모드/감도 자동 반영 + 정적 `LocalInstance` 로 UI 인스펙터 드래그 회피. MicToggleButton = InGameHUD 마이크 ON/OFF 브리지 (`OnLocalMuteChanged` 이벤트로 sprite/색 자동 토글). MicTestService = ParrelSync/룸 가입 의존성 0 인 단일 인스턴스 마이크 자체 테스트 (UnityEngine.Microphone 직접 캡처 → AudioSource.loop 즉시 재생, Photon Recorder.DebugEchoMode 가 voice 룸 서버 경유라는 한계 회피). SettingsPanelUI 에 "내 마이크 테스트" 토글 통합 + 패널 Hide() 시 강제 종료. **함정 회피:** SettingsManager.SetMicSensitivity 에 `Mathf.Clamp(v, 0.01f, 1f)` floor 클램프 — `Recorder.VoiceDetectionThreshold = 0` 이 OpenMic 모드에서 voice publish 자체를 silent frame 으로 차단. 후행: 빌드 환경 송수신 검증 + R3 마이크 필터 드랍 (별건). 상세 [voice-chat.md](../systems/voice-chat.md).
- **N18 패시브 계수 (statOverrides multiplier) + 디테일 fix 묶음** (2026-04-29) — 스킬별 패시브 영향력 차등. `SkillData.applicableStats` (List<StatType>) → `statOverrides` (List<StatModifierEntry { type, multiplier }>) + `FormerlySerializedAs` 호환. **예외 override 모델** — 빈 리스트 = 모든 스탯 100% (default 1.0), 명시된 항목만 그 multiplier 로 차등 (0=미적용 / 0.5=50% / 1.5=150% 강화). multiplier 는 보너스 부분에만 곱(스킬 base + 캐릭터 base 보호). `SkillData.GetStatMultiplier` 신규 + `IsStatApplicable` 호환 유지. `PlayerStats.GetFiltered*` 7개 + `ApplyAttackTo` 모두 multiplier 적용 (`base + (final-base) × mult`). `SkillExecutor.BuildContext` CritChance/CritDamage 도 multiplier 적용. dead API `GetFilteredStat` 제거. **함께 묶인 디테일 fix 3종:** (1) `ChaosSkillManager.Start` 에서 `explosionEffectPrefab` prewarm 16개 (4인 동시 폭발 spike 방지, 자기 PV.IsMine 1회). (2) `PlayerVisual` 긴 i-frame 첫 0.1초 빨간 플래시 + 이후 alpha 깜빡임 (피격 인지 + 무적 표현 분리). (3) `SkillData.phase1RotationCount` (float, default 1.0, Min 0.1) 신규 + `OrbitalSpawner` 전달 + `OrbitalObject.phase1TargetAngle = 360 × count` + `SkillDataEditor` TwoPhase 섹션 노출 — 진화 장검/번개의 회전 횟수 인스펙터 조정 가능. audit: architecture-guardian PASS. U7 메모: 스킬 카드에 statOverrides 적용 후 실효 표시 (Vampire Survivors 패턴, 후행 옵션). 모든 SO 가 빈 리스트라 마이그레이션 부담 0. ParrelSync 검증 통과.
- **N15 + N17 호스트 권위 스킬 발사 RPC 인프라** (2026-04-29) — 번개 위치 클라 desync (Random.insideUnitCircle 자체 결정) + 토네이도 방향 핑 어긋남 동시 해소. **Client-decided + Host-trusted 패턴.** `PlayerStub.RPC_RequestSkillSpawn` (자기≠호스트 → MasterClient 송신) / `PlayerStub.RPC_BroadcastSkillSpawn` (자기=호스트 → Others 송신) + `ISkillSpawner.TryGenerateSpawnPos` (5종 Spawner 구현, AreaSpawner 만 Random 결정 후 true 반환) + `SpawnContext.spawnPosOverride / hasSpawnPosOverride` + `SkillExecutor.BroadcastNetworkSpawn / BeginFromNetwork` + `SkillManager.HandleNetworkSkillSpawn / FindEquippedSkillById` + `Skill.FireFromNetwork`. 자기 클라가 spawnPos / baseDirection 결정 → RPC 인자로 모든 클라에 전파 → 동일 위치/방향 보장 + Client Prediction 으로 자기 핑 0 체감. **부산물 fix:** `Skill.Update` 에 `cachedPV.IsMine` 가드 — 기존 모든 클라 자체 시뮬레이션이 신규 RPC 와 중복돼 이중 spawn 버그 → 자기 PlayerStub 만 자체 cooldown+Fire. **Phase 1 범위 한계:** TwoPhase 미지원 (장검 진화 — Single 강제, 자기 클라 자체 시뮬레이션 유지, N19 메모). 데미지 권위는 기존 `Projectile / AreaZone` 의 `IsMasterClient` 가드 그대로 유지 (이중 데미지 0). audit: architecture-guardian PASS, photon-sync-auditor Critical 1 (TwoPhase) 처리 + 1 (데미지 누수) 무리한 우려로 통과. ParrelSync 2 인스턴스 검증 통과.
- **R3 마이크 필터 드랍 픽업** (2026-04-29) — 카오스 재미 픽업. 5종 필터(LowPass / Distortion / Echo / PitchHelium / PitchDemon) — 앞 3종 `AudioLowPass/Distortion/EchoFilter` 동적 add, 뒤 2종 `AudioSource.pitch` 변경. `Features/Voice/Domain/MicFilterType` (5종 enum) + `Features/Voice/Adapter/Data/{MicFilterData, MicFilterDatabase}` SO + `Adapter/{MicFilterController, MicFilterPickup}` + `Pickup/Domain/PickupType.MicFilter` + `Shared/Data/EnemyDropTable.micFilterChance` + `Shared/Managers/GameManager.MicFilterDB` + `DropSpawner.RaiseMicFilterApplied/RPC_ApplyMicFilter`. 호스트 권위(랜덤 ActorNumber + 인덱스 롤) → RPC_ApplyMicFilter(All) → 모든 클라가 ActorNumber 매칭 PlayerStub 의 MicFilterController.ApplyFilter. 클라 자체 만료 코루틴 (호스트 마이그레이션 영향 0). 새 필터 도착 시 기존 즉시 교체(자동 시간 연장). **시각 표시 없음** — 본인은 자기 음성 못 들음(Photon Voice self-mute) → 다른 사람 반응에서 깨닫는 게 카오스 본질. **NullRef fix:** Speaker.Awake 가 AudioSource 동적 생성하는 케이스 + 자식 GO 케이스 대응 — `EnsureAudioSrc()` lazy lookup + `audioSrc.gameObject` 를 AudioFilter 호스트로 사용. ParrelSync 2 인스턴스 검증 통과(LowPass + PitchHelium). 빌드 송수신 종합 검증은 Phase 8-2 후행과 별건. 디버그 ContextMenu 5종 (`#if UNITY_EDITOR` 가드, 빌드 무영향) 보존. 상세 [items.md § 8-1](../game-design/items.md).
- **Phase 8-5 A — Localization 코어 + Sheet 임포터** (2026-04-28) — 자체 다국어 시스템 1차. `Shared/Localization/Domain/{ILocalizationService, LocalizationKey}` (Locale.cs 는 R12 선행) + `Adapter/{LocalizationTable, LocaleFontMap, LocalizationManager, LocalizationBootstrap, LocalizedText}` + `Editor/LocalizationSheetImporter`. 동기 API (Survivors-like 매 프레임 깜빡임 방지). Fallback 우선순위 Locale → EN → KO → key. CSV public export URL 임포트 (Service Account JSON 불필요) + cache-buster `&_cb={timestamp}` + `CacheControl: no-cache` (CDN 옛값 함정 회피). `LocalizationBootstrap` 정적 `OnInitialized` 이벤트 + `SubscribeWhenReady` 헬퍼로 Awake race 차단(다른 GameObject 의 LocalizedText.OnEnable 이 먼저 실행돼도 정상 갱신). RFC 4180 호환 CSV 파서 (따옴표 안 콤마/줄바꿈/이스케이프). `SettingsManager.SetLocale` → `LocalizationBootstrap.Service.SetLocale` 1줄 결선으로 R12 드롭다운 즉시 반영. KO/EN 전환 검증 통과. 후행: Phase B (UI 점진 키 매핑) / Phase C (스킬 SO 통합) / Phase D (NotoSans 폰트 + 검수). 상세 [localization.md](../systems/localization.md).
- **R14 인게임/대기실 보이스 HUD** (2026-05-01, 커밋 `0b1b23ff1`) — 좌측 별도 `VoicePanel` (대기실 + 인게임 동일 prefab, `VoicePanelController` 가 PhotonNetwork.PlayerList + 콜백 기반 행 동적 추가/제거 — 씬 무관 동작). 호버 알파(idle 흐림 → hover 또렷): root `CanvasGroup` + `VoicePanelHover` 가 alpha lerp / 자식 슬라이더의 핸들·트랙 Image 알파는 `SliderHoverFade` 가 별도 lerp(`panelHover` 인스펙터 슬롯 노출 — GetComponentInParent 함정 회피). `PerUserVoiceSettings` (`Dictionary<int actor, float volume>` 싱글턴, ActorNumber 키, 룸 나가면 `OnLeftRoom→Clear`, RuntimeInit AfterSceneLoad 자동 부트로 인스펙터 부착 누락 해결, DontDestroyOnLoad 로 대기실↔인게임 씬 횡단). `PerUserVoiceApplier` (Speaker 측, IsMine 가드, Owner null 대기 코루틴 OnDisable 정리). **`AudioGainBoost` (`OnAudioFilterRead` sample-level gain 곱 — AudioSource.volume 0~1 cap 우회로 0~2 boost 지원, 마이크 작은 유저 보정용)**. 마이크 민감도 3곳(R12 설정 / 대기실 VoicePanel / 인게임 VoicePanel) 양방향 바인딩 (`MicSensitivitySlider`). `MicActivityIndicator` (Discord 패턴 — `Speaker.IsPlaying` 기반 자동 색 토글, 동기화 X — voice frame 수신으로 자동 결정). **`LobbyPlayer.prefab` Phase 8-2 보이스 셋업 보완** — `PhotonVoiceView`+`Recorder`+`Speaker`+AudioSource(Voice mixer)+`VoiceController`+`AudioGainBoost`+`PerUserVoiceApplier` (PlayerStub 패턴 동일). 대기실에서도 PTT 음성 송수신 활성화. 핵심 결정: master(AudioMixer "VoiceGain" dB) × perUser(AudioGainBoost) 2단 적용으로 이중 곱셈 회피. 신규 9 .cs + 2 prefab(VoicePanel/TeammateVoiceRow). unity-reviewer Critical 2건 fix(Applier 코루틴 정리 / Controller Start+OnEnable 중복 빌드). 후속 별건: LobbyPlayerEntry.prefab 잔존 빈 VoiceSlider 자식 정리(무해) / U4 ESC 메뉴 팀원 보이스 섹션(PerUserVoiceSettings 싱글턴 공유로 자동 정합). 상세 [voice-chat.md](../systems/voice-chat.md).
- **Phase 7-5 인-런 통계 시스템 (B-1a 분산 추적)** (2026-05-06, 커밋 `203b6bb93`) — [`run-statistics.md`](../systems/run-statistics.md) 구현. 자기 발사 데미지 / 자기 막타 킬 / 보스 D13 모든 파티원 카운트 / 자기 받은 데미지 / 자기 사망 통계를 자기 PC 에 누적 + 결과 화면 시각화 (플레이어 카드 + 스킬별 막대 차트 + 팀 합계). **B-1a 흐름** = 가해 데미지는 자기 발사 시점 자기 PC 누적 (호스트 적용 결과 무관, 마이그레이션 안전, 작은 오차 < 1% 감수), 자기 막타 킬은 사망 RPC 페이로드 확장 (Enemy: `SpawnManager.deathQueue` stride 5→6 `+killerSkillId` / Boss: 신규 `RPC_BossDied(int bossId)` RpcTarget.All / PlayerHealth: `RPC_TakeDamage(int, int attackerEnemyId)`). **공유 인프라:** `TriggerContext` / `SpawnContext` 에 `attackerActorNumber` + `sourceSkillId` 필드, 4개 인스턴스(Projectile/AreaZone/OrbitalObject/PlacedTurret) `SetSourceSkillId` + OnReturnToPool 리셋, 7개 핸들러(Deal/DamageNearby/Explode/Chain/ApplyDoT/SpawnProjectile/Execute) Enemy.LastDamager* 채우기. `Enemy.LastDamagerSkillId` / `BossData.bossId` / `PlayerHealth.LastDamagerEnemyId` 필드 신규. EnemyAttack/EnemyProjectile/TelegraphZone 페이로드 `+sourceEnemyId`, `PlayerStub.TakeDamageFromEnemy` 신규 진입점. **신규 Stats Feature** — Domain (`LocalRunStats`/`SkillRunStats` VO) + Adapter (`LocalStatsRecorder` MonoBehaviour 싱글톤, GameScene 마다 새 인스턴스, GameManager.Awake `GetOrCreate()` timing 안전). `PlayerBuildData` 7개 통계 필드 + `ResultManager` float packing 직렬화 (`BitConverter.SingleToInt32Bits`). spec 정정: run-statistics.md §4 B-1a 흐름 + meta-unlock.md §11 §15 D13 보스 공유 카운트. audit: photon-sync-auditor Critical 0 / unity-reviewer Critical 1 (풀링 sourceSkillId 리셋) 수정. 검증: 싱글 / 멀티 분산 추적 / 마이그레이션 보존 / 보스 D13 / 결과 UI 통과. sourceSkillId 인프라 도입으로 **meta-unlock D1 ("특정 스킬로 처치") 부활 거의 무비용** 상태.
- **회귀 fix 두 건 (B-1a 검증 중 발견)** (2026-05-06, 커밋 `c53f48401`) — (1) 보스 진입 시 `SpawnManager.StopSpawning()` 이 호스트 isReady=false 만들고 BossFight 상태에서 자동 복구 안 됨 → 호스트 측 `Skill.Update` IsReady 가드 영구 차단. `SpawnManager.MarkReady()` 신규 + `BossSpawner.SpawnBoss/SpawnEmergencyBoss` 끝에 호출. (2) 사망 중 신규 스킬 획득 시 `Skill.Activate` 가 isActive=true 강제 셋팅 → 죽은 상태인데 발사. `SkillManager.isPaused` 플래그 + Pause/Resume 동기화 + `AcquireSkill`/`TryExecuteEvolution` 신규 스킬 Activate 후 isPaused 면 즉시 Deactivate.
- **R13 적 스탯 시간/인원 스케일링** (2026-05-01, 커밋 `1e4c373fb`) — 후반 단조로움 해소(데미지/이속도 시간 곡선). `DifficultyData` 에 `damageStart/End/Curve`(1.0→3.0) + `moveSpeedStart/End/Curve`(1.0→1.6) + `PlayerScaling.damageMultiplier/moveSpeedMultiplier` (HP 비율 절반~1/3, 4인 1.3/1.1). `DifficultyManager.GetDamageMultiplier(t,N)` / `GetMoveSpeedMultiplier(t,N)` 신규. `EnemyData.moveSpeedScaleSensitivity` (Range 0~1, default 0.5) — 타입별 정체성 보존(Tank=0/Chaser=0.5/Swarm=0.7/Runner=1.0/Ranged=0.3). **계산은 스폰 1회, 런타임 재계산 X.** `Enemy.Initialize` 시그니처에 `damageMul/speedMul` 추가, `finalContactDamage/finalAttackDamage/finalMoveSpeed` 캐싱 + getter 소스만 base→final 로 교체(호출부 0줄 변경). `Enemy.DamageMul/SpeedMul` 노출 — 중도참가 RPC 재전송용. SpawnManager 의 SpawnEnemy/Swarm/Ranged/Elite 4종 RPC 페이로드에 float 2개 추가, 호스트 권위 산출 후 전파(클라 자체 평가 X — 발산 회피). 격리 몹 RPC_SpawnQuestBarrier 는 시간 무관 default 1f/1f. **방어력/속성 저항 의도적 보류** — Survivors-like 다중 약공격 빌드 보호. photon-sync-auditor 통과(Critical 0). 부채 W1: 중도참가 시 hpMul=1f 유지(클라 HP 판정 안 하니 무해, 향후 클라 HP 바 도입 시 회귀 가능 — 별건). 사용자 후속(별건): EnemyData 12개 .asset 의 sensitivity 인스펙터 채우기(현재 default 0.5 단일이라 타입 정체성 미반영). 상세 [enemy-stat-scaling.md](../systems/enemy-stat-scaling.md).
- B4 스웜 충돌 회피 (2026-05-01) — `EnemyData.resolveOverlap=false` 플래그 실효화. `EnemyMovement.ResolveEnemyOverlap` 진입부 + for-loop 다른 적 분기 양방향 가드 (자기 보정 X + 상대가 swarm 이면 continue). Layer/Physics2D Matrix 셋업 불필요.
- B3 팀원 HUD disconnect entry 자동 정리 — R11 World Indicator 부산물 fix (2026-04-26). `InGameHUD.UpdateTeammates` 가 매 호출 stale 비교 → Destroy + Remove (poll 기반, OnPlayerLeftRoom 콜백 불필요).
- **R12 설정 패널 (Video/Audio/Language)** (2026-04-27) — 5 Phase 완료. AudioMixer 라우팅(`MasterMixer` + 4 그룹 + Exposed `MasterVol/BGMVol/SFXVol/VoiceGain`) + dB 변환 `Log10*20`. `Shared/Localization/Domain/Locale.cs` (KO/EN/JA/ZH-CN) Phase 8-5 A 선행 도입. `Features/UI/Adapter/Settings/{SettingsModel, SettingsManager}` (VO + DontDestroyOnLoad 싱글턴 + PlayerPrefs Load/Save + Setter 즉시 반영 + `Flush()`). `Features/UI/Presentation/SettingsPanelUI` (Slider 5 / Toggle 1 / Dropdown 3 / Button 1, `SetValueWithoutNotify` 콜백 가드). `AudioManager` SetFloat → Start 1프레임 지연 (Mixer 함정 회피) + `OnValidate` 라이브 적용. `TitlePanelController.OnClickSettings` 제거 — `settingsButton.onClick` 인스펙터에서 `SettingsPanelUI.Show` 직접 연결 (§2 Adapter→Presentation 의존 회피, Unity-native first). PlayerPrefs 키 `settings.{video,audio,locale}.*`. 후속(별건/후행): U4 ESC 인게임 메뉴 진입, Voice/Mic 실효 = 8-2, Locale 실효 = 8-5 A, 클라우드 마이그레이션 = 8-1.

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
- **U4 ESC 인게임 메뉴** (2026-05-01) — `Features/UI/Adapter/InGameMenu/{InGameMenuController, ConfirmDialog}` 신규 + `InGameMenuCanvas.prefab` (sortOrder=100). 4 메뉴 항목(Resume/설정/룸 나가기/게임 종료). 솔로(PlayerCount<=1) 한정 `GameState.Paused` 진정 정지 + `GameManager.IsMenuPaused` 보조 플래그(LevelUpManager 자기참조 회피용 외부 정지원 가드 — 솔로+레벨업 ESC 도 타이머 정지). 멀티는 로컬 UI 토글만(게임 흐름 유지). 호출 가능 상태 = `Playing/BossFight/Paused`(레벨업 중) 한정 + 이미 열려있으면 GameOver/GameClear 에서도 닫기 허용(결과창 접근). 룸 나가기 = `MenuSceneManager.ReturnToRoomList=true` + `NetworkManager.LeaveRoom` (`ResultManager.OnExit` 패턴 재사용, 호스트는 마이그레이션). 게임 종료 = `Application.Quit` + 에디터 분기. 설정 패널은 사용자 prefab(SettingsPanel) 인스펙터 연결 — DontDestroyOnLoad 의 `SettingsManager.Instance` 공유로 GameScene 동작. 임시 `ConfirmDialog` (Frame_PopUp 미작성 stand-in, 작성 시 일괄 이관). 자식 Canvas + Override Sorting + GraphicRaycaster 패턴(메인 Canvas 안 분리). unity-reviewer H/M 반영(OnDestroy 리스너+LeftRoom 구독 해제, EventSystem.SetSelectedGameObject(null), 씬 전환 1프레임 잔재 방지). 인게임 검증: 솔로 정지/멀티 흐름/레벨업 타이머/룸 나가기/게임 종료 모두 통과. 후속 별건: GameOver 자동 닫기(보류 — 사용자 수동 ESC 충분), R14 § 팀원 보이스 슬라이더 섹션 동반(별건). 상세 [in-game-menu.md](../systems/in-game-menu.md).

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
