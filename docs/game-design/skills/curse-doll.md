# 스킬 설계서: 저주인형 (Curse Doll)

## 1. 메타

| 항목 | 값 |
|---|---|
| 스킬 ID | `skill_curse_doll_01` |
| 한국어 이름 | 저주인형 |
| 영어 이름 | Curse Doll |
| 카테고리 | 액티브 |
| 유형 | 디버프 |
| 진화 여부 | Yes (역병 인형) |
| 최종 업데이트 | 2026-04-18 |

## 2. 컨셉

랜덤 타겟 1마리 지정 디버프. 적 집단에서 특정 개체를 약화시키는 타겟팅 스킬. 진화형은 "저주가 전염되는" 형태로 확장된다.

## 3. 기본 동작

| 항목 | 값 |
|---|---|
| 조준 | 랜덤 적 1마리 |
| 발사 모드 | Single |
| 궤적 | — (즉시 디버프 부여) |
| 관통 | — |
| 투사체 개수 스탯 적용 | **적용 안 됨** |

**동작:** 랜덤 적 1마리에게 일정 시간 저주 부여. 저주 대상은 받는 데미지 증폭. 비주얼 표시(아이콘 or 색상 변경).

## 4. 수치

*실제 값은 `Assets/Data/Skills/curse_doll_01.asset`.*

| 레벨 | 피해 증폭 배율 | 쿨다운 | 지속시간 | 기타 |
|---|---|---|---|---|
| 1 | — | — | — | *TBD* |

## 5. TriggerEffect 매핑

| Trigger | EffectAction | 파라미터 | 용도 |
|---|---|---|---|
| OnFire | ApplyVulnerability | (mult, duration, 0) | 타겟에 디버프 마크 |

*타겟 선정 자체는 Executor가 처리. OnFire 시점에 선정된 타겟에 `DebuffMark` 부착.*

## 6. 진화 경로

- **진화 조건:** 저주인형 + 스킬 쿨타임 감소
- **진화 후 이름:** 역병 인형 (Plague Doll)
- **주요 변화:** **저주 대상이 사망하면 가장 가까운 적 2마리에게 전이**.

**진화형 구현:**
- 사망 이벤트 구독 → 주변 탐색 → 저주 부여 (TriggerEffect: `OnKill → ApplyVulnerability to nearby`)
- 전이 대상 수(기본 2)는 **SO 설정**.

## 7. 데이터 계약

- **SO 타입:** `DebuffSkillData`
- **에셋 경로:** `Assets/Data/Skills/curse_doll_01.asset`
- **주요 필드:** firingMode=Single, applicableStats=[스킬 쿨타임 감소, 스킬 유지 시간]
- 관련 컴포넌트: `Assets/Scripts/Adapter/Skill/Effects/DebuffMark.cs`

## 8. 네트워크

- 타겟 선정은 호스트.
- 디버프 마크 동기화는 호스트 → RPC.
- 진화형 전이도 호스트가 판정.

## 9. 구현 체크리스트

- [ ] SO 생성
- [ ] DebuffMark 부착/제거 로직
- [ ] 비주얼 표시 결정 (아이콘 vs 색상 — UI 디자이너와 협의)
- [ ] 진화형 전이 대상 수 SO 반영
- [ ] `photon-sync-auditor`
- [ ] 플레이테스트

## 10. 오픈 이슈

- 비주얼 표시 방식 확정 (아이콘 vs 색상)
- 전이 대상 수(기본 2)가 적절한지 밸런싱
- 저주 대상 사망 시 즉시 전이 vs 다음 틱에 전이 정책
