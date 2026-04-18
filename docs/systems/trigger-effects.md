# TriggerEffect 시스템 — 이벤트-액션 매핑 레퍼런스

스킬 발사/적중/처치 등 이벤트 발생 시 `FireTrigger(TriggerType, TriggerContext)` 가 호출되고, 매칭되는 모든 효과의 핸들러가 실행된다.

> Executor 패턴·발사 모드는 [skill-executor.md](skill-executor.md). 개별 스킬이 어떤 Trigger → Action 조합을 쓰는지는 [../game-design/skills/](../game-design/skills/) 각 파일의 "TriggerEffect 매핑" 섹션.

## 1. 구조 개요

```
SkillTriggerSystem
  ├─ baseEffects[]     ← SO (SkillData.triggerEffects) 정의
  └─ runtimeEffects[]  ← 정수/무기/혼돈 스킬이 런타임 추가
      각 항목 { TriggerType, EffectActionType, EffectParams }
```

- 이벤트 발생 시 `SkillTriggerSystem.FireTrigger(triggerType, context)` 호출.
- 매칭되는 모든 효과의 `IEffectActionHandler` 가 실행된다.
- 코드: `Assets/Scripts/Adapter/Skill/TriggerEffects/SkillTriggerSystem.cs`, `EffectActionRegistry.cs`, `Handlers/*.cs`

## 2. TriggerType (발동 시점)

| TriggerType | 발동 시점 | TriggerContext 주요 필드 |
|---|---|---|
| `OnFire` | 스킬 발사/시전 시 | position(플레이어), direction(발사 방향), owner |
| `OnHit` | 적에게 적중 시 | position(적중 위치), target(맞은 적), damage(입힌 데미지), owner |
| `OnKill` | 적 처치 시 | position(처치 위치), target(죽은 적), damage(마지막 데미지), owner |
| `OnExpire` | 투사체/장판 소멸 시 | position(소멸 위치), damage, owner |
| `OnInterval` | 주기적 발동 | position(플레이어), owner |
| `OnPlayerHit` | 플레이어 피격 시 | position(플레이어), damage(받은 데미지) |

## 3. EffectActionType 상세

### 3.1 `DealDamage` — 추가 데미지

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 데미지 | 15 |
| secondary | 범위 (0이면 단일 대상) | 2.0 |

단일 또는 범위 내 적에게 추가 데미지. `(15, 0, 0)` 단일 15 / `(10, 2.0, 0)` 반경 2.0 내 10.

### 3.2 `Explode` — 범위 폭발

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 폭발 반경 | 1.5 |
| secondary | 데미지 배율 (1.0 = context.damage 100%) | 1.0 |

`context.position` 중심. 트리거 원인 적은 제외. `(1.5, 1.0, 0)` → 반경 1.5, 스킬 데미지 100%.

### 3.3 `Chain` — 주변 적 전이 (즉시 데미지)

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 체인 횟수 | 3 |
| secondary | 탐색 반경 (기본 0.65) | 5.0 |

적중한 적 주변에서 가장 가까운 적을 찾아 **즉시 데미지**. 체인마다 **80% 감쇄**. 이미 맞은 적 제외. **투사체 비행 없음.**

> ⚠️ 진화형 매직 미사일(체인 미사일)의 **투사체가 다음 적으로 날아가는 동작**은 이 핸들러가 아니라 [skill-executor.md § 5 체인 비행](skill-executor.md) 을 사용한다.

### 3.4 `ApplyDoT` — 지속 피해

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 틱당 데미지 | 5 |
| secondary | 지속시간 (초) | 3.0 |
| tertiary | 틱 간격 (초, 0이면 기본 0.5) | 0.5 |

대상에 `DoTEffect` 컴포넌트 부착. 중복 시 갱신. **적 개체에 부착되므로 이동해도 지속.**

> ⚠️ `AreaZone` 틱 데미지와 구분: `AreaZone` 은 범위 안에서만, `ApplyDoT` 은 적 개체에 부착.

### 3.5 `ApplySlow` — 슬로우 부여

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 슬로우 배율 (0.5 = 50% 감속) | 0.5 |
| secondary | 지속시간 (초) | 2.0 |

호스트에서만 처리.

### 3.6 `Pull` — 끌어당김 (즉시 1회)

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 끌어당김 반경 | 3.0 |
| secondary | 끌어당김 힘 | 5.0 |

`context.position` 방향으로 범위 내 적을 즉시 이동. 선형 감쇄.

> ⚠️ `BoomerangTrajectory.hasPullOnReturn`(비행 중 매 프레임 지속 흡인) 과 구분. 이 핸들러는 트리거 1회 발동당 1회.

### 3.7 `SpawnProjectile` — 추가 투사체 생성

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 생성 개수 | 2 |
| secondary | 데미지 배율 (1.0 = 100%) | 0.5 |

`context.position` 에서 균등 방향으로 서브 투사체 생성.

> ⚠️ **현재 `SpawnProjectileHandler.SetProjectilePrefab()` 으로 코드에서 수동 설정**해야 함. `SkillData.subProjectilePrefab` 필드 추가 예정.
> 프리팹 미설정 시 fallback으로 방향별 즉시 데미지 처리.

### 3.8 `ApplyVulnerability` — 받는 피해 증가

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 받는 피해 배율 (1.3 = +30%) | 1.3 |
| secondary | 지속시간 (초) | 5.0 |

대상에 `DebuffMark` 부착. 기존 마크는 갱신.

### 3.9 `HealSelf` — 자기 회복

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 고정 회복량 | 5 |
| secondary | 데미지 비율 회복 (0.1 = 10%) | 0.1 |

최종 회복 = `primary + (context.damage × secondary)`. `context.owner.PlayerHealth.Heal()` 호출.

### 3.10 `Execute` — 즉사 처형

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | HP 비율 임계값 (0.05 = 5%) | 0.05 |
| secondary | 범위 (0이면 단일 대상) | 0 |

대상 HP가 (최대 HP × 임계값) 이하면 즉사. **보스 제외.**

### 3.11 `Refire` — 스킬 재발동 (미구현)

| 파라미터 | 용도 | 예시 |
|---|---|---|
| primary | 재발동 딜레이 (0이면 즉시) | 1.0 |

`IFireRecorder` 에서 마지막 발사 기록을 읽어 재현. **메아리(#17)** 스킬용.

> ⚠️ `IFireRecorder` 구현체 작성 시 함께 구현 예정. [skill-executor.md § 4](skill-executor.md) 참조.

## 4. TriggerEffect와 다른 시스템의 차이

혼동하기 쉬운 유사 메커니즘 요약:

| 동작 | TriggerEffect 핸들러 | 별도 시스템 | 차이 |
|---|---|---|---|
| 끌어당김 | `PullHandler` (1회성) | `BoomerangTrajectory.hasPullOnReturn` (비행 중 지속) | 발동 횟수 |
| 데미지 전이 | `ChainHandler` (즉시 데미지) | Projectile 체인 비행 (투사체 물리 이동) | 비행 여부 |
| 반복 데미지 | `ApplyDoTHandler` (적 개체 부착, 이동 지속) | `AreaZone` 틱 (장판 범위 안에서만) | 범위 귀속 |

## 5. 런타임 효과 추가 (정수 / 무기 / 혼돈)

```csharp
// 정수 장착 시
triggerSystem.AddRuntimeEffect("essence_fire", new SkillTriggerEffect(
    TriggerType.OnHit,
    EffectActionType.ApplyDoT,
    new EffectParams(3, 4.0f, 1.0f)
));

// 정수 해제 시
triggerSystem.RemoveRuntimeEffects("essence_fire");

// 모든 정수 효과 해제
triggerSystem.RemoveByPrefix("essence_");
```

### source 명명 규칙

| Prefix | 의미 |
|---|---|
| `essence_{이름}` | 정수 속성 (얼음/불/번개 + 조합) |
| `weapon_{이름}` | 무기 부가 효과 |
| `chaos_{이름}` | 혼돈 스킬 |
| `buff_{이름}` | 일시 버프 |

## 6. 네트워크

- 런타임 효과 추가/해제는 호스트가 판정하여 각 클라이언트에 RPC 전파.
- 상세 규약은 [network-sync.md](network-sync.md).

## 7. 핸들러 파일 참조

`Assets/Scripts/Adapter/Skill/TriggerEffects/Handlers/`:
- `ApplyDoTHandler.cs`
- `ApplySlowHandler.cs`
- `ApplyVulnerabilityHandler.cs`
- `ChainHandler.cs`
- `DealDamageHandler.cs`
- `ExecuteHandler.cs`
- `ExplodeHandler.cs`
- `HealSelfHandler.cs`
- `PullHandler.cs`
- `SpawnProjectileHandler.cs`
- (`RefireHandler` — 미구현)

## 8. 알려진 제약

- [ ] `Refire` 핸들러 미구현. `IFireRecorder` 와 함께 메아리 구현 시점에 작성.
- [ ] `SpawnProjectile` 서브 프리팹은 코드 수동 설정 상태. `SkillData.subProjectilePrefab` 필드 추가 필요.
- [ ] Chain 감쇄율 80% 는 현재 하드코딩 — SO 노출 검토.
