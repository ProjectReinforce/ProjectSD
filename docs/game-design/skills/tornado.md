# 스킬 설계서: 회오리바람 (Tornado)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_tornado_01` |
| 한국어 이름 | 회오리바람 |
| 영어 이름 | Tornado |
| 카테고리 | 액티브 |
| 유형 | 투사체 (CC) |
| 진화 여부 | Yes (대선풍) |
| 최종 업데이트 | 2026-04-18 |

## 2. 컨셉

적을 모으면서 데미지를 주는 CC 투사체. 360° 분포로 주변을 "흔드는" 느낌. 진화형은 점점 퍼지는 나선형으로 더 큰 범위를 커버.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 후방 기준 360°/n 방향 |
| 발사 모드 | SimultaneousSpread |
| 궤적 | Straight (느린 전진) |
| 관통 | 관통 |
| 투사체 개수 스탯 적용 | 방향 수 증가 (360°/n) |

**동작:** 플레이어 후방을 기준으로 n방향으로 회오리 동시 발사. 천천히 전진하며 적을 모으고 데미지. 적을 관통.

## 4. 수치

*실제 값은 `Assets/Data/Skills/tornado_01.asset`.*

| 레벨 | 데미지 | 쿨다운 | 이동속도 | 지속시간 | CC 강도 |
|---|---|---|---|---|---|
| 1 | — | — | — | — | *TBD* |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, 0, 0) | 관통 피해 |
| OnHit | Pull | (radius, force, 0) | 적 모으기 (1회성) |

> Pull 핸들러는 트리거 1회 발동당 1회 — 비행 중 지속이 아니라 각 프레임 OnHit 시마다 발동. [trigger-effects.md § 3.6](../../systems/trigger-effects.md).

## 6. 진화 경로

- **진화 조건:** 회오리바람 + 스킬 범위 증가
- **진화 후 이름:** 대선풍 (Great Tempest)
- **주요 변화:** **Trajectory 를 Spiral 로 변경**. 발사점 기준 나선형으로 점점 멀어지며 회전.

**진화형 구현:**
- `trajectoryType = Spiral` (SimpleTrajectory 일종) + 회전 파라미터 SO.

## 7. 데이터 계약

- **SO 타입:** `ProjectileSkillData`
- **에셋 경로:** `Assets/Data/Skills/tornado_01.asset`
- **주요 필드:** firingMode=SimultaneousSpread, trajectoryType=Straight(진화 후 Spiral), applicableStats=[스킬 범위, 공격력]

## 8. 네트워크

- Executor 호스트, 투사체 로컬 렌더.
- Pull 효과의 적 이동은 호스트 판정.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] SpreadPatterns 360°/n 균등 분할 확인
- [ ] 진화형 Spiral trajectory 연결
- [ ] Pull 핸들러 파라미터 튜닝
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 기본 회오리의 이동속도(느린 전진)와 관통 조합 밸런스
- 진화형 나선 회전 방향 (고정 vs 랜덤)
