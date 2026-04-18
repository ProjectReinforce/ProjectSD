# 시스템 명세서: {시스템 이름}

> 이 템플릿을 복사해서 `docs/systems/{system-id}.md`로 저장하세요.

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `damage-formula` |
| 이름 | 데미지 계산 시스템 |
| 분류 | 전투 / UI / 네트워크 / 세이브 / 기타 |
| 의존 레이어 | Domain / Application / Adapter / Presentation |
| 최종 업데이트 | YYYY-MM-DD |

## 2. 목적

한 문단으로 이 시스템이 존재하는 이유, 어떤 문제를 해결하는지.

## 3. 인터페이스

외부에서 이 시스템을 호출하는 방법:

```csharp
public interface IDamageCalculator
{
    DamageResult Calculate(Attacker a, Defender d, SkillContext ctx);
}
```

호출 위치 예시:
- `SkillExecutor.ApplyHit()`
- `Enemy.TakeDamage()`

## 4. 공식 / 규칙

수식:
```
final = base * (1 + power * 0.1)
      * (isCrit ? crit : 1)
      * (1 - defender.resist)
```

- 반올림 기준: 소수점 내림 (Mathf.FloorToInt) / 반올림 / 최솟값 1
- 오버플로/언더플로 처리: 최솟값 보장 등

## 5. 데이터 출처

- ScriptableObject: `SkillData.BaseDamage`, `CharacterData.AttackPower`
- 런타임 버프/디버프: ApplyVulnerability 마크, Essence 부여 효과 등 — 어떤 방식으로 합산되는지

## 6. 네트워크

네트워크 기본 규약은 [network-sync.md](network-sync.md) 참조.

- **계산 주체:** 호스트만 / 각자
- **결과 전파:** RPC 이름, 이벤트 코드
- **순서 보장 필요성:** 있음/없음

## 7. 테스트

- **단위 테스트 위치:** `Assets/Tests/…`
- **플레이 모드 시나리오:** (예) "보스 Phase 3에서 유리대포 + 피격 시 반사 발동하는지"
- **회귀 체크 포인트:** (예) 장검 진화 Phase2 발사 타이밍

## 8. 기존 코드 참조

- **핵심 구현 파일:** `Assets/Scripts/Adapter/Skill/SkillExecutor.cs` 등 (절대 경로 또는 루트 기준)
- **관련 인터페이스:** `Assets/Scripts/Domain/...`
- **Data(SO):** `Assets/Data/Skills/...`

## 9. 알려진 제약 / 트레이드오프

아래 항목들 중 해당되는 것을 체크하고 세부 설명:

- [ ] **부동소수점 오차** — 클라이언트 간 미세 차이 허용 범위
- [ ] **성능 상한** — 최대 동시 처리 개수 (예: 프레임당 연쇄 폭발 횟수)
- [ ] **네트워크 지연** — X ms 초과 시 동작 보장 안 됨
- [ ] **동기화 실패 복구** — 호스트 데이터를 정답으로 간주 등
- [ ] **기타 하드 리밋:** 명시

## 10. 변경 이력 (선택)

- YYYY-MM-DD: 공식 수정 이유 / 관련 커밋
