# 시스템 명세서: 맵 경계 & 안개

## 1. 메타

| 항목 | 값 |
|---|---|
| 시스템 ID | `map-bounds` |
| 이름 | 맵 경계 & 안개 시스템 |
| 분류 | 게임플레이 / 맵 / 비주얼 |
| 의존 레이어 | Adapter (Scene 컴포넌트), Presentation (안개 비주얼) |
| 구현 상태 | ⬜ 설계만 (맵 사이즈 미확정 — 진행은 [implementation-roadmap.md](../architecture/implementation-roadmap.md) 에 항목 추가 시 연결) |
| 최종 업데이트 | 2026-05-01 |

## 2. 목적

플레이 가능한 맵 영역을 명시하고, 그 외곽을 **스타크래프트형 안개**로 시각·물리 차단한다.

해결하는 문제:
- **무한 맵 방지** — 플레이어가 맵 외곽 무한정으로 이동하는 걸 막고, 게임 강도(적 밀도, 보스 추적, 카메라 구도)를 일정 범위에 가둬 디자인 가능하게 함
- **스폰 영역 정의** — "맵 안 / 맵 밖" 이라는 명확한 기준을 코드(`BossSpawner.mapBoundsCollider`)에 제공
- **연출** — 안개 너머에서 보스/적이 등장해 맵 안으로 진입하는 시각적 긴장감

## 3. 핵심 정책 (결정)

| 항목 | 정책 | 비고 |
|---|---|---|
| **안개 통과 — 플레이어** | ❌ 차단 | 안개에 닿으면 더 못 진행. 물리 벽(Collider2D, isTrigger=false) |
| **안개 통과 — 적 / 보스** | ✅ 자유 통과 | Layer 충돌 매트릭스에서 Enemy/Boss 레이어와 FogWalls 레이어 충돌 OFF |
| **안개 통과 — 투사체 / 드랍** | ✅ 자유 통과 | 게임플레이 흐름 유지를 위해 통과 (필요 시 정책 재검토) |
| **보스 스폰 영역** | 카메라 시야 밖 + **맵 외부**(안개 영역) | `BossSpawner.enforceOutsideMap = true` 시 활성. 맵 콜라이더 bounds 안인 후보는 reject |
| **일반 적 스폰 영역** | 현재: centroid + 카메라 시야 밖. 맵 외부 가드 미적용 | 향후 동일 가드 추가 검토 ([spawn-rules.md](spawn-rules.md) cross-link) |

## 4. 씬 구성 (계획)

```
GameScene/
├── MapBounds (GameObject)
│   └── BoxCollider2D
│       ├── isTrigger = true
│       ├── Layer = "Ignore Raycast" 또는 전용 "MapBounds" 레이어
│       └── 모든 충돌 매트릭스 OFF (참조 전용, 물리 영향 없음)
├── FogWalls (GameObject)
│   ├── Box/Edge/Polygon Collider2D × N
│   │   ├── isTrigger = false
│   │   └── Layer = "FogWalls" — Player 레이어와만 충돌
│   └── (선택) Composite Collider2D 로 합치기
└── FogVisual (GameObject)
    └── 안개 비주얼 (스프라이트 / Shader / Particle — § 6 참조)
```

**MapBounds 와 FogWalls 를 분리하는 이유:**
- MapBounds = "맵 영역 정의" (스폰 가드 / AI 이동 한계 / 미래 미니맵 등 참조 용)
- FogWalls = "물리적 차단" (플레이어 이동 막기)
- 두 책임을 나누면 "맵 가드 영역만 살짝 다르게" 같은 튜닝이 자유로움

## 5. 인터페이스

### BossSpawner

```csharp
[SerializeField] private Collider2D mapBoundsCollider;
[SerializeField] private bool enforceOutsideMap = false;
```

후보 위치 검증:
```
if (enforceOutsideMap && mapBoundsCollider != null
    && mapBoundsCollider.bounds.Contains(candidate))
    continue;  // 맵 안 후보는 reject
```

- `Collider2D.bounds` 는 **AABB(축 정렬 경계 박스)** 만 본다. 콜라이더 모양(Polygon/Edge/Composite)에 무관
- 직사각형 맵: bounds = 맵 영역 그대로 → 정확
- 비대칭/L자 맵: bounds = 외접 사각형 → 보수적 판정 (실제 맵 안 일부도 reject 가능). § 9 제약 참조

### Layer 충돌 매트릭스 (계획)

| Layer | Player | Enemy | Boss | Projectile | FogWalls |
|---|---|---|---|---|---|
| Player | — | ✓ | ✓ | ✓ | **✓ (차단)** |
| Enemy | ✓ | ✓ | — | ✓ | ✗ |
| Boss | ✓ | — | — | ✓ | ✗ |
| Projectile | ✓ | ✓ | ✓ | — | ✗ |
| FogWalls | ✓ | ✗ | ✗ | ✗ | — |

> Project Settings → Physics 2D → Layer Collision Matrix 에서 셋업.

## 6. 안개 비주얼 구현 옵션

| 방법 | 코드/에셋 | 장단점 |
|---|---|---|
| **Tiled SpriteRenderer** | 코드 | 회색·검정 반투명 텍스처 1장을 맵 외곽에 타일링. 가장 간단, 퀄리티는 낮음 |
| **Shader Graph (URP) — 노이즈 + 시간 흐름** | 코드 | 자연스러운 안개. URP 셰이더 그래프로 Perlin 노이즈 + UV 스크롤. 학습/제작 시간 필요 |
| **ParticleSystem** | 코드 | 부드러운 안개 입자. 카메라 따라다니지 않으면 외곽 고정 가능. 파티클 수가 많아지면 비용 ↑ |
| **Asset Store — 2D Fog of War / Smoke 에셋** | 에셋 | 빠른 적용. 라이선스/스타일 확인 필요. 무료 대안 다수 (예: "2D Smoke FX Free") |

**권장 순서:** 우선 Tiled SpriteRenderer 또는 ParticleSystem 으로 코드 자체 제작 → 부족하면 Shader Graph → 그래도 부족하면 에셋 도입. 처음부터 에셋을 쓸 필요는 없음.

## 7. 데이터 출처

- **씬 GameObject** — `MapBounds`, `FogWalls`, `FogVisual` (`GameScene.unity` 에 배치)
- **레이어 정의** — `ProjectSettings/TagManager.asset`
- **충돌 매트릭스** — `ProjectSettings/DynamicsManager.asset` (Physics 2D)
- **밸런싱 수치 (맵 사이즈, 안개 두께)** — 맵 사이즈 확정 시 `Assets/Data/.../*.asset` SO 로 추출 검토. 현재는 인스펙터 직접 셋업

## 8. 네트워크

- **클라이언트 로컬.** 맵 콜라이더·안개 비주얼·FogWalls 는 모든 클라이언트가 GameScene 로딩 시 동일하게 셋업. 네트워크 동기화 불필요
- **보스 스폰 위치** 는 [network-sync.md](network-sync.md) 의 표준 — 호스트가 결정 후 `PhotonNetwork.Instantiate` 로 전파. 위치 검증 가드(맵 외부)도 호스트 단독 실행 후 결과만 동기화

## 9. 알려진 제약 / 트레이드오프

- [x] **bounds.Contains() 는 AABB 만 본다** — 비대칭 맵에선 외접 사각형 기준이라 실제 맵 안 일부 위치도 reject 가능. 직사각형 맵이면 무관
- [x] **Composite/Polygon 콜라이더의 정확한 안/밖 판정 필요 시** — `OverlapPoint` 로 교체 검토. 단 직사각형 맵은 AABB로 충분
- [x] **fallback 은 맵 가드 무시** — 시도 한도(10회) 초과 시 fallback 분기는 맵 가드를 적용하지 않음. 4면이 모두 맵 안인 극단 케이스에서만 발동
- [ ] **일반 적 스폰의 맵 가드 미적용** — `SpawnManager.GetSpawnPosition` 은 현재 맵 외부 가드 없음. 맵 확정 후 동일 정책 추가 필요
- [ ] **멀티에서 플레이어가 안개 너머로 이동 못 함** — centroid 가 맵 가장자리에 가까울 때 카메라 시야가 안개 영역까지 보일 수 있음. 안개 비주얼이 그 시야를 잘 가리는지 디자인 검증 필요

## 10. 기존 코드 참조

- **핵심 구현:** [Assets/Scripts/Features/Boss/Adapter/BossSpawner.cs](../../Assets/Scripts/Features/Boss/Adapter/BossSpawner.cs) — `CalculateSpawnPosition`, `mapBoundsCollider`, `enforceOutsideMap`
- **연관 시스템:** [spawn-rules.md](spawn-rules.md) — 일반 적 스폰 정책 (centroid + 카메라 시야 밖)
- **씬 셋업 가이드:** [scene-structure.md](scene-structure.md) — GameScene GameObject 배치 (MapBounds·FogWalls·FogVisual 추가 예정)

## 11. 변경 이력

- 2026-05-01: 초안. BossSpawner 의 맵 외부 가드 hook(`mapBoundsCollider` + `enforceOutsideMap`) 추가와 함께 작성.
