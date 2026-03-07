# Architecture Diagram

```mermaid
graph TB
    subgraph "Feature/&lt;Name&gt;"
        Presentation["Presentation<br/>View, Presenter, InputHandler"]
        Infrastructure["Infrastructure<br/>PhotonAdapter, Repository"]
        Application["Application<br/>UseCases, Port I/F, DTO"]
        Domain["Domain<br/>Entities, ValueObjects, Rules<br/>(No Unity, No Photon, No IO)"]
    end

    Shared["Shared<br/>Kernel, Cross-feature Utilities"]

    Presentation -->|depends on| Application
    Infrastructure -->|implements ports| Application
    Application -->|depends on| Domain

    Presentation -.->|can use| Shared
    Infrastructure -.->|can use| Shared
    Application -.->|can use| Shared

    style Domain fill:#4a9,stroke:#333,color:#fff
    style Application fill:#49a,stroke:#333,color:#fff
    style Presentation fill:#a94,stroke:#333,color:#fff
    style Infrastructure fill:#a49,stroke:#333,color:#fff
    style Shared fill:#888,stroke:#333,color:#fff
```

## asmdef 제약

```mermaid
graph LR
    P[Presentation] -->|"X 직접참조 불가"| D[Domain]
    I[Infrastructure] -->|"X 직접참조 불가"| D

    style P fill:#a94,stroke:#333,color:#fff
    style I fill:#a49,stroke:#333,color:#fff
    style D fill:#4a9,stroke:#333,color:#fff
```

> Presentation / Infrastructure는 Domain을 직접 참조할 수 없음
> 따라서 `LobbyTeam` 같은 enum은 Application DTO로 유지

## 의존성 방향 요약

| From | To | 비고 |
|---|---|---|
| **Presentation** | Application, Shared | Domain 직접 참조 불가 |
| **Infrastructure** | Application, Shared | Domain 직접 참조 불가 |
| **Application** | Domain, Shared | |
| **Domain** | (없음) | 순수 비즈니스 로직만 |
| **Shared** | (없음) | Feature 코드 의존 금지 |
