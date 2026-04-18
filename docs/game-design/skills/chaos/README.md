# 혼돈 스킬 (Chaos Skills)

전체 목록과 선택/적용 규칙은 [../INDEX.md § 4 혼돈 스킬](../INDEX.md) 참조. 보스에 적용된 혼돈 스킬의 실제 효과는 [../../enemies/boss.md](../../enemies/boss.md) 가 SSOT.

각 혼돈 스킬 1개당 1 파일로 관리 (스켈레톤 상태). 수치/등급별 세부 효과/보스 적용 효과는 TBD — 밸런싱 단계에서 채워넣는다.

## 혼돈 스킬 파일 규칙

- 파일명: `{chaos-skill-slug}.md` (영어 slug)
- 내용은 [../../../templates/skill-spec.md](../../../templates/skill-spec.md) 기반. 카테고리 "혼돈", SO 타입 `ChaosSkillData`.
- **보스 적용 효과**는 본인 파일에 기재하되, `enemies/boss.md` 의 표에서 링크로 참조되는 형태로 동기화.
