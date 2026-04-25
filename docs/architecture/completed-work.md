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
- N3 빨간색 고착 수정 (2026-04-25) — PlayerVisual.originColor 정적 캡처 + StopCoroutine 직후 즉시 복원
- N2 피격 이펙트 (0,0) 잔상 (2026-04-25) — 실제 원인은 사용자가 prefab 교체 시 root GameObject 에 HitEffect 스크립트 미부착. 코드 측은 견고화: `GetComponentsInChildren<ParticleSystem>(true)` 로 부모-자식 모든 ps 캐싱 + 명시 Play(true) + AudioSource 캐싱 + SetActive 를 Play(position) 안으로 이동(소리 위치 정확).
- Phase 6 퀘스트 MVP 인프라 (2026-04-25) — QuestType/State/Data SO + QuestZone 상태 머신 + RewardDispatcher + LevelUpManager.RequestQuestReward
- Phase 6 격리 몹 + 킬 카운트 연결 (2026-04-25) — `SpawnManager.SpawnQuestBarriers`/`DespawnEnemies` + `QuestZone.activeZones` 레지스트리 + `OnEnemyDied` 통지 경로 + F2 보상 큐잉

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
