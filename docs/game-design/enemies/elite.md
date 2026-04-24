# 적 설계서: 엘리트형 (Elite)

> **SSOT:** 이 문서의 수치는 `Assets/Data/Enemy/Elite/*.asset` 및 `Assets/Data/EliteDropTable.asset` (SO)의 복제본이다.
> 밸런싱 수정은 **SO에서 먼저** 하고 이 문서는 그 결과를 반영한다.
> 참조 SO: `Assets/Data/Enemy/Elite/{EliteChaser, EliteRunner, EliteTank, EliteRangedTurretShot}.asset`, `Assets/Data/EliteDropTable.asset`
> 최종 동기화: 2026-04-24

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID (베이스) | `enemy_elite` |
| 한국어 이름 | 엘리트형 |
| 영어 이름 | Elite |
| 분류 | 엘리트 |
| 등장 시점 | SpawnManager 엘리트 타이머 (기본값 구현 코드 기준) |
| 등장 비율 | 별도 스폰 경로 (정수 드랍 유일 소스) |
| 최종 업데이트 | 2026-04-24 |

## 2. 컨셉

일반 몬스터보다 뛰어난 스펙을 가진 강화 버전. **정수(Essence)를 100% 드랍**하기 때문에 플레이어가 우선 처치 타겟으로 삼게 만든다. 패턴은 기반 타입을 따르되 수치를 대폭 끌어올림.

## 3. 기반 타입과 조합 (현재 구현된 4종)

| 엘리트 파일 | 기반 타입 | `isElite` |
|---|---|---|
| `EliteChaser.asset` | Chaser | 1 |
| `EliteRunner.asset` | Runner | 1 |
| `EliteTank.asset` | Tank | 1 |
| `EliteRangedTurretShot.asset` | Ranged Turret Shot | 1 |

*무리형 (Swarm) 은 엘리트화 제외 — 그룹 단위 컨셉이라 개별 강화 개념이 안 맞음.*

## 4. 스탯 (현재 SO 값)

| 엘리트 | HP | 데미지 | 이속 | EXP | `knockbackResistance` | `visualScaleMultiplier` |
|---|---|---|---|---|---|---|
| EliteChaser | **500** | **30** | 0.6 | **15** | **1** (완전 저항) | **1.5** |
| EliteRunner | **250** | 15 | 0.75 | **9** | 1 | 1.5 |
| EliteTank | **150** ⚠️ | 30 | 0.3 | 12 | 1 | 1.5 |
| EliteRangedTurretShot | **300** | **15** (접촉) / **30** (공격) | 0.48 | 15 | 1 | 1.5 |

### 기반 대비 배율

| 엘리트 | HP 배율 | 데미지 배율 | EXP 배율 |
|---|---|---|---|
| EliteChaser | ×10 | ×1.5 | ×3 |
| EliteRunner | ×10 | ×1.0 | ×3 |
| EliteTank | **×1.0** ⚠️ (미조정) | ×1.0 | ×1.0 |
| EliteRangedTurretShot | ×10 | ×1.5 | ×3 |

⚠️ **EliteTank는 기반과 HP/데미지/EXP가 동일** — SO 값이 아직 튜닝되지 않은 상태로 보임. 밸런싱 조정 대상.

### 시간 경과 스케일링
기반 적과 동일한 시간 체력 배율(`DifficultyData.hpCurve`, 0.8 → 15)을 곱한다.

## 5. 이동 / 공격 패턴

**기반 타입의 패턴을 그대로 사용.** 수치만 끌어올린 "상위 호환".
- 특수 패턴 추가는 **프로토타입 범위 밖**.
- 엘리트에는 페이즈 / 광폭화 없음 (그건 보스 영역).

## 6. 보상 (핵심 차별점)

### 드랍 확률 (`EliteDropTable.asset`)

| 드랍 | 확률 |
|---|---|
| `essenceChance` | **1.0 (100% 드랍)** |
| `weaponChance` | 0.0001 |
| `magnetChance` | 0 |
| `potionChance` | 0 |

- 정수 타입 가중치: `essenceTypeWeights = [1, 1, 1]` (Fire/Ice/Lightning 동등)
- 무기 등급 가중치: `[60, 25, 12, 3]` (Common/Rare/Epic/Legendary)

### 정수 (Essence)
엘리트 처치 시 반드시 1종 드랍. 선착순, 최대 2개 보유. 상세 [../essence.md](../essence.md).

### 경험치
기반의 3배 (Tank 제외, EliteTank는 미조정).

## 7. 데이터 계약

- **SO 타입:** `EnemyData` (공통, `isElite = 1` 플래그만)
- **에셋 경로:** `Assets/Data/Enemy/Elite/{variant}.asset`
- **드랍 테이블:** `EliteDropTable.asset` 전용

## 8. 네트워크

- 스폰/AI/드랍 판정 호스트.
- 정수 드랍 결과는 호스트 결정 후 RPC 로 전파 (선착순 처리도 호스트).

## 9. 비주얼 / 식별

- 엘리트 비주얼: `visualScaleMultiplier = 1.5` (일반 적 대비 1.5배 크기).
- 추가 이펙트/오라는 별건.

## 10. 구현 체크리스트

- [x] `EnemyData.isElite` / `dropTable` 필드 — 기존 SO 확장
- [x] 독립 스폰 타이머 + `RPC_SpawnElite`
- [x] 정수 드랍 **훅** (`SpawnManager.OnEnemyDied` 에서 `isElite + essenceChance` 롤링)
- [x] 엘리트 변형 SO 4종 생성 (완료)
- [ ] **EliteTank HP/데미지/EXP 배율 조정** (현재 기반과 동일)
- [ ] 스탯 배율이 기반 타입 스케일링과 합리적으로 곱해지는지 검증
- [ ] 체력 바 UI (일반 적과 구분) — 별건
- [ ] 엘리트 비주얼 식별 (발광/테두리 등) — 별건
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 11. 오픈 이슈

- EliteTank 수치 튜닝 필요 (현재 TankData와 동일)
- 나머지 Ranged 3 변형(Zone/Kite×2)의 엘리트판 존재 여부
- 특수 패턴 추가 여부 — 추후 확장
- 엘리트 처치 시 BGM·긴장도 연출 여부
