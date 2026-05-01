# Implementation Roadmap

Sweepin' Dreams 의 구현 단계 로드맵. 현재 코드 진행 상태를 반영하여 **Phase별 완료/진행 표시**를 명확히 한다.

최종 업데이트: 2026-05-01 (R13 ✅ 마이그레이션 — completed-work 로 이동, Top 5 재정렬)

> 본 문서는 "**언제·무엇을·어떤 순서로 구현하는가**" 의 SSOT. 게임 설계 자체는 [../game-design/overview.md](../game-design/overview.md) 참조. 개별 스킬·적·시스템 설계는 해당 폴더 문서.
>
> 완료된 항목 ledger = [completed-work.md](completed-work.md). 알려진 버그 = [known-issues.md](known-issues.md). 본 문서는 **잔여**만 다룬다.
>
> **운영 룰 (2026-04-26 도입):** R/U/Phase 항목이 ✅ 처리되는 순간 → [completed-work.md](completed-work.md) 로 이동, 본 문서에서는 1줄 요약 + 링크만 남긴다. Phase 별 ✅ 완료 서브섹션은 묶어서 1줄 요약. 본 문서는 항상 **현재 해야 할 일** 만 보이도록 유지.

---

## 🎯 지금 추천 작업 (Top 5) — 사용자가 "다음 뭐 할까?" 물으면 이 섹션만 보고 답변

> **운영 룰:** finalize-work §2.5 가 ✅ 처리 시 큐에서 자동 제거 + 다음 후보 제안. 우선순위는 의존성·블로킹·사용자 임팩트 기준. 진행 중에 사용자 의사결정 변경되면 즉시 갱신.
>
> 마지막 갱신: **2026-05-01** (R14/R13 최상단으로 승격 — 사용자 우선순위)

| 순위 | 항목 | 근거 | 의존성/블로킹 | 예상 |
|---|---|---|---|---|
| 1 | [§ R14](#r14-인게임-마이크-민감도--유저별-보이스-볼륨-조절) **유저별 보이스 + 인게임 마이크 민감도** | 사용자 우선순위. 멀티 게임 UX 핵심 — 트롤 마이크 / 음량 차이 즉시 대응 | **[U4 ESC 메뉴](#u4-esc-인게임-일시정지-메뉴) 동반 작업 권장** ([spec](../systems/in-game-menu.md)). Phase 8-2 ✅ 해제됨 | 1.5~2일 (U4 포함) |
| 2 | [§ R10](#r10-클라이언트-적-위치-수렴--convergence-damping) **클라 적 위치 수렴** | 사용자 직접 보고한 떨림 이슈. 명세 + 정책 확정 완료 (network-sync.md § 8.1) | 없음 (선행 가능) | 0.5~1일 |
| 3 | [§ R6](#r6-회오리끌어당김-pullradius-패시브-반응) **회오리 pullRadius 패시브 반응** | 작은 코드 단위 (1~2시간), 단독 가능. SkillRange 패시브 영향 받도록 | 없음 | 1~2시간 |
| 4 | [§ Phase 5-2/5-3](#phase-5--나머지-스킬--혼돈-스킬--진행-중-현재-브랜치-hyeon-woo) **신규 액티브/혼돈 스킬 #11~24, 13종** | 현재 브랜치 본업. 컨텐츠 양 절대량 큼 | Phase 5-1~5-5 의 SO 패턴 정착 완료 | 항목당 0.5~1일 |
| 5 | [§ R4](#r4-캐릭터적-아웃라인--스프라이트-애니메이션-호환성--퍼포먼스-검토) **아웃라인 검증 + 퍼포먼스** | 동시 90마리 적 + 아웃라인 셰이더 부하 측정 + 스프라이트 애니메이션 호환성 | 없음 | 0.5일 |

**선행 가능 그룹** (다른 작업 안 끝나도 시작 OK): R10, R6, Phase 5-2/5-3 모두 독립. R14 는 U4 ESC 메뉴 작업과 묶음.
**병렬 가능 그룹**: R10 + R6 + Phase 5 가 서로 다른 파일 영역이라 동시 진행 OK. R14+U4 도 별도 영역이라 위 그룹과 병렬 가능.
**다음 진입 후보** (Top 5 다 끝났을 때): Phase 8-5 B (Localization UI 키 매핑), R5 (혼돈 글로벌 설정 이전), Phase 8-1 A (Platform 추상화), Phase 8-5 C (스킬 SO 통합).

---

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
| 8. 출시 인프라 (Voice / Platform SDK / Localization) | 🟡 진행 중 (8-2 ✅, 8-5 A ✅) | 8-2 Voice 1차 ✅ (2026-04-27). 8-5 A 코어+임포터 ✅ (2026-04-28). 8-1/8-3/8-4/8-5 B~D ⬜ |
| 드랍/장비/퀘스트 (Phase 0~7) | 🟡 코드 거의 완료 | 코드 ledger = [completed-work.md § 드랍 시스템 구현](completed-work.md). HUD/유저 Unity 배선/Quest 핸들러 잔여 — 본 문서 § DQ |
| 신규 잔여 (R/U) | 🟡 미시작 | 방어력/자연회복/i-frame/메뉴 등 — 본 문서 § R, § U 참조 |

최근 관련 커밋:
- `1f225a555` fix: 장검 진화 Phase2 발사 동작 복구 → Phase 5 영역
- `84dfb3b3f` feat: FrameToast → Phase 4 UI 영역
- `6d6112763` feat: Frame_PopUp → Phase 4 UI 영역
- `b40a9e5d0` refactor: 멀티플레이어 동기화 대규모 리팩토링 — Dead Reckoning + C안 데미지 요청
- `deb23b669`~`3422f8def` refactor: 스킬 시스템 2차 리팩터링

---

## Phase 0 — 프로젝트 셋업 ✅

Unity 2D + PUN2 + ParrelSync + 폴더 구조 + ScriptableObject 템플릿 + 2씬 셋업 모두 완료. 상세 [completed-work.md § Phase 0](completed-work.md).

---

## Phase 1 — 네트워크 + 메뉴 플로우 ✅

NetworkManager 싱글톤 / MenuScene 패널 전환 / 대기실(LobbyPlayer + 캐릭터 선택 + Ready + 카운트다운 + Kick) / GameScene 기본 + Player / 결과→대기실 복귀 / 에러 처리 모두 완료. 상세 [completed-work.md § Phase 1](completed-work.md).

---

## Phase 2 — 기본 전투 🟡 (스폰 위치 환원만 잔여)

완료: Enemy 기본 클래스 / 스킬 시스템 기초(Spawner 5종, 표창, 쿨다운) / DamageCalculator / 경험치 시스템(ExperienceOrb, 자석, 팀 공유). 상세 [completed-work.md § Phase 2](completed-work.md).

### 2-2. 스폰 위치 명세 환원 🟡
- [ ] **스폰 위치: 맵 경계 랜덤** — 현재 "캐릭터 기준 일정 반경" 으로 구현됨. 명세는 "맵 경계 기준 랜덤"이므로 미완료 환원 (2026-04-25)

---

## Phase 3 — 적 AI + 스폰 고도화 🟡 진행 중

완료: 빠른형/무리형 겹침/원거리 4변형/엘리트(Essence 드랍 훅) + 난이도 곡선(스폰 테이블, 체력 배율, 등장 비율) + 풀링(투사체/이펙트) + 인원수 스케일링 구조. 상세 [completed-work.md § Phase 3](completed-work.md).

### 3-1. 적 타입 잔여 🟡
- [ ] 둔한형 넉백 저항 튜닝
- [ ] 무리형 그룹 스폰 완성

### 3-3. 인원수별 배율 튜닝 (밸런싱) 🟡
- [ ] 2~4인 배율 실측 튜닝

### 3-4. 화면 밖 적 간소화 AI 🟡
- [ ] **화면 밖 적 간소화 AI** — 90마리 동시 운영 시 성능 마진. ↓ 설계 메모 참조

#### 화면 밖 적 간소화 AI — 설계 메모 (착수 전 반드시 읽기)

**전제 — 현재 적 동기화는 Dead Reckoning 방식**: [EnemyMovement.cs:16-21, 158-167](../../Assets/Scripts/Features/Enemy/Adapter/EnemyMovement.cs#L16). 호스트만 적 AI를 돌리는 게 아니라 **호스트 + 클라가 각자 같은 시뮬레이션을 돌린다**. 양쪽이 같은 입력(플레이어 위치)으로 같은 출력(적 위치)을 만들어내는 구조. 호스트는 가끔 자기 측 위치를 RPC로 보내서 미세 오차만 보정.

**왜 "호스트 카메라 시야 기준 간소화"가 깨지나**:
- 호스트만 자기 화면 밖 적의 path 갱신을 늦추면 → 호스트 측에선 그 적이 거의 멈춤
- 클라는 그 규칙 모름 → 정상적으로 매 프레임 path 갱신 → 적이 잘 쫓아옴
- 양쪽 시뮬레이션 결과가 **달라짐** → 호스트가 보내는 위치로 강제 보정 발동 → 적이 클라 화면에서 **워프**
- 카메라 위치/줌은 측마다 다르고 동기화 안 함 → "내 화면" 기준은 절대 사용 불가

**올바른 접근 — 동기화된 정보로만 결정**:
- 결정 입력 = "**적과 가장 가까운 (살아있는) 플레이어와의 거리**"
- 모든 플레이어 위치는 PhotonTransformView로 동기화됨 → 모든 측이 같은 거리값을 계산 → 같은 간소화 결정
- 추적 대상 플레이어 거리만 보는 변형도 가능하지만, 다른 플레이어 시야엔 보일 수 있어 어색해질 수 있음. **"가장 가까운 임의 플레이어 거리"가 가장 안전**

**"간소화"의 의미 — path 재계산 빈도만 줄이기**:
- ❌ 정지 / 단순 직진: 양쪽 동시에 멈춰도 시각적으로 어색. 다시 화면 안으로 들어오면 갑자기 방향 휙 바뀜
- ✅ **이동 자체는 매 프레임 유지**, `FindClosestPlayer()` 재계산만 0.3초마다. 적이 마지막으로 잡은 target 방향으로 계속 이동

**구현 스케치**:
```csharp
// EnemyMovement.cs Update 안
private const float SIMPLIFY_DISTANCE = 25f;        // 화면 대각선 + 안전 margin
private const float SIMPLIFY_HYSTERESIS = 3f;       // 22m에서 정상 복귀 (flap 방지)
private const float SIMPLIFIED_PATH_INTERVAL = 0.3f;

private bool isSimplified;
private float pathUpdateTimer;
private Transform cachedTarget;

// 1. 간소화 여부 결정 (모든 측 동일 입력 → 동일 결정)
float minDist = ComputeMinDistanceToAnyAlivePlayer();
if (isSimplified) {
    if (minDist < SIMPLIFY_DISTANCE - SIMPLIFY_HYSTERESIS) isSimplified = false;
} else {
    if (minDist > SIMPLIFY_DISTANCE) isSimplified = true;
}

// 2. path 갱신 — 간소화면 0.3초마다, 정상이면 매 프레임
pathUpdateTimer -= Time.deltaTime;
if (pathUpdateTimer <= 0f || cachedTarget == null) {
    cachedTarget = FindClosestPlayer();
    pathUpdateTimer = isSimplified ? SIMPLIFIED_PATH_INTERVAL : 0f;
}

// 3. 이동은 항상 매 프레임 (정지/직진 X)
movementStrategy.UpdateMovement(transform, cachedTarget, moveSpeed);
```

**추가 절감 포인트**:
- **`ResolveEnemyOverlap()`** ([EnemyMovement.cs:332-384](../../Assets/Scripts/Features/Enemy/Adapter/EnemyMovement.cs#L332)) — 간소화 모드에선 LateUpdate 진입부에서 즉시 return. `Physics2D.Overlap` 호출 자체가 무거워 가장 큰 절감 포인트. 화면 밖 적이 서로 겹쳐있어도 안 보이니 무관
- **`TickSlowStack()`** — 화면 밖 적은 슬로우 만료 갱신만 하면 됨 (어차피 안 보임). 단 화면 안으로 들어왔을 때 효과 정확해야 하므로 dt 누적은 유지
- **Animator disable** — 화면 밖 적은 SpriteRenderer/Animator 비활성. 풀 반환 시 다시 enable 필수. R4 (스프라이트 애니메이션 + outline) 작업 후 평가하는 게 안전
- **`FindClosestPlayer` 캐싱** — 현재 한 프레임 캐시. 간소화 적은 0.3초 캐시로 더 절감 가능

**검증 포인트 — 간소화 도입 후 모니터링 필요**:
- [EnemyMovement.cs:319](../../Assets/Scripts/Features/Enemy/Adapter/EnemyMovement.cs#L319) `SnapThreshold = 3m` 즉시 스냅이 자주 발동하면 → 양측 시뮬레이션이 결정적이지 않다는 신호. path interval을 더 짧게 (0.3 → 0.15) 하거나 결정 입력 재검토
- 클라 화면에서 화면 밖 → 화면 안 진입 시점에 적이 잘 따라오는지 (워프/멈춤/이상 방향 없는지)
- Profiler에서 EnemyMovement.Update + LateUpdate 비용이 실제로 떨어지는지 (간소화 적이 많을 때 80%+ 절감 기대)

**연관 항목**:
- 본 작업 = Phase 3-4 잔여
- R4 (스프라이트 애니메이션 호환성·퍼포먼스) 와 함께 처리하면 Animator disable까지 한 번에 평가 가능
- B5 (토네이도 발사 방향 호스트/클라 차이) 와는 무관 (Dead Reckoning 미적용 영역)

**한 줄 요약**: 간소화 결정 입력은 **반드시 동기화된 데이터(가장 가까운 플레이어 거리)** 로, 간소화 효과는 **path 갱신 빈도만 줄이고 이동은 매 프레임 유지**.

상세 스폰 규칙은 추후 [../systems/spawn-rules.md](../systems/spawn-rules.md) 작성 시 여기에 링크.

---

## Phase 4 — 레벨업 시스템 ✅ (2026-04-25 검증)

레벨업 UI(`FrameToast`/`Frame_PopUp` + `LevelUpPanel` + `SkillCardUI`) / 선택지 RPC 전파 / 타임아웃 자동 선택 / SkillManager(6슬롯/풀시 레벨업/만렙시 StatBoost 전환) / 진화 시스템(EvolutionData + 2슬롯→1슬롯 + 장검 Phase2) / 패시브 적용(`applicableStats` 필터) 모두 동작. 상세 [completed-work.md § Phase 4](completed-work.md). 상세 설계 [../systems/ui-frame.md](../systems/ui-frame.md).

---

## Phase 5 — 나머지 스킬 + 혼돈 스킬 🟡 진행 중 (**현재 브랜치 `Hyeon-Woo`**)

완료: Spawner 5종(Area/Orbital/Placed/Debuff/Projectile) / 액티브 #1~10(매직미사일·번개·부메랑·회오리·각 레벨 스케일링) / 진화 10종 부분~완성 / 혼돈 6종(유리대포/연쇄폭발/폭주/가속/단결/도박꾼) + 선택 UI / Trajectory enum 7종 + 서브클래스 6개 통합 삭제 / `applicableStats` 필터 8종 / `SpawnProjectileHandler` 서브 프리팹 SO화. 상세 [completed-work.md § Phase 5](completed-work.md).

### 5-2. 나머지 액티브 스킬 🟡
- [ ] 스킬 #11~24 구현 (설계만 있음)

### 5-3. 혼돈 스킬 잔여 🟡
- [ ] 혼돈 스킬 나머지 13종 구현
- [ ] 런타임 효과 추가 경로 (`essence_*`, `weapon_*`, `chaos_*`) — `SkillTriggerSystem.AddRuntimeEffect()`

### 5-4. 진화 스킬 잔여 🟡
- [ ] 진화 조합 데이터 SO 완성

### 5-5. 리팩터링 잔여 🟡
- [ ] `IFireRecorder` 호출부·구현체·`RefireHandler` 작성 (메아리 #17 연동 시)
- [ ] 디버그 로그 정리 (Projectile/ExplodeHandler/ChainHandler 등)

---

## Phase 6 — 보스 + 네트워크 고급 🟡 대부분 완료

완료: Boss 3페이즈 + 등장 연출 + 체력바 / 보스 혼돈 부여(미선택 랜덤) + 6가지 변형(`BossChaosEffects`) / 사망부활(10초 + HP 50%) + 전원 사망 게임오버 / 호스트 이탈(5초 대기 + 비상 보스전) / 인게임 HUD(체력/경험치/타이머/스킬슬롯/팀원/혼돈 아이콘). 상세 [completed-work.md § Phase 6](completed-work.md).

### 6-2. 보스 혼돈 스킬 잔여 🟡
- [ ] **보스 등장 시 혼돈 스킬 UI 표시** — `BossWarningUI.Show()` 호출은 있으나 실 표시 검증 필요
- [ ] 나머지 13종 보스 변형 (Phase 5-3 19종 확장과 함께)

---

## Phase 7 — 마무리 + 밸런싱 🟡 부분 시작

완료: 결과 화면(통계/빌드/보스 혼돈) / 경험치 곡선(보스 시점 레벨 18~22) / BGM·효과음(AudioManager + AudioLibrary) / 캐릭터·적 아웃라인(R4 검토 잔여). 상세 [completed-work.md § Phase 7](completed-work.md).

### 7-2. 밸런싱 잔여 🟡
- [ ] 적 스펙 조정
- [ ] 스킬 데미지/쿨타임 조정
- [ ] 보스 난이도 조정 (혼돈 스킬별)
- [ ] 2~4인 스케일링 검증

### 7-3. 비주얼 + 사운드 잔여 🟡
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

### R1. 플레이어 방어력 적용 피격 데미지 ✅ (2026-04-25) → [completed-work.md](completed-work.md)

### R2. 체력 자연회복 패시브 ✅ (2026-04-25) → [completed-work.md](completed-work.md)

### R3. 마이크 필터 드랍 아이템 ✅ (2026-04-29) → [completed-work.md](completed-work.md)

5종 필터(LowPass/Distortion/Echo/PitchHelium/PitchDemon) + 호스트 권위 RPC + 클라 자체 만료 + 본인 포함 랜덤 1명 + 새 필터로 즉시 교체 + 시각 표시 없음(카오스 재미). ParrelSync 2 인스턴스 검증 통과. 빌드 환경 송수신 종합 검증은 Phase 8-2 후행과 별건.

### R4. 캐릭터/적 아웃라인 — 스프라이트 애니메이션 호환성 + 퍼포먼스 검토
- 현재 아웃라인은 적용됐으나, 스프라이트 애니메이션이 적용된 상태에서도 정상 반영되는지 검증 필요.
- 퍼포먼스 부하 측정 (특히 동시 90마리 적 + 아웃라인 셰이더).

### R5. 혼돈 스킬 글로벌 설정을 GameplayConfig 로 이전 적절성 검토
- 현재 연쇄폭발/단결 등 글로벌 효과는 캐릭터 프리팹의 Skills 오브젝트에 설정됨.
- 게임 설정(`GameplayConfig.asset`) 으로 이전이 적절한지 검토. **일단 기록만** (의사결정 보류).

### R6. 회오리/끌어당김 `pullRadius` 패시브 반응
- [known-issues.md B1](known-issues.md) 과 같은 코드 수정 단위. SkillRange 패시브에 영향 받도록 `pullRadius * (1 + ctx.skillRangeBonus)`.

### R7. 플레이어 무적 시간 (i-frame) ✅ (2026-04-25) → [completed-work.md](completed-work.md)

### R8. 시작 스킬: 스폰 딜레이 동안 발동 차단 ✅ (2026-04-25) → [completed-work.md](completed-work.md)

### R9. 데미지 공식 — 치명타 확률·치명타 데미지 적용 ✅ (2026-04-26) → [completed-work.md](completed-work.md)

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

### R11. 파티원 / 보스 위치 인디케이터 — World Indicator UI ✅ (2026-04-26) → [completed-work.md](completed-work.md)

랜덤 퀘스트 인디케이터까지 R11 범위에 흡수해 어댑터 3종 + Manager pending drain 패턴으로 완료. 후속:
- [ ] **Localization** (Phase 8-5 C) — Boss `DisplayName` "Boss" 및 QuestData displayName 을 키로 교체
- [ ] **인접 인디케이터 오프셋 분산** (별건) — 모서리 4명 밀집 시 색만으로 구분 어려운 케이스 발견되면

### R12. 설정 패널 — Video / Audio / Language ✅ (2026-04-27) → [completed-work.md](completed-work.md)

Phase 1 (AudioMixer) ~ Phase 5 (검증) 완료. 후속 별건: U4 ESC 인게임 메뉴 진입점. 후행: Voice/Mic 실효 = Phase 8-2, Locale 실효 = Phase 8-5 A, 클라우드 마이그레이션 = Phase 8-1.

### R13. 적 스탯 스케일링 ✅ (2026-05-01, 커밋 `1e4c373fb`) → [completed-work.md](completed-work.md)

데미지/이속 시간 곡선(1.0→3.0 / 1.0→1.6) + 타입별 sensitivity(Tank 0/Chaser 0.5/Swarm 0.7/Runner 1.0/Ranged 0.3) + PlayerScaling 확장(damageMul/moveSpeedMul). 호스트 권위 + 스폰 1회 캐싱. photon-sync-auditor 통과. 사용자 후속(별건): EnemyData 12개 .asset sensitivity 인스펙터 채우기(현재 default 0.5 단일). 상세 [enemy-stat-scaling.md](../systems/enemy-stat-scaling.md).

### R14. 인게임 마이크 민감도 + 유저별 보이스 볼륨 조절 🟡 코드 완료, 인스펙터/검증 잔여

**개요:** R12 (설정 패널) 의 글로벌 Voice/MicSens 외에 **유저별 개별 보이스 볼륨** 슬라이더 + **마이크 민감도 빠른 접근 UI (대기실/인게임 HUD)** 노출. "쟤만 시끄러워서 작게" 케이스 + 게임 중 마이크 민감도 즉시 조절.

**확정 결정사항 (2026-05-01 사용자 확정):**
- **대기실 음성 채팅 활성화** — Phase 8-2 1차 통합 시 `PlayerStub.prefab` (인게임) 만 보이스 컴포넌트 부착돼 있던 것을 본 작업에서 `LobbyPlayer.prefab` (대기실 월드 캐릭터) 도 동일 패턴으로 보완. 즉 대기실에서도 음성 송수신 + 좌측 VoicePanel 로 즉시 조절 가능
- **최종 볼륨:** `master(AudioMixer "VoiceGain" dB) × perUser(audioSource.volume)` — **AudioMixer 가 master 처리, PerUserVoiceApplier 가 perUser 처리.** 즉 Applier 에서 master 안 곱함 (이중 곱셈 회피). spec 초안의 `master_linear × perUser` 직접 곱은 AudioMixer 미사용 가정이라 정정.
- **유저별 슬라이더 노출 위치 3곳 (모두 좌측 별도 `VoicePanel` 패턴 통일):**
  - **대기실 — `VoicePanel` 좌측 배치** (대기실 패널 내부)
  - **인게임 HUD — `VoicePanel` 좌측 배치** (GameScene Canvas)
  - U4 ESC 메뉴 팀원 섹션 — **U4 의존, 본 R14 작업 범위 외**. PerUserVoiceSettings 싱글턴 공유로 자동 정합
  - 시각: 평소 흐림(alpha 0.5 + 핸들 alpha 0 + 트랙 alpha 0.4) → 호버 시 또렷(alpha 1). 인터랙션 가드 X (Slider default)
  - **`LobbyPlayerEntry` 행 슬라이더는 도입하지 않음** — 좌측 VoicePanel 로 통일 (인게임 ↔ 대기실 일관성 + 시각 단순)
- **유저별 볼륨 값은 같은 클라 안에서 룸 내내 유지** (대기실 ↔ 인게임 씬 전환 횡단). **룸 나가면 휘발**. PlayerPrefs 저장 X. 키는 **ActorNumber** (룸 내 유일). 동기화 X (클라이언트 로컬)
- **마이크 민감도 UI 3곳 (값 공유):**
  - R12 설정 패널 (기존) / 대기실 `VoicePanel` 안 / 인게임 `VoicePanel` 안
  - **3곳 모두 R12 의 `SettingsManager.MicSensitivity` 와 양방향 바인딩.** PlayerPrefs 저장은 R12 가 담당 (영구 보존)
- **슬라이더 범위:** 유저별 볼륨 **0~2** (boost 지원). 1.0 default + 1.0 노치 마커 권장. AudioSource.volume 의 0~1 cap 우회를 위해 `AudioGainBoost` (OnAudioFilterRead 후처리) 컴포넌트로 sample-level gain 곱. 1 초과는 마이크 작은 유저 보정용 boost. 2 초과 시 clipping 위험으로 차단

**선결 조건:**
- R12 ✅ (Master Voice 슬라이더 + AudioMixer + MicSensitivity SettingsManager)
- Phase 8-2 ✅ (Photon Voice 2 통합 — Speaker/Recorder/PhotonVoiceView)
- U4 (ESC 일시정지 메뉴) — 팀원 슬라이더 진입 경로. **본 R14 와 분리 진행** (PerUserVoiceSettings 싱글턴 공유로 U4 도 동일 데이터 자동 사용)

**코드 작업 ✅ (2026-05-01, 미커밋):**
- ✅ `Features/Voice/Adapter/PerUserVoiceSettings.cs` — 싱글턴, OnLeftRoom→Clear, OnVolumeChanged 이벤트
- ✅ `Features/Voice/Adapter/PerUserVoiceApplier.cs` — Speaker 측 audioSource.volume 갱신, IsMine 가드, Owner null 대기 코루틴 (OnDisable 정리 포함)
- ✅ `Features/UI/Adapter/Voice/PerUserVoiceSliderEntry.cs` — 슬라이더 ↔ Settings 양방향 (대기실/HUD 행 공통)
- ✅ `Features/UI/Adapter/Voice/MicSensitivitySlider.cs` — 슬라이더 ↔ SettingsManager 양방향 (3곳 공통)
- ✅ `Features/UI/Adapter/Voice/VoicePanelHover.cs` — 호버 상태 + root CanvasGroup alpha lerp
- ✅ `Features/UI/Adapter/Voice/SliderHoverFade.cs` — 자식 슬라이더의 핸들/트랙 Image 알파 lerp
- ✅ `Features/UI/Adapter/Voice/VoicePanelController.cs` — 인게임 좌측 패널, OnPlayerEntered/Left/Joined 콜백으로 행 동적 추가/제거
- ✅ `LobbyPlayerEntry.cs` 수정 — Bind() 안에서 voiceSliderEntry.Bind/Unbind + voiceSliderRoot SetActive(!isYou)
- unity-reviewer Critical 2건 (PerUserVoiceApplier 코루틴 정리 / VoicePanelController Start+OnEnable 중복 빌드) 수정 완료

**사용자 인스펙터 작업 ⬜:**
- [ ] `PlayerStub.prefab` Speaker(또는 자식 AudioSource) GameObject 에 `AudioGainBoost` + `PerUserVoiceApplier` 부착. PerUserVoiceApplier 의 `gainBoost` 슬롯에 AudioGainBoost 드래그 (Awake 자동 탐색도 됨)
- [ ] **신규: `LobbyPlayer.prefab` 보이스 셋업 (Phase 8-2 보완)** — `PlayerStub.prefab` 과 동일 패턴:
  - `PhotonVoiceView` (PhotonView 와 동일 GameObject)
  - `Recorder` (TransmitEnabled=false, MicrophoneType=Photon)
  - `Speaker` (playOnAwake=false)
  - AudioSource (Speaker 가 자동 생성 또는 수동 부착, Output → MasterMixer/Voice 그룹)
  - `VoiceController` (자기 측 마이크 제어, IsMine 가드)
  - **`AudioGainBoost`** (AudioSource 와 같은 GameObject — `OnAudioFilterRead` 가 동작하려면 필수)
  - `PerUserVoiceApplier` (Speaker 측, 다른 사용자 볼륨 적용 — gainBoost 슬롯 자동 탐색 OK)
- [ ] **신규 `VoicePanel.prefab`** 작성 — 좌측 코너 패널, root 에 `CanvasGroup` + `VoicePanelHover` + `VoicePanelController` + `Image`(raycastTarget=true, 호버 hit). 자식에 자기 마이크 슬라이더(`MicSensitivitySlider` + `SliderHoverFade`) + 팀원 컨테이너
- [ ] **신규 `TeammateVoiceRow.prefab`** 작성 — 팀원 한 줄 (마이크 아이콘 Image + 이름 TMP_Text + 슬라이더). 컴포넌트: `PerUserVoiceSliderEntry` + `SliderHoverFade` + **`MicActivityIndicator`** (Discord 패턴, `Speaker.IsPlaying` 으로 자동 색 토글). `VoicePanelController.memberRowPrefab` 슬롯에 연결
- [ ] **GameScene Canvas 에 `VoicePanel.prefab` 좌측 인스턴스 배치**
- [ ] **MenuScene 의 대기실 패널에도 `VoicePanel.prefab` 좌측 인스턴스 배치** (인게임과 동일 prefab — VoicePanelController 가 PhotonNetwork.PlayerList 기반이라 씬 무관 동작)

**Phase 5 — 검증 ⬜:**
- [ ] ParrelSync 2~4 인스턴스 — A 가 B 볼륨 0 → A 측 B 음소거, 다른 인스턴스 영향 없음
- [ ] 대기실 A 볼륨 0.5 → 인게임 씬 전환 후 0.5 유지
- [ ] 룸 나가기 → 재입장 → 모든 볼륨 1.0 초기화
- [ ] 마이크 민감도 3곳 중 한 곳 변경 → 다른 2곳 즉시 갱신
- [ ] R12 Master Voice = 0 → PerUser 무관 무음
- [ ] 호버 알파: 패널 평소 흐림, 마우스 진입 시 또렷, 슬라이더 핸들 사라짐/등장
- [ ] 마이크 활성도: 송신 중 유저 행 마이크 아이콘 활성 색 / 음소거·없음·PTT 미발화 시 회색

**범위 외:**
- 보이스 음소거 전용 토글 — 슬라이더 0 으로 충분
- spatial audio / 거리 감쇠 — 별건
- 다른 유저가 "쟤가 날 음소거함" 알림 — 표준 X
- 유저별 볼륨 동기화 (다른 클라 전파) — 클라이언트 로컬 유지

**관련:** R12 (Master Voice / MicSensitivity), U4 (ESC 메뉴 팀원 섹션은 별도), Phase 8-2 (Photon Voice 2), [voice-chat.md](../systems/voice-chat.md)

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

**spec:** [systems/in-game-menu.md](../systems/in-game-menu.md) (2026-05-01 작성)

**확정 결정사항 (spec 참조):**
- UI: 반투명 dim + **중앙 모달**
- 메뉴 항목: Resume / 설정 / 룸 나가기 / 게임 종료
- 정지 정책: **솔로(PlayerCount == 1) 한정** `GameState.Paused` 진정 정지 / **멀티는 로컬 UI 토글만** (게임 흐름 유지)
- 호출 가능 상태: `Playing` / `BossFight` / `Paused`(레벨업 중) — ESC 메뉴는 레벨업 패널 위에 띄움
- 룸 나가기 행선지: 메뉴 씬 → **룸 리스트 패널** (호스트는 마이그레이션, 게스트는 단순 leave)
- 메인씬: ESC = 뒤로가기 (별건)

**의존성:**
- **Frame_PopUp 미작성** ([ui-frame.md § 3.2](../systems/ui-frame.md)) — 룸 나가기/게임 종료 **확인 다이얼로그** 가 모달 필요. ESC 메뉴 구현 들어가기 전에 Frame_PopUp 먼저 작성하거나 임시 모달로 진행 후 마이그레이션
- 메뉴 씬 설정 패널 재활용 — § 7 옵션 A(같은 프리팹 인스턴스화) / B(공통 프리팹 분리) 중 구현 단계 결정

**잔여 작업:**
- [ ] `InGameMenuController` (ESC 입력 + Toggle + 정지 분기 캐싱)
- [ ] `InGameMenuCanvas` 프리팹 (sortOrder=100, 중앙 모달 카드, 4 버튼)
- [ ] 솔로 판정 + GameState 분기 (Playing/BossFight 캐싱 → 닫을 때 복원, Paused 면 건드리지 않음)
- [ ] 룸 나가기 → `MenuSceneManager` 룸 리스트 진입점 호출 (HostMigrationHandler 기존 인프라 활용)
- [ ] 게임 종료 (`Application.Quit()` + 에디터 분기)
- [ ] **R14 Phase 4 팀원 보이스 슬라이더 섹션** 동반 (R14 와 합쳐 진행 권장)

### U5. 결과창 "나가기" → 방 리스트로
- 현재 Title 경유. RoomList 직행으로 라우팅 (`ResultManager.OnExit` → `ShowRoomList`).

### U6. 설정창 구조잡기 → **R12 로 통합 (2026-04-26)**
- ~~`TitlePanelController.OnClickSettings()` TODO~~ → [§ R12](#r12-설정-패널--video--audio--language) 로 흡수. Video/Audio/Language 카테고리로 확정.
- 키바인딩은 R12 범위 외 — 별건 작업으로 보류 (`settings.input` PlayerPrefs 키만 예약).

### U7. 스킬 카드에 적용 패시브 + multiplier 표시 (N18 후속, 옵션)
- 현재 [SkillCardDescriptionFormatter](../../Assets/Scripts/Features/UI/Presentation/SkillCardDescriptionFormatter.cs) 가 SO base 수치만 표시 (Survivors-like 표준). N18 도입으로 스킬마다 패시브 영향력 차등 가능해진 후, 카드에서 "이 스킬은 SkillRange 50% 만 적용" 같은 정보가 노출되지 않음.
- 우선순위: 낮음 (Vampire Survivors 등도 동일 패턴, 데미지 팝업으로 실효 확인). 후속 UX 향상 필요 시 추가.
- 구현 후보:
  - 카드 하단 작은 영역에 적용 패시브 리스트 (예: `SkillRange ×50%, ProjectileSpeed ×100%`) — 빈 리스트(전부 적용) 인 경우 미표시
  - `PlayerStats.ApplyAttackTo(data.GetDamageForLevel(level), data)` 호출로 실제 적용 데미지 부가 표시
- 0.5일.

---

## 드랍 / 장비 / 퀘스트 잔여 (DQ)

`drop-system-roadmap.md` (Phase 0~7) 의 코드 측 핵심은 모두 완료. 잔여는 HUD / 유저 Unity 배선 / Quest 핸들러 3종.
완료 내역 ledger = [completed-work.md § 드랍 시스템 구현](completed-work.md).

### DQ1. Quest 시스템 잔여
- [ ] **QuestProgressUI HUD** — 진입 진행 / 대기 카운트다운 / 진행률 바
- [ ] **DodgeFalling / Defend / KillInTime** 핸들러 (현재 KillTarget MVP 만)
- [ ] **맵 배치** (WFC 또는 사전 배치, `GameplayConfig` 에 거점 개수/최소 간격)
- [ ] **유저 Unity 배선** — QuestData SO 작성, QuestZone 거점 prefab + Scene PhotonView (Owner=null/Master), `SpawnManager.questBarrierVariants` 에 격리 몹 EnemyData 등록, 격리 몹 EnemyData (사실상 무한 HP) 작성

### DQ2. Essence HUD / Combo
- [ ] `EssenceSlotsUI` HUD — 보유 정수 2슬롯 표시 (Phase 4 와 병행 보류 상태)
- [ ] `EssenceCombo` VO — 조합 히든 효과 (얼음+불 / 얼음+번개 / 불+번개). 설계서 TBD 상태, 수치 확정 후 착수. [essence.md § 10](../game-design/essence.md) 5가지 결정 항목 선행 필요

### DQ3. Weapon HUD
- [ ] `WeaponSlotsUI` — HUD 4슬롯 + 등급 색상 테두리
- [ ] `WeaponCombinePreview` — 근접 시 조합 결과 프리뷰 팝업 (Frame)
- 현재는 `DebugOverlay` 로 modifier 수 / runtime effect 수 관찰 중

### DQ4. Phase 4 (Weapon) 유저 Unity 배선
1. `WeaponData` SO 5~8종 생성 (`Assets → Create → SwDreams/Data/WeaponData`) — `weaponId` / `rarity` / `statEntries` / `triggerEntries` / `combineRecipe`
2. `WeaponDatabase` SO 생성 — `weapons` 리스트에 위 SO 전부 등록 (네트워크 인덱스 기반이라 빌드 간 일관 유지)
3. `GameManager.weaponDatabase` Inspector 할당
4. `Weapon.prefab` 작성 — `WeaponPickup` + Collider2D(isTrigger) + Rigidbody2D(Kinematic) + SpriteRenderer
5. `DropSpawner.weaponPrefab` 할당, 적 SO 별 `EnemyDropTable.weaponChance` 0.01~0.05 조정
6. Player 프리팹 자식에 `PlayerWeaponInventory` 부착 + 자체 PhotonView 필수 (Essence 패턴)

### DQ5. Phase 5 (StatBoost) 유저 Unity 배선
1. `StatBoostData` SO 생성 (`SwDreams/Data/StatBoostData`) — `boostId` 고유 int 필수
2. `StatBoostDatabase` SO 생성 — boosts 리스트
3. `GameManager.statBoostDatabase` Inspector 할당
4. Player 프리팹 자식에 `StatBoostManager` 컴포넌트 부착
5. 테스트: 스킬 풀 만렙 후 레벨업 → StatBoost 패널 등장 확인

### DQ6. Phase 7 (혼돈 등급) 유저 Unity 배선
- 혼돈 스킬 SO 19종 Inspector 의 `Rarity` 필드 등급 재지정 (Common/Rare/Epic/Legendary)
- 현재 모두 기본값 Common — 등급 선정기는 동작하나 분포 평탄. 밸런싱 의도에 맞춰 분산
- `paramsByRarity[4]` 치트시트는 [completed-work.md § Phase 8](completed-work.md) 또는 `drop-system-roadmap.md § Phase 8-A 치트시트` 참조

### DQ7. 보류 (설계 선행 필요)
- **정수 데미지 스케일링** — 정수 OnHit 데미지가 SO 수치 그대로 사용. ATK / CritChance / 무기 영향 미반영. 5가지 결정 항목 [essence.md § 10](../game-design/essence.md). 결정 후 1~1.5시간
- **`BossChaosApplicator` 등급 가중치** — 현재 `ChaosEffectType` enum 직접 사용. 등급별 보스 강도 차등은 별도 기획 확정 후 (Phase 7 MVP 범위 외)
- **Architecture 부채** — `ChoicePanelKind` Shared 승격 여부 / `IChaosHookBus` 의 `Vector2` 의존 → `Position2D` VO 분리 / `IPlayerTransform` 포트 (`ChaosHandlerContext.playerRoot` 가 구체 Transform) / EssenceResolver 순수함수화 / DebugOverlay 릴리스 빌드 가드

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

### 8-2. Photon Voice 2 통합 ✅ 1차 (2026-04-27) → [completed-work.md](completed-work.md)

PunVoiceClient + PlayerStub 4컴포넌트 + VoiceController/MicToggleButton/MicTestService + SettingsPanel 통합 완료. micSensitivity = 0 함정 floor 클램프. 후행: 빌드 환경 송수신 검증 + R3 마이크 필터 드랍 ([§ R3](#r3-마이크-필터-드랍-아이템-재미-요소)). 상세 [../systems/voice-chat.md](../systems/voice-chat.md).

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

**Phase A — 코어 시스템 + 임포터** ✅ (2026-04-28) → [completed-work.md](completed-work.md)

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
