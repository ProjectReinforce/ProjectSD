# game-design/ — 게임 디자인 문서 (GDD)

"**게임이 어떻게 작동해야 하는가**" 에 대한 설계. 코드가 아니라 규칙·경험·수치에 관한 문서.

## 구조

```
game-design/
├── overview.md              ← 게임 전체 컨셉·코어 루프 (GDD)
├── flow-design.md           ← 화면 전환·UI·네트워크 이벤트 플로우
├── flow-diagram.mermaid     ← 전체 플로우 시각화
├── rules.md                 ← 6슬롯·사망부활·경험치·인원 스케일링 등 규칙
├── skills/
│   ├── INDEX.md             ← 스킬 24종 + 패시브 19 + 진화 10 인덱스
│   ├── shuriken.md / magic-missile.md / longsword.md / ...  (24개)
│   └── chaos/
│       ├── README.md
│       └── glass-cannon.md / chain-explosion.md / ...  (19개)
└── enemies/
    ├── INDEX.md             ← 적·보스 인덱스
    └── basic.md / fast.md / tank.md / swarm.md / boss.md
```

## 탐색 진입점

| 무엇을 하고 싶은가 | 어디부터 |
|---|---|
| 게임 전반을 이해 | [overview.md](overview.md) |
| 화면 전환 / UI 흐름 파악 | [flow-design.md](flow-design.md) or [flow-diagram.mermaid](flow-diagram.mermaid) |
| 특정 스킬의 설계 | [skills/INDEX.md](skills/INDEX.md) → 해당 파일 |
| 적/보스 설계 | [enemies/INDEX.md](enemies/INDEX.md) |
| 게임 규칙 (슬롯, 사망, 혼돈 등) | [rules.md](rules.md) |

## 새 문서 시작

`docs/templates/` 의 양식 복사:
- 스킬 1개 → `templates/skill-spec.md`
- 적/보스 1개 → `templates/enemy-spec.md`
- 시스템 → `templates/system-spec.md` (but 시스템 문서는 `docs/systems/` 에 둔다)

## Claude 가 이 폴더를 참조하는 때

- 새 스킬/적/진화 분기 추가 요청 시 → 기존 비슷한 문서를 Grep 으로 찾아 일관성 확인.
- "스킬 X의 공격력이 왜 Y인가" 같은 질문 → 해당 스킬 파일의 "수치" / "오픈 이슈" 섹션.
- 스킬 보일러플레이트 생성 시 → 문서의 수치·계약·TriggerEffect 매핑 사용.

## SSOT 주의

- 시스템 구현 규약(발사 모드, TriggerEffect, 네트워크, 스폰 수식)은 **`../systems/`** 에 있다. 스킬·적 문서는 "이 규약을 따른다" 라고만 링크한다.
- 구현 Phase 로드맵은 **`../architecture/implementation-roadmap.md`**. 본 폴더에는 일정을 적지 않는다.
