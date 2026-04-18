# 혼돈 스킬: 연쇄 폭발 (Chain Explosion)

> _TBD — 등급별 수치는 밸런싱 단계._

| 항목 | 값 |
|---|---|
| 스킬 ID | `chaos_chain_explosion` |
| 카테고리 | 혼돈 |
| SO 타입 | `ChaosSkillData` |
| 메인 효과 (플레이어) | 적 처치 시 폭발하여 주변 적에게 데미지. 연쇄 |
| 보스 적용 효과 | 보스가 처치한 플레이어 위치에 폭발 (3초 후, 데미지 40, 범위 5m) |

**등급별 폭발 데미지:**

| 등급 | 폭발 데미지 |
|---|---|
| 일반 | 8 |
| 희귀 | 10 |
| 영웅 | 15 |
| 전설 | 20 |

**구현 노트:** 프레임당 연쇄 횟수 상한 필요 (무한 루프 방지) — [../INDEX.md § 성능 관리](../INDEX.md).

TriggerEffect 매핑: `OnKill → Explode(radius, 1.0, 0)`. 런타임 추가 규약은 [../../../systems/trigger-effects.md § 5](../../../systems/trigger-effects.md) (`chaos_chain_explosion`).
