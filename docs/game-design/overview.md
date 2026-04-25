# Game Design Overview — Sweepin' Dreams

*"Sweet Dream" + "Sweep" — 꿈의 세계에서 적을 쓸어버리는 언어의 유희*

최종 업데이트: 2026-04-24

> **SSOT:** 이 문서의 수치는 `Assets/Data/GameplayConfig.asset` 등 SO의 복제본이다.
> 개별 수치 상세는 [rules.md](rules.md)에서 관리. 이 문서는 전체 개요.

## 1. 프로젝트 개요

| 항목 | 값 |
|---|---|
| 프로젝트명 | Sweepin' Dreams |
| 장르 | Survivors-like / 멀티플레이어 액션 |
| 플레이 인원 | 1~4명 Co-op (솔로 공식 지원) |
| 단일 게임 시간 | **15분** (`GameplayConfig.totalGameTime = 900s`) |
| 보스 등장 | 15분 (`bossSpawnTime = 900s`) |
| 대상 플랫폼 | PC (Stove Indie → Steam) |
| 엔진 | Unity 2D URP + Photon PUN 2 |
| 개발 규모 | 개발자 2명 + UI 디자이너 1명 |

## 2. 핵심 콘셉트

Vampire Survivors에서 영감을 받은 멀티플레이 Survivors-like. 1~4명이 협력해 밀려오는 적 군단을 쓸어버리고, 레벨업마다 스킬을 선택해 자신만의 빌드를 구축한다.

### 핵심 재미

- **쓸어버리는 재미** — 몰려오는 적을 다수 스킬로 쓸어버리는 쾌감
- **빠른 성장 체감** — 짧은 플레이 타임 내에서 눈에 보이는 성장
- **운과 선택의 캐리** — 랜덤 스킬 조합으로 실패해도 다시 시도하게 되는 구조
- **재미 선택 = 클리어 선택** — 단순 재미 선택이 클리어에 도움이 되는 설계 (예: 최대 체력 스택 → 반사 데미지)

### 차별화 포인트

- **멀티플레이 중심 설계** — 1~4인 Co-op 기본, 팀원 간 시너지와 캐리 구조
- **혼돈 스킬** — 게임 규칙 자체를 바꾸는 스킬이 매 Run의 개성을 결정
- **보스에게 적용되는 미선택 혼돈 스킬** — 매번 다른 보스전
- **정수/무기 시스템** — 속성 부여·장비 조합으로 빌드 다양성 확보
- **퀘스트 시스템** — 전투 외 부가 목표

## 3. 기본 게임 루프

```
이동 → 자동 전투 → 경험치 획득 → 레벨업 → 스킬 선택 → 반복 → 보스 등장 → 클리어/실패
```

세부 플로우와 UI 화면 전환은 [flow-design.md](flow-design.md) 및 [flow-diagram.mermaid](flow-diagram.mermaid) 참조.

### 플레이 흐름 요약

1. **이동** — WASD/컨트롤러 자유 이동. 이동 판단이 생존의 핵심.
2. **자동 전투** — 공격은 자동. 플레이어는 이동에 집중.
3. **레벨업 및 스킬 선택** — 경험치는 팀 공유. 레벨업 시 모든 플레이어가 동시에 선택 화면을 봄. 전체 스킬 풀(액티브 + 패시브 통합)에서 랜덤 3개 제시. 타임아웃 시 랜덤 자동 선택.
4. **보스 등장** — 보스전 제외 최대 15분이 지나면 보스 등장. 보스에게 **미선택 혼돈 스킬 1개**가 적용되어 매 Run마다 다른 전투.

타이밍 구조와 구간별 이벤트는 [systems/spawn-rules.md](../systems/spawn-rules.md)에서 수식·수치 관리.

## 4. 스킬 시스템 (개념)

| 분류 | 설계 개수 | SO 구현 | 위치 |
|---|---|---|---|
| 액티브 스킬 | 24종 (설계서 기준) | **10종** | `Assets/Data/Skill/Active/` (001~010) |
| 패시브 스킬 | 19종 | **13종** | `Assets/Data/Skill/Passive/` (101~113) |
| 진화 스킬 | 10종 | **10종** | `Assets/Data/Skill/Evolved/` (201~210) |
| 혼돈 스킬 | 19종 | **6종** | `Assets/Data/Skill/Chaos/` (301~306) |

- **6슬롯 제한** — `GameplayConfig.maxSkillSlots = 6` (액티브 + 패시브 합계, 시작 패시브 포함). 모든 슬롯이 차고 만렙이면 능력치(스탯 부스트) 선택지로 전환.
- **진화 시스템** — 특정 액티브 + 특정 패시브가 모두 최대 레벨일 때 진화. 2슬롯이 1슬롯으로 합쳐지며, 새 효과 발현. 진화 확률: `evolutionChance = 0.7`.
- **혼돈 스킬 선택 레벨** — **레벨 10 / 20 / 30** (`GameplayConfig.chaosLevels`).
- **보스에게 적용되는 미선택 혼돈 스킬** — 혼돈 스킬 선택 시 미선택 중 랜덤 1개가 보스에게 적용.

개별 스킬 24종 상세는 [skills/INDEX.md](skills/INDEX.md) 및 하위 파일. 발사 메커니즘은 [systems/skill-executor.md](../systems/skill-executor.md), TriggerEffect 핸들러는 [systems/trigger-effects.md](../systems/trigger-effects.md).

## 5. 캐릭터

- 초기 출시 시점 캐릭터 3명 제공. 언락 시스템으로 추가 해금.
- 멀티플레이에서 같은 캐릭터 중복 선택 가능.
- 캐릭터 구성: **시작 액티브 1개** + **시작 패시브 최대 1개**(없을 수도) + **기본 스탯 차이**(고유 특성 시스템 없음).

세부 게임 규칙(사망/부활, 경험치, 6슬롯)은 [rules.md](rules.md).

### 캐릭터 구현 현황

`Assets/Data/Character/` 에 3종 SO 존재 (`AData.asset`, `BData.asset`, `CData.asset`).
대표 기본 스탯 (A 캐릭터 기준):

| 필드 | 값 |
|---|---|
| `maxHP` | 100 |
| `moveSpeed` | 0.84 |
| `attackMultiplier` | 1.0 |
| `critDamage` | 1.5 (150%) |
| `cooldownReduction` | 0 |
| `knockback` | 1.0 |
| `expMultiplier` | 1.0 |
| `defenseBonus` | 0 (양수 = 강함, 0.05 = 받는 데미지 -5%) |
| `healMultiplier` | 1.0 |
| `hpRegen` | 0 (HP/초, HealMultiplier 영향 안 받음) |
| `iFrameDuration` | 0.4 (피격 후 무적 시간, 초) |

## 6. 적과 보스

- **적 종류:** 기본 추적형(Chaser), 빠른형(Runner), 둔한형(Tank), 무리형(Swarm), 원거리형(Ranged 4변형), 엘리트형(4변형). 상세는 [enemies/INDEX.md](enemies/INDEX.md).
- **난이도 스케일링:** 시간 경과(`DifficultyData` AnimationCurve 기반) + 플레이 인원. 수식은 [systems/spawn-rules.md](../systems/spawn-rules.md).
- **보스:** 15분 후 등장. baseHP 20000, 3페이즈 + 미선택 혼돈 스킬 1개 적용. 상세는 [enemies/boss.md](enemies/boss.md).

## 7. 정수 시스템

엘리트형 적이 드랍하는 속성 부여 아이템 (얼음/불/번개, 최대 2개). 상세는 [essence.md](essence.md).

## 8. 무기 시스템

모든 적이 매우 낮은 확률로 드랍하는 장비 (LoL 아이템식 스탯 부여 + 조합, 슬롯 4개). 상세는 [weapon.md](weapon.md).

## 9. 퀘스트 / 능력치 / 기타

- **퀘스트:** 맵 거점 진입형 부가 목표 (4유형: 처치/시간내킬/회피/지키기). 보상은 능력치 부스트. 상세는 [quest.md](quest.md).
- **능력치 시스템:** 만렙 후 레벨업 + 퀘스트 보상으로 획득. 4등급 체계. 상세는 [stat-boost.md](stat-boost.md).
- **기타 아이템:** 자석 / 물약. 상세는 [items.md](items.md).
- **언락 / 메타 진행:** 목표 달성 시 새 스킬/캐릭터/코스메틱 언락. Steam 업적.
- **영구 스탯 강화:** 초기 출시엔 제외 (멀티 밸런스). EA 피드백 후 재검토.

## 10. 미확정 사항

| 분류 | 항목 | 결정 시점 |
|---|---|---|
| 스킬 | 혼돈 스킬 등급별 세부 수치 | 밸런싱 |
| 스킬 | 보스에게 부여된 혼돈 스킬을 선택지에서 빼는지 | 밸런싱 |
| 스킬 | 혼돈 스킬 19종 중 나머지 13종 SO 구현 | 구현 |
| 스킬 | 액티브 11~24번 SO 구현 (설계서만 존재) | 구현 |
| 스킬 | 스킬 #11~24 진화 조합 | 기획 |
| 스킬 | 경험치 공식 `5 + 레벨×4` 가 테스트값인지 최종값인지 | 밸런싱 |
| 적 | EliteTank 스탯 배율 (현재 기반 Tank와 동일) | 밸런싱 |
| 적 | 엘리트형 스폰 간격 최종값 | 밸런싱 |
| 적 | Ranged 비율과 일반 타입 비율의 관계 정리 | 기획 & 구현 |
| 정수 | 조합표 상세 / 상충 조합 처리 | → [essence.md § 9](essence.md) |
| 무기 | 종류·조합 레시피 / 분해 / 스킬유형 매핑 | → [weapon.md § 9](weapon.md) |
| 퀘스트 | 거점 개수·트리거 수치·격리 몹 명세 | → [quest.md § 9](quest.md) |
| 능력치 | 등급별 스탯 강도 매트릭스 | → [stat-boost.md § 9](stat-boost.md) |
| 언락 | 조건 및 스킬 목록 | 기획 |
| 네트워크 | RPC / 상태 동기화 범위 세부 | 대화로 결정 |

## 11. 비주얼 / 사운드 / 맵

- **아트 스타일:** 탑다운 2D 픽셀 아트. 어두운 배경 + 청록/보라/분홍 발광 이펙트. 꿈같은 분위기.
- **카메라:** 정확한 탑다운, 플레이어 추적, 회전 불가.
- **후처리:** Bloom 강조.
- **UI:** 미니멀. HUD에 체력/레벨/경험치 바. 스킬 선택은 카드 3장 형태. UI 프레임 시스템은 [systems/ui-frame.md](../systems/ui-frame.md).
- **사운드:** Unity Asset Store 구매.
- **맵:** 초기 1개. 폐쇄형, 경계는 안개 연출. 적은 경계 안개에서 스폰.

## 12. 등급 체계 통합

게임 전반에 동일한 4단계 등급 체계:

| 등급 | 적용 대상 | 확률 |
|---|---|---|
| 일반 | 혼돈 스킬, 능력치 | 가장 높음 |
| 희귀 | 혼돈 스킬, 능력치 | 보통 |
| 영웅 | 혼돈 스킬, 능력치 | 낮음 |
| 전설 | 혼돈 스킬, 능력치 | 매우 낮음 |

선택지 UI에서 색상으로 등급 구분.

## 13. 관련 문서

- [rules.md](rules.md) — 게임 규칙 (6슬롯, 사망/부활, 경험치 흡수, 호스트 이탈)
- [flow-design.md](flow-design.md) — 화면 전환·UI·네트워크 이벤트 플로우
- [flow-diagram.mermaid](flow-diagram.mermaid) — 전체 플로우 시각화
- [skills/INDEX.md](skills/INDEX.md) — 스킬 24종 인덱스
- [enemies/INDEX.md](enemies/INDEX.md) — 적/보스 인덱스
- [../systems/](../systems/) — 구현 명세 (데미지 공식, 네트워크, TriggerEffect)
- [../architecture/implementation-roadmap.md](../architecture/implementation-roadmap.md) — 구현 진행 계획
