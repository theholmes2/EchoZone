# 경찰 검사·월드 복원·회수·건물 정보창 작업 기록

2026-09-29. 구현 반영 및 로컬 검증 완료 범위와 미검증 범위를 구분한다. 실제 Relay 호스트 교체 통합 테스트 완료를 뜻하지 않는다.

## 변경 흐름

- 검사 예정 → `HeistWorldGlue.ReserveInspection` → 한 경찰/한 건물 예약 → `PoliceHeistDutyGlue`가 실제 문 앞으로 이동 → 도착 후 숨김·검사 타이머 → 완료 후 다음 검사 예약. 취소는 짧은 재시도 대기로 돌아간다.
- 마이그레이션 시작 → 복구 장벽 → Cloud 최신 체크포인트/연결 계정 지갑 Revision 확인 → 스키마·고정 ID 검사 → 경찰/펫 생성 → 고정 ID 관계 재연결 → 늦은 플레이어 복원 예약 → 진행 재개.
- 신고는 예약만 한다. 경찰 도착 후 원장 반환·감사비 → 완료 PetId 기록 → Cloud 체크포인트 저장 확인 → 펫 디스폰. 저장 실패 시 상호작용 불가 상태로 재시도한다.
- 기존 HUD ManualUpdate → 별도 건물 Raycast → 복제된 건물/펫 상태 읽기 → 로컬 정보창. 조준 레이는 변경하지 않았다.

## 핵심 파일과 진입점

경로는 프로젝트 루트 기준. 줄 번호는 이 보고서 작성 시점이다.

### 추가 파일

| 파일 | 핵심 위치 / 역할 |
|---|---|
| Assets/Script/Enemy/PoliceDestinationBrick.cs | 7: 고정 순서 목적지 후보와 점유 원장 |
| Assets/Script/Online/Migration/SessionWorldSnapshot.cs | 8: 확장 월드 DTO, ActorRecord 개별 필드 설명 |
| Assets/Script/Online/Migration/SessionWorldSnapshotValidator.cs | 13: 중복 ID·위치·기본 원장 검증 |
| Assets/Script/Online/Migration/SessionWorldMigrationGlue.cs | 37 장벽, 50 Cloud 대조, 94 복원, 136 늦은 플레이어 연결, 180 배치 |
| Assets/Script/Heist/HeistWorldMigration.cs | 19 수집, 35 건물·원장·수배·지갑 복원 |
| Assets/Script/Heist/HeistRetirementCommit.cs | 28: 체크포인트 확인 후 펫 종료 |
| Assets/Script/Pet/PetMigrationState.cs | 9 수집, 18 소유권·도주·회복·장물 복원 |
| Assets/Script/Enemy/PoliceMigrationState.cs | 12 수집, 30 복원, 52 고정 ID 추격 대상 재연결 |
| Assets/Script/Enemy/EnemySpawnMigration.cs | 9 스포너 수집, 18 풀 기반 복원 |
| Assets/Script/Data/BuildingHoverConfig.cs | 5: 탐색 레이어·크기·오프셋·갱신 간격 |
| Assets/Script/Heist/BuildingHoverGlue.cs | 26 별도 레이, 48 표시 조건 |
| Assets/Script/Heist/BuildingHoverView.cs | 20: Canvas 안쪽 위치 제한 |
| Assets/Tests/Editor/ExtendedMigrationTests.cs | 스키마 호환·원장·Revision·중복 복원 테스트 |

### 기존 파일 확장

- `HeistWorldGlue.cs:144` 검사 예약/목적지 점유, `:203` 검사 종료, `:269` 실제 회수. 원장 키를 PetId/PlayerId로 전환. 시작 펫 이력과 종료 펫 이력 유지.
- `PoliceHeistDutyGlue.cs`: 검사 우선 출동, 실행 속도, 도착 후 타이머, 정체 취소, 회수 중복 예약, 수색 목적지 분산.
- `PoliceEnemyBrainGlue.cs:146` 풀 재사용 시 이전 복원 대상 초기화. 순찰 시작 인덱스 분산, 회피 설정, 목적지 예약·재탐색 제한.
- `EnemySpawnManager.cs`: 현재 경찰과 겹치는 스폰 후보 배제.
- `EnemyRuntimeUpdateGlue.cs:38`: 복구 장벽; 늦은 플레이어 적용과 시작 펫 지급 재시도. 씬 월드가 플레이어보다 늦게 스폰되어도 펫 지급 누락 방지.
- `PolicePatrolBrick.cs`, `PolicePerceptionBrick.cs`, `PoliceDeadEventGlue.cs`: 순찰 대기·발견 상태·시체 반환 타이머 복원.
- `PetStateGlue.cs`: 고정 PetId, 인증 소유자, 재접속 대기와 실제 주인 사망 구분. 종료 펫 상호작용 차단.
- `PlayerPetGlue.cs:29`: 계정별 시작 펫 1회 지급.
- `PetHeistGlue.cs`, `HeistBuildingState.cs`: 클라이언트에 작업 건물 ID, 출동/검사 종료 시각 제공.
- `HeistLedgerBrick.cs`, `PoliceInvestigationBrick.cs`: 고정 키 JSON과 상대 시간 복원.
- `WalletSessionBrick.cs`, `PlayerWalletGlue.cs`: 미로드 보상 포함 저장, Cloud Revision/SettlementId 대조, 정산 전 체크포인트 확인.
- `HostMigrationSnapshot.cs`, `HostMigrationSnapshotJsonSerializer.cs`: schema 2와 확장 World; schema 1은 World 없이 읽는다.
- `HostMigrationSnapshotCollector.cs`, `HostMigrationSnapshotApplier.cs`: 확장 데이터 수집/적용, 중복 적용 방지, 미접속 플레이어 보존.
- `WorldItemMigrationSource.cs`: 기존 아이템 ID/수량에 위치·회전 추가.
- `RelaySessionGlue.cs:163`: 새 호스트 Cloud 검증→복원→장벽 해제. 이전 연결 세대의 늦은 오류 무시.
- `HostMigrationCloudCheckpointGlue.cs`, `HostMigrationCloudConfig.cs:14`: 직렬화 크기 상한 524288바이트.
- `WeaponFireBrick.cs:9`, `NetworkWeaponFireGlue.cs:43`: 탄창·재장전·사격 대기 복원.
- `PlayerDeathGlue.cs:25`: 부활 남은 시간 복원.
- `PlayerNetworkMovementGlue.cs`, `DamageReceiverGlue.cs`, `PlayerHeistGlue.cs`: 복원 중 서버 행동 차단.
- `HeistHudGlue.cs`: 기존 중앙 흐름에 호버 표시 호출 추가.
- `HeistConfig.cs:56`: 검사 속도/정체/재시도; `:74` 복원 위치 분리 설정.
- `PoliceEnemyConfig.cs:10`: 목적지 간격·대체 후보·재탐색·정체 설정.

## 씬/에셋 연결

- 씬: `Assets/Scenes/Kenney_City2x2_Online.unity`.
- 추가 Config: `Assets/Data/Heist/BuildingHoverConfig.asset`.
- 계층: `HeistHUD/BuildingHover/Panel/Information`.
- 기존 HUD Canvas/한글 TMP 폰트 재사용. 배경·텍스트 Raycast Target 비활성. HUD→BuildingHoverGlue→BuildingHoverView 연결.
- 기존 HeistConfig/PoliceEnemyConfig/HostMigrationCloudConfig 에셋은 추가 필드 기본값 사용. 새 네트워크 프리팹을 요구하지 않는다.
- 네트워크 직렬화 필드가 추가됐으므로 테스트 전 Host/Client 모두 플레이 모드를 종료하고 같은 코드로 재시작한다.

## 뭉침 원인과 수정

코드에서 확인한 원인: 작업이 없는 상태에도 CancelServer가 경로를 초기화하던 흐름, 공유 순찰/전투 목적지, 목적지 점유 부재, 스폰 중첩 검사 부족, 반복 경로 요청. 작업이 있을 때만 취소하도록 수정하고 결정적인 후보·점유 해제·재탐색 간격·회피 우선순위·스폰 중첩 검사를 적용했다. 검사 문 앞도 목적지 원장에 예약한다. 좁은 골목의 실제 교착 제거는 육안 검증이 남아 있다.

현재 Animator는 Idle/Walk 이동 전환을 재사용한다. 별도 Run 전환을 추가하지 않았으므로 출동 속도와 발걸음의 일치는 체감 조정 대상이다.

## 실제 완료 테스트

- 마지막 감사비 정산 중 회수 보류 수정까지 Unity 컴파일 오류 0 확인.
- ExtendedMigrationTests 12/12: 원장 반환 1회, 운반자 감사비 제외, 추출 장물 보존, 조사 상대 시간, 로드 전 지갑 보상, 동일 영수증 확정, 미확인 최신 Revision 거부, 목적지 해제, 순찰 대기, schema 1/2, 중복 개체 거부.
- HeistRulesTests 7/7.
- 실제 로컬 Host 플레이 모드 복원 13항목 통과: 경찰 13명/펫 1마리, 절도→도주, PetId·장물 유지, 도주 경로 재계산, 같은 Snapshot 재적용 시 펫/경찰 중복 없음, RunId 유지.
- 검사 플레이 모드 9항목 통과: 출동 상태/속도, 중복 예약 차단, 사망 해제, 다른 경찰 재배정, 문 앞 도착 후 검사, 복원 시 미완료 검사 취소/대기 전환.
- 측정한 테스트 Snapshot JSON은 20,177 UTF-8바이트. 대규모 장물 원장 누적 부하 테스트는 아직 안 했다.
- UI 직접 Show 호출 성공. 현재 CLI Game 캡처에 Overlay UI가 보이지 않아 시각적 배치·잘림 검증 완료로 계산하지 않는다.

## 남은 직접 테스트 순서

1. Host A, Client B/C를 동일 코드로 실행. 건물 호버 금액/대기/출동/검사 표시 일치와 가장자리 잘림·한글 확인.
2. 검사 시간이 되면 실제 경찰 1명 출동→도착 후 입장. 도중 공격/사망 시 재배정 확인.
3. 순찰·검사·수색·다수 추격을 좁은 골목에서 관찰. 간격·속도·발걸음 체감 조절.
4. 펫 절도 중, 경찰 검사 중, 검사 출동 중 각각 A 종료. 새 호스트에서 취소·문 앞 분산·재배정 확인.
5. 도주 펫/회복 대기 펫/늦게 재접속한 주인, 지연 수배, 경찰 추격, 잔탄·재장전·부활 타이머 확인.
6. 신고 후 경찰 실제 회수→돈 반환/감사비 1회→Cloud 저장 후 펫과 자식 짐 제거. 회수 전 다른 사람이 획득하면 신고 취소·생존 확인.
7. 회수 직후/탈출 저장 직후 호스트 종료. 이미 종료한 펫과 장물이 되살아나지 않는지 확인. Cloud 장애 시 복원/정산 보류 표시도 확인.
8. 호버는 발사 입력을 먹지 않고, 실제 버튼 위 클릭은 발사하지 않는지 확인.

## 제한 / 아직 완료로 보지 않는 부분

- 가상 플레이어 2개는 켜져 있으나 별도 CLI 서버가 없어 이번 검증은 단일 로컬 Host였다. 2~3인 Relay 교체, 실제 Cloud 회수 완료·탈출 통합 검증은 미완료.
- 현재 펫은 기존 단일 프리팹으로 복원한다. 향후 여러 종 프리팹 카탈로그/종류 키는 별도 확장이 필요하다. 플레이어 장착 무기도 현재 기존 무기 구성을 재사용한다.
- 구형 schema 1에는 확장 월드 상태가 없으므로 새 필드의 과거 상태를 복구할 수 없다.
- Cloud 최신 기록을 검증할 수 없으면 오래된 확장 월드로 임의 진행하지 않는다. 수동 정합성 확인이 필요한 Revision 충돌도 자동 덮어쓰지 않는다.
- 회수 체크포인트 실패 시 펫은 종료 예약 상태로 남아 재시도한다. 정상 디스폰까지 저장 지연이 보일 수 있다.
- 신고자가 탈출 정산 중이면 확정 금액에 감사비를 끼워 넣지 않도록 회수를 보류한다. 정산 이후 새 지갑으로 적립된 보상은 기존 정책상 그 세션의 다음 탈출에서 Cloud에 정산된다.
- UI 절도 가능 표시는 예상 안내이며 서버에서 최종 검사한다. 검사 중 절도는 기존 적발 규칙을 유지하고 위험 안내를 표시한다.
- 비행 총알·일회성 효과·NavMesh 경로는 복원하지 않는다.

커밋하지 않았다. 작업 전부터 존재한 다른 수정도 보존했다.
