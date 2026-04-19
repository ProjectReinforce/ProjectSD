# systems/ — 시스템 구현 명세

`game-design/` 이 "무엇을" 을 담는다면, `systems/` 는 **"코드가 반드시 따라야 하는 계약"** 을 담는다. 수식, 직렬화 포맷, 네트워크 프로토콜, 상태머신 등.

## 문서 목록 (인덱스)

| 문서 | 내용 | 상태 |
|---|---|---|
| [skill-executor.md](skill-executor.md) | 발사 모드 4종·applicableStats 필터·FireRecord 계약 | ✅ |
| [trigger-effects.md](trigger-effects.md) | TriggerType × EffectAction 핸들러 11종 레퍼런스 | ✅ |
| [network-sync.md](network-sync.md) | Photon RPC / CustomProperties / 20Hz 전송 규약 (SSOT) | ✅ |
| [ui-frame.md](ui-frame.md) | FrameToast / Frame_PopUp 프로토콜 | ✅ (설계) |
| [managers.md](managers.md) | GameManager/NetworkManager/SpawnManager 등 매니저 레이어 | ✅ |
| [scene-structure.md](scene-structure.md) | MenuScene / GameScene 패널 구조 + DontDestroyOnLoad | ✅ |
| [spawn-rules.md](spawn-rules.md) | 시간/인원별 스폰 테이블 · 수식 · 등장 비율 | ✅ (수치 일부 TBD) |
| [damage-formula.md](damage-formula.md) | 데미지 공식·크리티컬·소프트캡 방어·반사·DoT | 🟡 제안 (밸런싱 검토) |
| [voice-chat.md](voice-chat.md) | Photon Voice 2 통합 가이드 (PTT/Open Mic, UI 후크) | ⬜ 설계만 (미구현) |
| [platform-integration.md](platform-integration.md) | Stove/Steam SDK 추상화 (`IPlatformService`) + 출시 로드맵 | ⬜ 설계만 (미구현) |
| save-format.md | 세이브 데이터 스키마 | ⬜ 필요 시 작성 (platform-integration 에 일부 포함) |

## 문서 특징

- **정확성 > 가독성.** 수식과 상수는 정확히. 반올림 기준, 단위까지 명시.
- **코드와 동기화.** 값/공식을 코드에서 바꾸면 이 문서도 같이 수정. PR 체크리스트에 포함.
- **참조 ScriptableObject 명시.** "이 수식은 `Data/SkillData.asset` 의 `Damage` 필드를 사용" 같은 식.

## 새 시스템 문서 시작

`docs/templates/system-spec.md` 복사.

## Claude 가 이 폴더를 참조하는 때

- 스킬/적의 수치가 맞는지 검증 → 공식 문서 확인
- 네트워크 동기화 변경 시 → `network-sync.md` 프로토콜 위반 여부 확인
- 새 시스템 설계 → 기존 명세 스타일 따라 작성
- `photon-sync-auditor` 서브에이전트는 네트워크 관련 코드 변경 시 본 폴더(특히 `network-sync.md`, `skill-executor.md`) 기준으로 감사
