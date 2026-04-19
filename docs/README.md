# docs/ — ProjectSD 설계 문서 보관소

Claude 는 **필요한 순간에만** `docs/` 를 읽는다. 모든 작업이 이곳을 거치는 건 아니지만, 설계 의사결정과 도메인 지식의 **단일 출처(SSOT)** 역할을 한다.

## 폴더 지도

```
docs/
├── README.md                          ← (본 파일) 폴더 지도·SSOT 규칙
├── architecture/
│   ├── overview.md                    ← 레이어 구조·의존성 규칙
│   └── implementation-roadmap.md      ← Phase별 구현 진행도
├── game-design/
│   ├── overview.md                    ← 게임 전체 컨셉·코어 루프 (GDD)
│   ├── flow-design.md                 ← 화면 전환·UI 플로우
│   ├── flow-diagram.mermaid           ← 전체 플로우 시각화
│   ├── rules.md                       ← 6슬롯·사망부활·경험치 등 규칙
│   ├── essence.md                     ← 정수 시스템 (속성 부여, 엘리트 드랍)
│   ├── weapon.md                      ← 무기 시스템 (LoL 아이템식, 조합)
│   ├── quest.md                       ← 퀘스트 시스템 (4유형, 격리 메커니즘)
│   ├── stat-boost.md                  ← 능력치 시스템 (만렙 후 / 퀘스트 보상)
│   ├── items.md                       ← 추가 아이템 (자석/물약/경험치 오브)
│   ├── skills/
│   │   ├── INDEX.md                   ← 스킬 24종 인덱스
│   │   ├── {skill-id}.md              ← 스킬 1개당 1 파일 (24개)
│   │   └── chaos/                     ← 혼돈 스킬 19종
│   └── enemies/
│       ├── INDEX.md                   ← 적·보스 인덱스
│       └── {basic|fast|tank|swarm|boss}.md
├── systems/
│   ├── README.md                      ← systems 문서 인덱스
│   ├── skill-executor.md              ← 발사 모드·Stat 필터·FireRecord
│   ├── trigger-effects.md             ← TriggerType × EffectAction
│   ├── network-sync.md                ← Photon 동기화 규약 (SSOT)
│   ├── ui-frame.md                    ← FrameToast / Frame_PopUp
│   ├── managers.md                    ← 매니저 싱글톤 레이어
│   ├── scene-structure.md             ← MenuScene / GameScene
│   └── spawn-rules.md                 ← 스폰 타이밍·난이도 곡선
└── templates/
    ├── skill-spec.md
    ├── enemy-spec.md
    └── system-spec.md
```

## 폴더 용도

| 폴더 | 무엇을 두나 |
|---|---|
| `architecture/` | 시스템 전체 아키텍처, 레이어 규칙, 의존성 다이어그램, Phase별 로드맵 |
| `game-design/` | GDD. 게임 컨셉, 규칙, 스킬/적/보스 설계 (24+ 스킬, 5 적 파일) |
| `systems/` | 구현 중심 명세. 수식, 직렬화 포맷, 네트워크 프로토콜, 상태머신 |
| `templates/` | 신규 문서용 양식 |

## SSOT 규칙 (중복 방지)

같은 정보를 여러 문서에 적지 않는다. 반드시 **한 곳**이 SSOT:

| 주제 | SSOT | 다른 문서의 처리 |
|---|---|---|
| 스킬 선택 UI 플로우 | `game-design/flow-design.md` | 링크만 |
| 보스 혼돈 스킬 효과 | `game-design/enemies/boss.md` | 링크만 |
| 정수 / 무기 / 퀘스트 / 능력치 / 추가 아이템 | `game-design/{essence|weapon|quest|stat-boost|items}.md` | overview/rules 는 한 줄+링크 |
| Phase별 진행 계획·로드맵 | `architecture/implementation-roadmap.md` | 링크만 |
| 발사 모드·Stat 필터 | `systems/skill-executor.md` | 링크만 |
| TriggerEffect 핸들러 | `systems/trigger-effects.md` | 링크만 |
| 네트워크 RPC·동기화 규약 | `systems/network-sync.md` | "이 규약을 따른다" 만 |
| 스폰 테이블·인원 스케일링 | `systems/spawn-rules.md` + `game-design/rules.md` | 수식은 systems, 규칙은 game-design |
| 씬 구조·패널 | `systems/scene-structure.md` | 링크만 |

## 문서 작성 원칙

1. **파일당 주제 하나.** `skills.md` 같은 통합 파일보다 `skills/longsword.md` 같이 나눌 것.
2. **제목은 명확하게.** 파일명을 보고 "무슨 내용인지" 알 수 있어야 Grep 으로 빠르게 찾는다.
3. **변경 이유를 남겨라.** 숫자를 바꿨다면 "왜 이 값인가"를 한 줄이라도. 설계 문서는 "현재 상태"가 아니라 "결정의 기록".
4. **템플릿 사용.** 새 스킬/적/시스템 문서는 `templates/` 양식 복사로 시작. 양식이 맞지 않으면 템플릿 자체를 업데이트.
5. **`> _TBD_` 플레이스홀더.** 완성되지 않은 섹션은 명시적으로 `> _TBD_` 로 표기. 독자가 "여긴 아직" 을 즉시 파악 가능.

## Claude 에게 문서 반영 요청하는 법

새 설계 문서를 추가했다면:

> `docs/game-design/skills/longsword.md` 를 수정했어. 읽고 CLAUDE.md 용어집이랑 `skill-architect` 에이전트에 반영해줘.

Claude 는 문서를 Read/Grep 으로 훑고, CLAUDE.md 와 관련 서브에이전트를 업데이트한 뒤 diff 를 보여준다.

## Claude 가 문서를 참조하는 순서

1. `CLAUDE.md` 의 "참조 문서" 섹션 → 어떤 문서가 있는지 파악
2. 작업 유형별 템플릿 (`templates/`) → 양식 확인
3. 해당 도메인 문서 (`game-design/`, `systems/`) → 구체 규칙
4. `architecture/` → 레이어·의존성 관련 질문이 있을 때만

작업별 진입점:
- **새 스킬 추가** → `templates/skill-spec.md` + 기존 `game-design/skills/*.md` 하나를 참고해서 `skills/{new-id}.md` 작성
- **적/보스 추가** → `templates/enemy-spec.md` + `game-design/enemies/INDEX.md`
- **새 시스템 설계** → `templates/system-spec.md` + `systems/README.md`
- **아키텍처 관련** → `architecture/overview.md`
- **구현 일정 점검** → `architecture/implementation-roadmap.md`
