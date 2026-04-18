# 스킬 설계서: ??? (분기탄 / Splitter)

> _TBD — 정식 이름 미정. 본 스킬의 상세 설계는 아직 작성되지 않았습니다. [템플릿](../../templates/skill-spec.md)을 따라 채워주세요._

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_splitter_01` |
| 한국어 이름 | ??? (분기탄, 정식 이름 TBD) |
| 영어 이름 | Splitter (TBD) |
| 카테고리 | 액티브 |
| 유형 | 투사체 (분기) |
| 진화 여부 | Yes (갈래 수 3개 증가, 방향 랜덤) |
| 최종 업데이트 | 2026-04-18 (스켈레톤) |

## 2. 컨셉 (요약)

적 적중 시 두 갈래 분기, 반복 후 소멸. 진화 시 분기 수 3개, 방향 랜덤.

**구현 노트:** `SpawnProjectile` TriggerEffect 활용 예상. 현재 `SpawnProjectileHandler` 는 서브 프리팹을 **코드 수동 설정**해야 함 — [trigger-effects.md § 3.7](../../systems/trigger-effects.md). `SkillData.subProjectilePrefab` 필드 추가 예정.

## 3~10. 상세

> _TBD_
