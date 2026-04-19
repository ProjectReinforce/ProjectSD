---
name: finalize-work
description: 사용자가 "작업 마무리", "작업 마무리 하자", "마무리 해줘", "마무리 하자", "오늘 작업 끝", "정리하고 커밋", "커밋하고 마무리", "작업 끝내자", "wrap up" 같이 한 단위 작업의 종료를 알리는 발화를 할 때 자동으로 invoke 된다. 변경 범위 스캔 → 관련 docs 업데이트 제안 → 한국어 prefix 커밋 메시지 작성/승인 → 스테이징+커밋 → push 확인의 5단계 루틴을 순서대로 수행한다. ProjectSD(Unity + Photon PUN 2) 전용.
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
> - 커밋: `feat: 대기실 월드 공간 + Kick/Start 분리`
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
