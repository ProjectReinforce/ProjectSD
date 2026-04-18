# 스킬 설계서: 부메랑 (Boomerang)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_boomerang_01` |
| 한국어 이름 | 부메랑 |
| 영어 이름 | Boomerang |
| 카테고리 | 액티브 |
| 유형 | 투사체 (왕복) |
| 진화 여부 | Yes (그래비톤 부메랑) |
| 최종 업데이트 | 2026-04-18 |

## 2. 컨셉

전방으로 나갔다가 돌아오는 투사체. 갈 때/올 때 모두 데미지. 균등 360°/n 방향으로 동시에 뿌려지며, 진화형은 복귀 경로가 "블랙홀"처럼 적을 끌어모은다.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 360°/n 방향 (투사체 개수 스탯 기준) |
| 발사 모드 | SimultaneousSpread |
| 궤적 | Boomerang (`BoomerangTrajectory`) |
| 관통 | 관통 |
| 투사체 개수 스탯 적용 | 방향 수 증가 (360°/n) |

**동작:** 플레이어 주변 n방향으로 부메랑 동시 발사. 전방 발사 후 되돌아옴. 갈 때/올 때 모두 데미지. 적을 관통.

## 4. 수치

*실제 값은 `Assets/Data/Skills/boomerang_01.asset`.*

| 레벨 | 데미지 | 쿨다운 | 사거리 | 기타 |
|---|---|---|---|---|
| 1 | — | — | — | *TBD* |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnHit | DealDamage | (base, 0, 0) | 갈 때/올 때 공통 |

## 6. 진화 경로

- **진화 조건:** 부메랑 + 넉백 거리 증가
- **진화 후 이름:** 그래비톤 부메랑 (Graviton Boomerang)
- **주요 변화:** **복귀 경로에서 투사체 위치 중심으로 적을 끌어모음**. 비행 중 지속 흡인.

**진화형 구현 방법:**
- **TriggerEffect (`Pull`)이 아닌** `BoomerangTrajectory.hasPullOnReturn` 을 사용 — 비행 중 매 프레임 흡인.
- [systems/trigger-effects.md § 4](../../systems/trigger-effects.md) 의 "끌어당김 동작 구분" 참조.

## 7. 데이터 계약

- **SO 타입:** `ProjectileSkillData`
- **에셋 경로:** `Assets/Data/Skills/boomerang_01.asset`
- **주요 필드:** firingMode=SimultaneousSpread, trajectoryType=Boomerang, hasPullOnReturn(진화형, SO)

## 8. 네트워크

- Executor 호스트 실행. 투사체는 로컬 렌더.
- 흡인은 호스트에서 적 이동 판정.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] BoomerangTrajectory 곡선 궤도 확인 (실제 부메랑처럼 휘어지는지)
- [ ] 진화형 `hasPullOnReturn` 토글
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 포물선 궤도 vs 직선 왕복 궤도 정책
- 흡인 강도 밸런싱 (너무 강하면 보스 전투 망가짐)
