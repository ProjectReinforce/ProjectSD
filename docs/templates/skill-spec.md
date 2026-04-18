# 스킬 설계서: {스킬 이름}

> 이 템플릿을 복사해서 `docs/game-design/skills/{skill-id}.md`로 저장하세요.
> 상세 시스템 동작은 [systems/skill-executor.md](../../systems/skill-executor.md), [systems/trigger-effects.md](../../systems/trigger-effects.md) 참조.

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_longsword_01` |
| 한국어 이름 | 장검 |
| 영어 이름 | Longsword |
| 카테고리 | 액티브 / 패시브 / 혼돈 |
| 유형 | 투사체 / 근접 회전 / 장판 / 소환 / 디버프 / 설치 / 유틸리티 |
| 진화 여부 | Yes / No |
| 최종 업데이트 | YYYY-MM-DD |

## 2. 컨셉

한 문단으로 "이 스킬이 어떤 느낌인지, 플레이어가 뭘 기대하는지"를 서술.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 가장 가까운 적 / 랜덤 / 플레이어 고정 / 정면 / 360° 균등 |
| 발사 모드 | SimultaneousSpread / DelayedBurst / TwoPhase / Single (see [skill-executor.md](../../systems/skill-executor.md)) |
| 궤적(Trajectory) | Straight / Homing / Boomerang / Spiral / Pull / … (see `Adapter/Skill/Trajectories/`) |
| 관통 | 없음 / 관통 / 체인 비행 |
| 투사체 개수 스탯 적용 | 발사 수 / 방향 수 / 연발 수 / 적용 안 됨 |
| 지속시간 | 초 (장판·설치형) / N/A |

## 4. 수치

*구체적 수치는 `Assets/Data/Skills/*.asset` SO 에셋에서 최종 설정. 본 문서는 설계 의도를 기록한다. 하드코딩 금지.*

| 레벨 | 데미지 | 쿨다운 | 범위 | 기타 |
|---|---|---|---|---|
| 1 | 10 | 2.0s | 3.0m | — |
| 2 | 15 | 1.8s | 3.0m | — |
| … | | | | |

수식이 있다면 명시:
```
damage = base * (1 + attackPower * 0.1) * (isCrit ? critMult : 1)
```

## 5. TriggerEffect / EffectAction 매핑

이 스킬이 사용하는 Trigger→Action 조합을 나열. 각 핸들러 파라미터는 [trigger-effects.md](../../systems/trigger-effects.md) 참조.

| Trigger | EffectAction | 파라미터 (primary, secondary, tertiary) | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (15, 0, 0) | 기본 적중 데미지 |
| OnHit | Explode | (1.5, 1.0, 0) | 진화형 범위 폭발 |
| OnKill | — | — | |
| OnExpire | — | — | |
| OnInterval | — | — | |

## 6. 진화 경로

- **진화 조건(액티브 + 패시브 조합):** 예) `skill_longsword_01` + `passive_skill_range`
- **진화 후 이름:** {Evolved Name}
- **주요 변화:** Trajectory 교체 / Phase 추가 / 새 TriggerEffect / 수치 개편 등
- **Phase별 동작 (2페이즈 모드일 때):**
  - Phase 1: ...
  - Phase 2: ...

## 7. 데이터 계약 (ScriptableObject)

- **SO 타입:** `ProjectileSkillData` / `AreaSkillData` / `OrbitalSkillData` / `PlacedSkillData` / `DebuffSkillData` / `PassiveSkillData` / `ChaosSkillData`
- **에셋 경로:** `Assets/Data/Skills/{skill-id}.asset`
- **주요 필드:**
  - `firingMode`: enum (SimultaneousSpread / DelayedBurst / TwoPhase / Single)
  - `trajectoryType`: enum
  - `chainFlightCount`, `chainSearchRadius` (체인 비행 쓸 때)
  - `subProjectilePrefab` (SpawnProjectile 핸들러 쓸 때)
  - `applicableStats`: 이 스킬이 반영할 플레이어 스탯 필터
  - `triggerEffects[]`: SkillTriggerEffect 배열
  - 레벨별 수치 테이블
- **하드코딩 금지:** 체인 횟수·처형 기준·딜레이 등 모든 수치는 SO.

## 8. 네트워크 동기화

네트워크 기본 규약은 [systems/network-sync.md](../../systems/network-sync.md). 여기엔 이 스킬만의 특이사항만 적는다.

- **Executor 실행 주체:** 호스트 only / 각 클라이언트
- **투사체 동기화:** 로컬 생성(미동기화) / RPC로 전파
- **히트 판정:** 호스트만 / 양쪽
- **특이사항:** (ex. 체인 타겟 선정은 호스트가 결정해 RPC 반영)

## 9. 구현 체크리스트

- [ ] `Assets/Data/Skills/{skill-id}.asset` SO 생성
- [ ] Adapter 구현 (Skill/SkillExecutor/ISkillSpawner 연결)
- [ ] Trajectory/Effect 등록 (필요 시)
- [ ] TriggerEffect 매핑 SO 채우기
- [ ] SkillDatabase 등록
- [ ] 네트워크 동기화 검증 (`photon-sync-auditor`)
- [ ] Unity 리뷰 (`unity-reviewer`)
- [ ] 플레이테스트

## 10. 오픈 이슈 / 미해결 결정

- (결정하지 못한 수치, 실험 중인 밸런스, 미구현 핸들러 등)
