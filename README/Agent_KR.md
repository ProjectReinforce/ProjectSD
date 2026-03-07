# AGENT 규칙 문서 설명 (한국어 안내)

이 문서는 코드 생성 Agent가 참고하는 **AGENTS.md 및 /agent 폴더 규칙 문서들에 어떤 내용이 들어있는지 설명하는 안내 문서**이다.
실제 Agent는 영어 규칙 문서를 따르며, 이 문서는 사람이 이해하기 쉽게 한국어로 요약한 것이다.

---

# 전체 구조

프로젝트에는 다음과 같은 Agent 규칙 문서가 존재한다.

```
AGENTS.md

/agent
  architecture.md
  dependency_rules.md
  layer_rules.md
  feature_rules.md
  naming_rules.md
  anti_patterns.md
```

각 문서는 **코드 생성 Agent가 따라야 할 아키텍처 규칙**을 정의한다.

---

# 1. AGENTS.md

역할
Agent가 어떤 규칙 문서를 따라야 하는지 알려주는 **루트 규칙 파일**이다.

포함 내용

* Agent가 따라야 할 문서 목록
* 규칙 충돌 시 우선순위
* 코드 생성 시 기본 행동 지침

핵심 개념

* 코드 생성 시 항상 아키텍처 규칙을 먼저 따른다
* 가능한 한 현재 Feature 내부에서 코드를 추가한다
* Shared 추상화를 쉽게 만들지 않는다
* Domain에 비즈니스 로직을 유지한다

---

# 2. architecture.md

역할
프로젝트의 **전체 아키텍처 구조**를 정의한다.

핵심 구조

```
Features/
  FeatureName/
    Domain/
    Application/
    Presentation/
    Infrastructure/

Shared/
```

핵심 규칙

* 프로젝트는 **Feature 기반 Clean Architecture**를 따른다
* 각 Feature는 독립적인 구조를 가진다
* Feature 내부에 Domain / Application / Presentation / Infrastructure가 존재한다
* Shared에는 공통 코드만 존재해야 한다

중요 규칙

* Feature 전용 코드를 Shared로 옮기지 않는다

---

# 3. dependency_rules.md

역할
레이어 간 **의존성 방향 규칙**을 정의한다.

허용되는 의존 방향

```
Application → Domain
Application → Shared
Presentation → Application
Presentation → Domain
Presentation → Shared
Infrastructure → Application
Infrastructure → Domain
Infrastructure → Shared
```

금지되는 의존

* Domain → Application
* Domain → Presentation
* Domain → Infrastructure

특히 금지되는 것

* Domain에서 Unity API 사용
* Domain에서 Photon API 사용
* Domain에서 DB 접근

핵심 목표

**Domain을 완전히 독립적인 순수 로직으로 유지**

---

# 4. layer_rules.md

역할
각 레이어가 어떤 책임을 가지는지 정의한다.

## Domain

내용

* 엔티티
* 값 객체
* 도메인 규칙
* 비즈니스 로직

금지

* Unity API
* Photon API
* 파일 IO
* 데이터베이스
* UI 로직

---

## Application

내용

* UseCase
* Repository 인터페이스
* Network Port 인터페이스
* Output Port 인터페이스

규칙

* UseCase는 orchestration 역할만 한다
* 비즈니스 규칙은 Domain에 있어야 한다

---

## Presentation

내용

* EntryPoint
* Presenter
* View
* InputHandler

규칙

* UI 처리 담당
* 비즈니스 로직 금지

---

## Infrastructure

내용

* Photon 어댑터
* Persistence 어댑터
* 외부 SDK 통합

규칙

* Application Port를 구현한다
* 비즈니스 로직을 포함하지 않는다

---

# 5. feature_rules.md

역할
Feature 단위 아키텍처 규칙 정의

핵심 개념

각 Feature는 다음을 소유한다

* Domain
* Application
* Presentation
* Infrastructure

규칙

* Feature끼리 직접 의존하지 않는다
* Feature 간 통신은 Port 또는 이벤트를 통해 이루어진다

예시

Lobby Feature 내부에 Room이 포함될 수 있다.

단

Room이 독립 lifecycle을 가지면
Room Feature로 분리할 수 있다.

---

# 6. naming_rules.md

역할
코드 네이밍 규칙 정의

## Entity

접미사 없음

예

```
Lobby
Room
RoomMember
```

---

## UseCase

```
CreateRoomUseCase
JoinRoomUseCase
LeaveRoomUseCase
ChangeTeamUseCase
SetReadyUseCase
```

---

## Port

인터페이스는 Feature context 포함

```
ILobbyRepository
ILobbyNetworkPort
ILobbyOutputPort
```

---

## Adapter

```
LobbyPhotonAdapter
ClockAdapter
```

---

## UI

Presenter

```
LobbyPresenter
```

View

```
LobbyView
RoomListView
RoomDetailView
```

---

# 7. anti_patterns.md

역할
아키텍처 붕괴를 막기 위한 **금지 규칙 목록**

절대 하면 안 되는 것

* Presenter에 비즈니스 로직 넣기
* Domain에서 네트워크 처리
* Domain에서 Unity API 사용
* Feature 전용 코드를 Shared에 넣기
* 실제 중복이 없는데 Generic 추상화 만들기
* EntryPoint를 God Class로 만들기
* 하나의 Port에 여러 책임 넣기
* 정의되지 않은 새로운 레이어 만들기

핵심 원칙

확실하지 않을 때는

**Shared 대신 현재 Feature 내부에 코드를 둔다**

---

# 문서의 목적

이 규칙 문서들의 목적은 다음과 같다.

1. 코드 생성 Agent가 **아키텍처를 깨지 않도록 하기**
2. Feature 단위 구조 유지
3. Domain 로직 보호
4. Shared 남용 방지
5. 레이어 책임 분리 유지

---

# 가장 중요한 규칙

아키텍처 붕괴를 막는 핵심 규칙

```
Never move feature-specific code into Shared.
```

Feature 전용 코드는 반드시 Feature 내부에 유지한다.

---

# 요약

이 규칙 문서들은 다음을 보장하기 위한 것이다.

* Feature 중심 구조 유지
* Clean Architecture 의존 방향 유지
* Domain 로직 보호
* Shared 남용 방지
* Agent 코드 생성 안정성 확보


