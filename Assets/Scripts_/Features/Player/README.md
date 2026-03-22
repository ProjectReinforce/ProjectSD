# Player Feature

플레이어 캐릭터의 스폰, 이동, 점프 및 네트워크 위치 동기화를 담당한다.

## 책임

- 플레이어 스폰 (Photon Instantiate)
- 로컬 입력 → 이동/점프 처리
- 위치/회전 네트워크 동기화
- 로컬/원격 플레이어 분기 초기화

## 이벤트 흐름

### 로컬 플레이어

```
PlayerInputHandler (InputSystem)
  → PlayerUseCases.Move(player, input, deltaTime)
    → Player.CalculateMovement (도메인 물리)
    → IPlayerMotorPort.Move (CharacterController 이동)
    → Player.ApplyMovement (상태 갱신)

  → PlayerUseCases.Jump(player)
    → Player.TryJump (지면 판정)
    → IPlayerNetworkCommandPort.SendJump (RPC)
```

### 원격 플레이어

```
PlayerNetworkAdapter.OnPhotonSerializeView (위치/회전 수신)
  → Update()에서 Lerp 보간

PlayerNetworkAdapter.RPC_Jump (점프 수신)
  → PlayerNetworkEventHandler → PlayerJumpedEvent 발행
```

## 네트워크 동기화

| 데이터 | 방식 | 용도 |
|---|---|---|
| 위치, 회전 | `OnPhotonSerializeView` (연속 데이터) | 매 프레임 보간 |
| 점프 | `RPC` (이산 이벤트) | 점프 모션 트리거 |

`PlayerNetworkAdapter`는 `IPunObservable` + `MonoBehaviourPun`을 구현하며,
`IPlayerNetworkCommandPort`(송신)와 `IPlayerNetworkCallbackPort`(수신)을 모두 담당한다.

## Bootstrap 구조

두 컴포넌트가 협력한다:

- **GameSceneBootstrap** (씬 오브젝트): `PhotonNetwork.Instantiate`로 PlayerCharacter 프리팹 생성, 카메라를 플레이어에 부착.
- **PlayerSetup** (PlayerCharacter 프리팹): 스폰 후 `IsMine` 분기:
  - 로컬: EventBus + PlayerNetworkEventHandler + PlayerUseCases + InputHandler + View 초기화
  - 원격: Input/Motor 비활성화, View만 초기화

## 도메인 물리

`Player` 엔티티가 이동 계산을 도메인 레벨에서 수행한다:
- `MovementRule`: 속도 선택 (걷기/달리기), 수평 이동 델타, 중력 적용
- `PlayerSpec`: walkSpeed, sprintMultiplier, jumpForce, gravity
- 실제 CharacterController 이동은 `PlayerMotorAdapter`(Infrastructure)가 담당

## 피처 간 의존

- **Skill Feature가 이 프리팹을 사용**: `SkillSetup`과 `SkillNetworkAdapter`가 같은 PlayerCharacter 프리팹에 부착됨
- **Shared**: EventBus, Float3, DomainEntityId, IClockPort
