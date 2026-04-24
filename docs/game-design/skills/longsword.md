# 스킬 설계서: 장검 (Longsword)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | 3 (`skillId`) |
| 한국어 이름 | 장검 |
| 영어 이름 | Longsword |
| 카테고리 | 액티브 |
| 유형 | 근접 회전 |
| 진화 여부 | Yes (검무) |
| 최종 업데이트 | 2026-04-24 |

> **SSOT:** 이 문서의 수치는 `Assets/Data/Skill/Active/003_LongSword.asset` 의 복제본이다.

## 2. 컨셉

플레이어 주변을 회전하는 검. 근접 스킬이지만 방어 범위가 넓어 적을 자연스럽게 쓸어낸다. 투사체 개수가 늘어날수록 방어 반경이 넓어진다.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 플레이어 주변 회전 |
| 발사 모드 | TwoPhase (회전 → 발사) |
| 궤적 | Orbital(회전) → 진화 시 Phase 2에서 Straight 발사 |
| 관통 | 회전 중 관통, 발사 후 관통 |
| 투사체 개수 스탯 적용 | 회전 검 수 증가 (360°/n 균등 배치) |

**동작:** 플레이어 주변을 n개의 검이 1바퀴 회전 후 사라짐.

## 4. 수치 (현재 SO 값)

### 4.1 레벨별

| 레벨 | 데미지 | 쿨다운 |
|---|---|---|
| 1 | **18** | **1.50s** |
| 2 | 22 | 1.40s |
| 3 | 27 | 1.30s |
| 4 | 33 | 1.20s |
| 5 | 40 | 1.10s |
| 6 | 48 | 1.00s |
| 7 | **58** | **0.90s** |

### 4.2 발사 파라미터

| 필드 | 값 |
|---|---|
| `firingMode` | SimultaneousSpread (0) |
| `burstDelay` | 0.1초 |
| `areaRadius` | 2 |
| `areaDuration` | **1초** (회전 지속 시간) |
| `trajectoryType` | Homing (1) — Phase 2 발사용 |
| `aimType` | 3 (회전 중심) |
| `spreadPattern` | 2 (등분 배치) |
| `knockbackForce` | 0.35 |
| `maxInstances` | 10 |
| `effectPrefab` | 타격 이펙트 연결됨 |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, 0, 0) | 기본 적중 |

## 6. 진화 경로

- **진화 조건:** 장검 + 스킬 범위 증가
- **진화 후 이름:** 검무 (Sword Dance)
- **주요 변화:** 회전 완료 후 **각 검이 자신의 정면 방향으로 발사**됨. 플레이어 기준 n개 방향 발사. 발사된 투사체는 적을 관통.

**Phase별 동작 (TwoPhase):**
- **Phase 1 (회전):** 1바퀴 회전. Executor가 완료 콜백 대기.
- **Phase 2 (발사):** Phase 1 완료 콜백 → 각 검의 정면 방향으로 직선 발사 (Straight + 관통).

**주의:** 2페이즈 모드의 Phase 2 발사 동작은 과거 커밋 `1f225a555` 에서 복구됨. Phase 1 완료 판정은 Executor 내부 플래그가 관리.

## 7. 데이터 계약

- **SO 타입:** `ProjectileSkillData`
- **에셋 경로:** `Assets/Data/Skill/Active/003_LongSword.asset`
- **evolvedSkill:** `203_EvolvedLongSword.asset`
- **주요 필드:** firingMode=SimultaneousSpread, areaRadius/Duration 로 회전 판정, Phase2 발사는 진화형에서

## 8. 네트워크

- Executor 호스트 실행. Phase 전이는 호스트가 타이밍 판정.
- 진화형의 Phase 2 발사 타이밍은 **네트워크 동기화 민감**. 로컬/리모트 발사 타이밍 오차 있을 수 있음 → 추가 검증 필요.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] TwoPhase 발사 모드 연결 확인
- [ ] 진화형 Phase 2 트랜지션 테스트 (회귀 체크: `1f225a555`)
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트 (특히 Phase 2 발사 타이밍)

## 10. 오픈 이슈

- Phase 2 발사 시 각 검이 "자신의 정면"을 어떻게 해석할지 (Phase 1 마지막 위치 기준 vs 플레이어 기준)
- 회전 속도와 반경의 밸런스 (스킬 범위 패시브가 어느 것을 확장하는지)
