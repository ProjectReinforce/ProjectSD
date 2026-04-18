# 스킬 설계서: 메아리 (Echo)

> _TBD — 본 스킬의 상세 설계는 아직 작성되지 않았습니다. [템플릿](../../templates/skill-spec.md)을 따라 채워주세요._

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_echo_01` |
| 한국어 이름 | 메아리 |
| 영어 이름 | Echo |
| 카테고리 | 액티브 |
| 유형 | 복제 |
| 진화 여부 | Yes (A: 재현 횟수 증가, B: 재현 범위 확대) |
| 최종 업데이트 | 2026-04-18 (스켈레톤) |

## 2. 컨셉 (요약)

마지막 스킬 공격을 일정 시간 후 재현. 진화 시 재현 횟수↑ 또는 범위↑.

**의존:** `IFireRecorder` 인터페이스 + `Refire` 핸들러 필요. 둘 다 **현재 미구현** ([systems/skill-executor.md § 4](../../systems/skill-executor.md), [systems/trigger-effects.md § 3.11](../../systems/trigger-effects.md)). 본 스킬 구현 시 함께 작성.

## 3~10. 상세

> _TBD_
