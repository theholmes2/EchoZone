# EchoZone

Unity Netcode for GameObjects를 학습하며 제작하는 서버 권한 기반 멀티플레이 포트폴리오입니다.

로컬 Host/Client 환경에서 플레이어 이동부터 아이템 상호작용, 동시 획득 제어, 소유자 전용 인벤토리 동기화까지 단계적으로 구현하고 있습니다. 단순히 동작하는 기능을 만드는 데 그치지 않고 서버와 클라이언트의 책임, 데이터 흐름, 동시성 문제를 이해하고 검증하는 것을 목표로 합니다.

## 개발 환경

- Unity 6.3 LTS (6000.3.19f1)
- Netcode for GameObjects 2.7.0
- Multiplayer Play Mode 1.3.3
- Multiplayer Services 1.2.0
- Unity Input System

## 현재 구현 기능

- Multiplayer Play Mode 기반 로컬 Host/Client 실행
- 플레이어별 소유권을 구분한 입력 처리
- 서버 권한 기반 플레이어 이동과 `NetworkTransform` 동기화
- 범용 상호작용 후보 감지 및 Interact 입력 처리
- 서버 RPC를 통한 상호작용 대상·거리·조건 재검증
- ScriptableObject 기반 아이템, 아이템 카탈로그, 인벤토리 설정 데이터
- 월드 아이템의 부분 획득, 잔여 수량 동기화 및 네트워크 Despawn
- 동일 아이템 동시 획득 시 중복 지급을 막는 임계 구역 잠금
- 서버 원본 인벤토리와 소유자 전용 클라이언트 인벤토리 동기화

## 아키텍처

### Brick & Glue

게임 규칙은 네트워크나 화면에 의존하지 않는 독립적인 Brick으로 구성하고, 입력·네트워크·생명주기의 연결은 Glue가 담당합니다.

```text
PlayerInputReader ─┐
                  ├─ PlayerInteractionGlue ─ RPC ─ 서버 검증
InteractionSensor ┘                              │
                                                 ▼
                                      InteractableBehaviour
                                                 │
                                                 ▼
                                      ItemPickup / Inventory
```

- `ItemPickup`, `PlayerInventory`: 실제 수량과 슬롯을 처리하는 시뮬레이션 Brick
- `PlayerInteractionSensor`: 상호작용 가능한 후보만 수집하는 감지 Brick
- `PlayerInteractionGlue`: 소유자 입력을 서버 RPC 및 상호작용 실행과 연결
- `NetworkItemPickupState`: 아이템 수량을 네트워크 상태와 연결
- `NetworkPlayerInventoryState`: 서버 인벤토리를 소유 클라이언트의 복제본과 연결

### 데이터 중심 설계

- `ItemData`: 아이템 식별 정보와 월드 프리팹 정의
- `ItemCatalog`: 네트워크로 받은 `ItemId`를 실제 `ItemData`로 복원
- `InventoryConfig`: 최대 슬롯 등 인벤토리 설정을 중앙 관리
- 실행 중 변하는 수량과 슬롯만 네트워크 상태로 관리

새로운 아이템은 코드를 수정하지 않고 `ItemData`와 카탈로그 데이터 추가로 확장할 수 있도록 구성했습니다.

## 서버 권한 아이템 획득 흐름

```text
소유 Client가 Interact 입력
→ ServerRpc 요청
→ 서버가 NetworkObjectId로 대상 조회
→ 요청 플레이어의 센서 범위 및 CanInteract 재검증
→ 잠금 임계 구역에서 TryInteract 실행
→ 서버 PlayerInventory 원본 변경
→ NetworkList에 전송용 슬롯 복사
→ 소유 Client의 PlayerInventory 복제본 갱신
```

서버는 모든 플레이어의 권위 있는 인벤토리 원본을 보유합니다. 각 클라이언트는 `Owner` 읽기 권한으로 자기 플레이어의 인벤토리만 전달받습니다.

사용자 정의 네트워크 슬롯은 `INetworkSerializable`을 구현하여 `ItemId`와 `Quantity`의 직렬화 순서를 명시했습니다. 실제 `ItemData` 에셋을 전송하지 않고 식별자와 현재 수량만 전달합니다.

## 동시성 문제와 해결

두 플레이어가 같은 아이템을 동시에 요청하는 경쟁 상태를 고려했습니다. 같은 아이템의 `CanInteract` 검사와 실제 지급을 하나의 `lock` 임계 구역으로 묶어 요청을 순차 처리합니다.

먼저 들어온 요청이 수량을 변경한 뒤 다음 요청이 최신 서버 상태를 다시 검사하므로, 수량이 부족한 아이템이 두 플레이어에게 중복 지급되지 않습니다. 아이템마다 별도의 잠금 객체를 사용하여 서로 다른 아이템의 상호작용은 독립적으로 처리합니다.

## 테스트 및 검증

- EditMode 테스트에서 두 `Task`의 요청 시점을 동기화해 동시 획득 상황 재현
- 수량 2인 아이템이 한 플레이어에게만 지급되는지 검증
- Host가 일부 획득한 뒤 Client가 남은 수량을 획득하는 흐름 확인
- 수량이 0이 되면 Host와 Client 양쪽에서 아이템이 함께 Despawn되는지 확인
- Client 획득 결과가 서버 원본과 소유 Client 인벤토리에 동일하게 반영되는지 확인
- 다른 플레이어에게 소유자 전용 인벤토리 데이터가 공개되지 않는 권한 구조 적용

## 다음 작업

- 인벤토리 변경 이벤트를 구독하는 UI 및 View 구현
- 상호작용 실패 사유와 사용자 피드백 UI
- 아이템 사용, 버리기 및 월드 재생성
- 재접속과 영구 저장을 고려한 플레이어 데이터 구조 확장

