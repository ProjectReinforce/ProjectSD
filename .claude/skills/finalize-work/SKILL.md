---
name: finalize-work
description: 사용자가 "작업 마무리", "작업 마무리 하자", "마무리 해줘", "마무리 하자", "오늘 작업 끝", "정리하고 커밋", "커밋하고 마무리", "작업 끝내자", "wrap up" 같이 한 단위 작업의 종료를 알리는 발화를 할 때 자동으로 invoke 된다. 변경 범위 스캔 → 관련 docs 업데이트 제안 → ✅ 항목 마이그레이션(roadmap → completed-work) → 한국어 prefix 커밋 메시지 작성/승인 → 스테이징+커밋 → push 확인의 5+1단계 루틴을 순서대로 수행한다. ProjectSD(Unity + Photon PUN 2) 전용.
---

# Finalize Work — 한 단위 작업 종료 루틴

ProjectSD의 커밋·push 루틴을 자동화한다. 사용자가 "작업 마무리"류 발화를 하면 아래 절차를 **순서대로** 수행.

---

## 0. 안전 규칙 (항상 준수)

- `git add -A` / `git add .` **금지**. 반드시 경로 명시 (`Assets/ docs/ .claude/ CLAUDE.md` 등).
- `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/` 는 **절대 건드리지 않음**.
- `.env`, `credentials.json`, `*.keystore`, `*.pem` 같은 민감 파일이 staged에 있으면 **즉시 중단**하고 사용자에게 경고.
- `--no-verify`, `--amend`, `push --force`, `commit --gpg-sign=false` **금지**.
- pre-commit hook 실패 시 원인 수정 후 **새 커밋** 생성 (amend 금지).
- 현재 브랜치가 `publish` (main) 이면 commit·push 모두에 재확인 단계 강화.
- 다른 브랜치에 진행 중인 작업(stash 포함)이 있으면 경고 메시지만 출력 (중단은 X).

---

## 1. 변경 범위 스캔

병렬로 아래 명령 실행:

```bash
git branch --show-current
git status --porcelain
git diff --stat
git log --oneline -5
git stash list
```

결과로 **1줄 요약**을 사용자에게 보고:

> "현재 브랜치 `Hyeon-Woo`, 수정 12 / 신규 4 / 삭제 0. Features/UI/Menu 쪽 중심. 최근 커밋 스타일: 한국어 + `docs:`/`feat:` prefix."

변경 파일을 내부적으로 범주 분류:
- `Assets/Scripts/Features/<Feature>/` → 해당 Feature
- `Assets/Scripts/Shared/` → 공용 매니저/네트워크
- `Assets/Resources/` → 프리팹/ScriptableObject
- `docs/systems/`, `docs/game-design/`, `docs/architecture/` → 이미 문서 작업
- `.claude/`, `CLAUDE.md` → 메타
- 그 외 (`ProjectSettings/`, `Packages/` 등)는 범주 별도 표시

---

## 2. 관련 문서 업데이트 제안 (변경 감지형)

변경된 경로 기반으로 docs 매칭 (확정적 1:다 규칙):

| 변경 경로 | 연관 문서 후보 |
|---|---|
| `Features/UI/Adapter/Menu/`, `Resources/Prefabs/UI/Frame_*` | `docs/systems/waiting-room.md`, `docs/systems/ui-frame.md`, `docs/systems/scene-structure.md` |
| `Features/Skill/` | `docs/systems/skill-executor.md`, `docs/systems/trigger-effects.md`, `docs/game-design/skills/` |
| `Features/Enemy/`, `Features/Boss/` | `docs/game-design/enemies/`, `docs/systems/spawn-rules.md` |
| `Features/Character/`, `Features/Progression/` | `docs/game-design/overview.md`, `docs/game-design/rules.md` |
| `Shared/Managers/NetworkManager.cs`, `Shared/Network/` | `docs/systems/network-sync.md` |
| `Shared/Managers/` (기타) | `docs/systems/managers.md` |
| Phase 진행 상황이 명확히 이동한 경우 | `docs/architecture/implementation-roadmap.md` |

각 후보 문서에 대해:
1. 문서를 읽고 **이번 변경이 이미 반영돼 있는지** 확인
2. 반영되어 있으면 `문서명 — 최신, 스킵` 출력 후 건너뜀
3. 추가/수정이 필요하면 **diff 형태**로 사용자에게 제안:

   > "`docs/systems/waiting-room.md` 에 다음 섹션 추가 제안"
   > ```diff
   > + ### 4.7 비밀번호 Toggle 연결
   > + ...
   > ```
4. 사용자 승인 (y/n) 후 Edit 도구로 적용
5. 거부하면 스킵, 이유를 1줄로 기록 (상태 메모용)

**주의**:
- 문서 변경은 반드시 **읽기 → 제안 → 승인 → 적용** 순서. 무응답 자동 적용 X.
- `implementation-roadmap.md` 업데이트는 Phase 전환/완료 같은 굵직한 이동만. 작은 개선은 언급하지 않음.
- CLAUDE.md 수정은 사용자가 명시적으로 요청할 때만. skill 자체는 CLAUDE.md 건드리지 않음.

---

## 2.5. ✅ 항목 마이그레이션 (운영 룰)

[implementation-roadmap.md](../../../docs/architecture/implementation-roadmap.md) 헤더의 운영 룰 (2026-04-26 도입):

> **R/U/Phase 항목이 ✅ 처리되는 순간 → completed-work.md 로 이동, 본 문서에서는 1줄 요약 + 링크만 남긴다.**

본 단계는 이번 작업에서 R/U/Phase 항목이 새로 ✅ 처리된 경우에만 실행. 일상 커밋·부분 진행은 스킵.

### 트리거 감지

다음 신호 중 **하나라도** 있으면 본 단계 발동:
- 사용자 발화에 "R\d+ 완료", "R\d+ 끝났어", "Phase \d+ 완료", "U\d+ 끝" 등 명시적 완료 언급
- 변경 파일 중 `docs/architecture/implementation-roadmap.md` 가 포함되었고, diff 에서 R/U/Phase 헤더의 `🟡` → `✅` 또는 잔여 체크리스트 항목 `[ ]` → `[x]` 토글이 보임
- 코드 변경이 R 항목의 잔여 체크리스트와 명백히 일치 (예: `PlayerStats.baseCritChance` 추가 + `DealDamageHandler` 치명타 판정 → R9 완료 시그널)
- 신호가 모호하면 사용자에게 한 줄로 묻기: "R9 (치명타) 작업 ✅ 처리하고 마이그레이션 진행할까요?"

신호가 전혀 없으면 본 단계 스킵.

### 마이그레이션 절차

대상 R/U/Phase 항목별로:

1. **completed-work.md 추가:** 항목 본문(제목 + 핵심 결정/구현 메모)을 적절한 카테고리 섹션 말미에 추가
   - **R 항목** → `## 시스템 / 아키텍처` 섹션의 bullet 으로 추가. 형식: `- {짧은 제목} (R{번호}, YYYY-MM-DD) — {1~2줄 핵심 메모}`. 기존 R1/R2/R7/R8 패턴 따름.
   - **U 항목** → `## 메뉴 / UI` 섹션의 bullet
   - **Phase 서브섹션** → 해당 `## Phase N — ...` 섹션의 bullet 추가
   - 완료 일자 명시 (예: `2026-04-26`)

2. **roadmap 압축:** `implementation-roadmap.md` 에서 해당 본문을 **1줄 요약 + completed-work.md 링크** 로 교체. 형식: `### R{번호}. {짧은 제목} ✅ (YYYY-MM-DD) → [completed-work.md](completed-work.md)`

3. **§ 지금 추천 작업 (Top 5) 갱신:** ✅ 처리된 항목이 Top 5 안에 있었으면 큐에서 제거 + "다음 진입 후보" 에서 1개를 Top 5 로 승격. 우선순위는 의존성·블로킹·사용자 임팩트 기준. 갱신 일자도 갱신.

4. **시스템 spec 헤더 갱신 (해당 시):** ✅ 처리된 R 항목이 어떤 시스템의 모든 Phase 를 끝낸 경우, `docs/systems/{id}.md` §1 메타의 "구현 상태" 헤더를 ⬜/🟡 → ✅ 로 갱신. (예: Localization 의 모든 Phase A~D ✅ 시 → `localization.md` 헤더 갱신)

5. **진행 요약 표 갱신:** roadmap 상단 진행 요약 표의 해당 Phase 행 상태 갱신 (Phase 단위 변동 시만)

6. **마지막 업데이트 라인:** roadmap 의 `최종 업데이트:` 라인 갱신 (예: `2026-04-26 (R9 ✅ 마이그레이션)`)

7. **사용자 승인:** 위 6개 변경의 diff 를 한 번에 출력하고 "이대로 마이그레이션할까요? (y/n)" 단일 승인 받기. 승인 후 일괄 Edit 적용.

### 주의

- **자동 마이그레이션 절대 금지.** 반드시 diff 출력 + 사용자 승인.
- **부분 완료**(잔여 체크리스트 中 일부만 ✅)는 마이그레이션 대상 아님 — 본문 그대로 유지하고 해당 체크박스만 토글. § 지금 추천 작업 큐 위치는 유지.
- **마이그레이션 후 §3 커밋 메시지** 의 prefix 는 `docs:` (R/U/Phase 정리만) 또는 `feat:`/`fix:` (코드 작업 + 마이그레이션 동반) 으로. 코드 작업이 주된 의도면 `feat:`/`fix:` 로 두고 본문에 "R{번호} 완료 + roadmap 마이그레이션" 한 줄 명시.
- 같은 작업에서 다수의 R 항목이 동시에 ✅ 처리됐다면 **한 번에 모두 마이그레이션** 후 단일 커밋. 커밋 분리 안 함.
- **spec ↔ roadmap 분리 룰 (docs/README.md):** 시스템 spec 안에 phase 체크리스트 두지 말 것. 발견 시 roadmap 으로 이전 + spec 헤더에 링크.

### 관련

- [implementation-roadmap.md](../../../docs/architecture/implementation-roadmap.md) 헤더 운영 룰 블록
- [completed-work.md](../../../docs/architecture/completed-work.md) 카테고리 구조

---

## 3. 커밋 메시지 초안 작성

### Prefix 선정 규칙
변경 범주 조합에 따라 **하나**의 prefix 결정:

| 상황 | prefix |
|---|---|
| 새 기능/코드 추가 (Features/ 또는 Shared/ 에 신규 파일) | `feat:` |
| 버그 수정만 (문서·리팩터링 제외) | `fix:` |
| 문서 수정만 (docs/ 단독) | `docs:` |
| 리팩터링 (동작 변화 없음) | `refactor:` |
| 빌드/설정/.meta/잡다한 정리 | `chore:` |
| 복합 (기능+문서) | 주된 의도에 맞춰 `feat:` 선택하고 본문에 문서 변경 언급 |

### 형식
```
<prefix> <50자 이내 제목, 한국어>

<선택: 본문 — 주요 변경 3~5줄, 이유/영향 중심>

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

### 예시
최근 커밋 스타일(`docs: 정수/무기/퀘스트/능력치/추가아이템 5종 설계 문서 신규 + SSOT 정리`)을 참고해 비슷한 톤·길이로 작성.

### 사용자 승인
초안을 출력하고 **"이대로 커밋할까요? (수정 요청 가능)"** 한 줄로 묻는다. 수정 요청이 오면 반영 후 재출력. **승인 전까지 `git commit` 호출하지 않는다.**

---

## 4. 스테이징 + 커밋 실행

### 스테이징
사용자가 커밋 메시지를 승인하면:

```bash
# 범주별로 개별 add. 빈 카테고리는 생략.
git add Assets/Scripts/  Assets/Resources/  docs/  .claude/  CLAUDE.md
```

실행 직후 `git status` 로 staged 목록 재확인. 다음 경우 **중단**:
- Staged 목록에 `Library/`, `Temp/`, `UserSettings/`, `obj/`, `Logs/` 경로 포함
- `.env`, `credentials*`, `*.keystore`, `*.pem`, `*.p12` 파일 포함
- `ProjectSettings/ProjectSettings.asset`은 허용이지만 의외 수정이면 사용자에게 확인

### 커밋
HEREDOC으로 한 번에 전달:

```bash
git commit -m "$(cat <<'EOF'
<prefix> <제목>

<본문>

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### 사후 확인
- `git status` 로 working tree가 비었는지 (예: clean) 확인
- `git log --oneline -1` 로 새 커밋 hash 출력
- pre-commit hook이 실패했다면 에러 메시지 분석 → 수정 가능하면 수정 후 **새 커밋** 생성 (amend X)

---

## 5. Push — 반드시 확인 후 실행

### 브랜치 체크
```bash
CURRENT=$(git branch --show-current)
```

- `CURRENT == "publish"` (main 브랜치) → **반드시 강한 재확인**:
  > "⚠️ publish (main) 브랜치에 직접 push 입니다. 정말 진행할까요? (yes 만 허용)"
- 그 외 (예: `Hyeon-Woo`) → 일반 확인:
  > "`Hyeon-Woo` 브랜치에 push 할까요? (y/n)"

### 실행
- 사용자가 승인:
  - 업스트림 설정되어 있으면 `git push`
  - 처음 push 하는 브랜치면 `git push -u origin <CURRENT>`
- 거부: "커밋 완료. push는 나중에 `git push` 로 직접 실행하세요." 안내만

### 주의
- `push --force`, `push --force-with-lease` 금지.
- 거절된 push(원격이 앞서있음)는 `git pull --rebase` 또는 `git fetch && git merge` 를 제안만 하고 자동 실행하지 않는다 (충돌 가능성 때문).

---

## 6. 종료 보고

루틴 종료 후 사용자에게 짧게 요약:

> - 문서 업데이트: `waiting-room.md`, `implementation-roadmap.md` 2건 반영 / 1건 스킵
> - ✅ 마이그레이션: R9 (치명타) → completed-work.md 이전, roadmap 1줄 압축 *(해당 시만 표시)*
> - 커밋: `feat: 치명타 확률·데미지 적용 + R9 마이그레이션`
> - Push: 완료 (`Hyeon-Woo` → origin)

끝. 추가 지시가 없으면 여기서 멈춘다.

---

## 자연어 매칭 참고

다음 발화 예시는 모두 이 skill을 호출해야 한다:
- "작업 마무리 하자"
- "마무리 해줘"
- "오늘 작업 끝났어"
- "정리하고 커밋하자"
- "커밋하고 마무리"
- "wrap up"
- "작업 끝내자"

다음 발화는 **호출하지 않는다** (단순 커밋/문서 요청):
- "커밋만 해줘" (→ 단순 `git commit` 요청)
- "이 파일 문서화해줘" (→ 단순 문서 작성 요청)
- "push 해줘" (→ 단순 push)
