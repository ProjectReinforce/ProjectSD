# 적/보스 설계서: {이름}

> 이 템플릿을 복사해서 `docs/game-design/enemies/{enemy-id}.md`로 저장하세요.

## 1. 메타

| 항목 | 값 |
|---|---|
| 엔티티 ID | `enemy_basic_chaser` |
| 한국어 이름 | 기본 추적형 |
| 영어 이름 | Basic Chaser |
| 분류 | 기본 / 빠른형 / 둔한형 / 무리형 / 원거리형 / 엘리트 / 보스 |
| 등장 시점 | 0분~ / 3분~ / 7분~ / 10분 보스 / 등 |
| 최종 업데이트 | YYYY-MM-DD |

## 2. 컨셉

한 문단으로 이 적의 성격/역할/플레이어에게 주는 압박감 서술.

## 3. 스탯

| 레벨 | HP | 데미지 | 이속 | 공격 범위 | 점수(EXP) | 기타 |
|---|---|---|---|---|---|---|
| 1 | 100 | 10 | 3.0 | 0.5m | 10 | — |

수식/스케일링:
```
hp(t) = baseHp * hpMultiplier(timePhase)    // 시간대별
hp(n) = hp(t) * playerCountMultiplier(n)    // 인원 스케일링
```
시간/인원 스케일링 상수는 [systems/spawn-rules.md](../../systems/spawn-rules.md) 참조.

## 4. 이동 패턴

- **이동 타입:** ChaseMovement / SwarmMovement / StationaryMovement / 보스 전용 커스텀
- **참조:** `Adapter/Entity/Enemy/Movement/` 의 어느 클래스
- **추적 대상:** 가장 가까운 플레이어 / 랜덤 / 특정 플레이어 지속
- **특수 동작:** □ 돌진 □ 텔레포트 □ 정지 후 공격 □ 그룹 스폰 □ 경로 차단

## 5. 공격 패턴 (보스/엘리트의 경우)

### Phase 1 — 조건: HP 100%~60%
- 패턴 A: (설명)
- 패턴 B: (설명)
- **전이 조건:** HP ≤ 60% / 시간 경과 N초 / 특정 이벤트

### Phase 2 — 조건: HP 60%~30%
- ...
- **전이 조건:** HP ≤ 30%

### Phase 3 (Enrage) — 조건: HP 30%~0%
- ...

각 패턴에 대해 **어떤 인터페이스/구현체를 쓰는지** 명시.

## 6. 보상

- **경험치:** 점수 값
- **드랍:** 경험치 오브 / 정수(엘리트) / 무기(낮은 확률)
- **처치 이벤트 트리거:** OnKill TriggerEffect 대응 여부

## 7. 데이터 계약 (ScriptableObject)

- **SO 타입:** `EnemyData` / `BossData`
- **에셋 경로:** `Assets/Data/Enemies/{enemy-id}.asset`
- **주요 필드:** HP, Speed, Damage, Score, MovementType, AttackPatterns[] 등

## 8. 네트워크 동기화

네트워크 기본 규약은 [systems/network-sync.md](../../systems/network-sync.md).

- **스폰 주체:** MasterClient만 (일반) / Scene
- **AI 실행 주체:** 호스트
- **위치/체력 동기화:** 주기(기본 20Hz), 보간 필요 여부
- **페이즈 전이:** 호스트가 판정, RPC로 전파

## 9. 구현 체크리스트

- [ ] `Assets/Data/Enemies/{enemy-id}.asset` SO 생성
- [ ] Movement 구현 선택 (재사용 or 신규)
- [ ] 공격 패턴 스크립트 (보스의 경우 Phase별)
- [ ] EnemyDatabase / BossDatabase 등록
- [ ] 네트워크 동기화 검증 (`photon-sync-auditor`)
- [ ] Unity 리뷰 (`unity-reviewer`)
- [ ] 플레이테스트

## 10. 오픈 이슈

- (결정되지 않은 패턴, 밸런싱 미정 수치 등)
