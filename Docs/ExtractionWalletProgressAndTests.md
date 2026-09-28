# 탈출 지갑 / 멀티 검증 — 2026-09-24

## 후속 수정: 같은 방 재입장 허용

- LastSessionId가 같다는 이유로 정산 완료/자동 퇴장 처리하지 않는다. LastSessionId는 출처 기록만 유지한다.
- WalletSessionBrick.BeginEscape가 새 SettlementId를 만들며 실패 재시도에는 같은 ID를 유지한다. Confirm은 ID·잔액·버전을 모두 검사한다.
- HeistWorldGlue.Wallet은 새 플레이어가 요청할 때 이전 지갑이 Settled인 경우에만 새 지갑으로 교체하여 클라우드를 다시 읽는다. 미완료 지갑은 유지하므로 단기 재접속의 미정산 돈을 초기화하지 않는다.
- WalletCloudService/WalletFunctions에 settlementId/LastSettlementId 추가. 같은 ID·같은 요청만 재전송으로 인정하며 동일 방의 다른 탈출은 새 버전으로 저장한다. 기존 저장 키와 잔액은 유지된다.
- 수정 파일: WalletSessionBrick.cs, HeistWorldGlue.cs, PlayerWalletGlue.cs, WalletCloudService.cs, WalletFunctions.cs, WalletSessionTests.cs. 씬/프리팹 변경 없음.
- Unity 컴파일 및 지갑 테스트 4/4 통과, Cloud Code 빌드 경고/오류 0, 기존 환경 모듈 배포 완료.
- 실제 Relay 검증: 방 WT6QWC에서 Player2가 첫 탈출(테스트 보상 +1) 후 같은 방 재입장, 두 번째 탈출(+1) 후 다시 같은 방 재입장. ClientId 1→2→3, 잔액 1030→1031→1032, 각 재입장 IsEscaping=false 확인. 서버 보상 호출/텔레포트로 준비한 통합 테스트이며 손 조작 테스트는 별도다.
- 이번 검증으로 Player2 확정 잔액은 1032가 됐다. 아래 1030 기록은 이전 검증 시점의 기록이다. Play 종료 완료.

## 확정 정책과 구현

- 서버가 게임 중 개인 돈의 수입·지출을 관리한다. 개인 돈과 펫 장물은 별개이며, 장물은 성공한 탈출에서만 개인 돈에 합산한다.
- 방 입장 시 마지막 클라우드 확정 잔액을 읽는다. 읽기 실패를 잔액 0으로 덮어쓰지 않는다.
- 탈출 지점에서 플레이어와 소유 펫이 범위 안에 있고 작업 중이 아니면 3초 대기한다. 벽 너머 탈출은 차단한다.
- 탈출 승인 → 금액 고정 → 펫 장물 원장 정산 및 펫 디스폰 → Cloud Code 저장 → 성공 응답 뒤 기존 Relay 퇴장 순서다.
- 저장 중 이동·발사·재장전·상호작용·피해를 제한한다. 통신 실패 시 같은 금액/버전/세션으로 재시도한다.
- 기존 Update에서 ManualUpdate를 호출한다. 독립 Update는 추가하지 않았다. 개인 잔액과 상태 안내는 Owner에게만 동기화한다.
- 상점, 무기·방어구 영속 저장은 아직 구현하지 않았다.

## 파일 / 연결

### 추가

- `Assets/Script/Data/ExtractionConfig.cs`: 탈출 범위·시간·차폐 레이어·재시도 간격·Cloud Code 함수명 설정.
- `Assets/Script/Heist/WalletSessionBrick.cs`: 순수 잔액 계산, 로드 전 보상 누적, 잔액 부족 차단, 한 번만 탈출 합산, 정산 응답 검증.
- `Assets/Script/Heist/WalletCloudService.cs`: Cloud Code 요청 및 응답 DTO.
- `Assets/Script/Heist/PlayerWalletGlue.cs`: 인증 계정 해석, 서버 탈출 판정, Brick/Cloud/네트워크/퇴장 연결.
- `Assets/Script/Heist/ExtractionPoint.cs`: 위치와 Config만 제공하는 임시 지점.
- `HostMigrationModule/Project/WalletFunctions.cs`: 현재 로비 Host 및 대상 참가자 검증, Protected 지갑 읽기/조건부 쓰기.
- `Assets/Tests/Editor/WalletSessionTests.cs`: 지갑 규칙 4개 테스트.
- `Assets/Data/Heist/ExtractionConfig.asset`, `ExtractionMarker.mat`.

### 변경

- `HeistWorldGlue`: 인증 PlayerId별 세션 지갑 캐시, 장물 탈출 정산 연결.
- `HeistLedgerBrick`: Extracted 상태 추가. 이미 탈출한 돈은 반환·중복 지급하지 않지만 도난 증거는 유지한다.
- `PlayerHeistGlue`: 기존 임시 보상 잔액 대신 PlayerWalletGlue로 현상금/감사비 지급. 탈출 중 요청 차단.
- `HeistHudGlue`: 개인 돈 및 탈출 안내 표시.
- `PlayerNetworkMovementGlue`: 기존 루프에서 지갑 ManualUpdate 및 탈출 중 입력/이동 차단.
- `NetworkWeaponFireGlue`, `DamageReceiverGlue`, `PlayerInteractionGlue`: 탈출 정산 중 행동·피해 차단.
- `NetworkPlayerSessionCacheGlue`: 승인된 ClientId→PlayerId 조회, 명시적 새 방 생성 전 이전 방 캐시 초기화.
- `RelaySessionGlue`: 탈출 후 기존 명시적 퇴장 흐름 재사용. 새 방 생성에서만 캐시 초기화하며 마이그레이션에는 적용하지 않는다.
- `Assets/Prefabs/Player/KenneyNetworkPlayer.prefab`: PlayerWalletGlue 추가.
- `Assets/Scenes/Kenney_City2x2_Online.unity`: `TEMP_EXTRACTION_POINT`, 위치 (0, 0.12, 8), 녹색 표식/안내 및 Config 연결.

## 클라우드

- 기존 `HostMigrationModule`에 LoadWallet/SettleEscape 추가 배포 완료. 다른 클라우드 리소스는 배포하지 않았다.
- Protected Player Data 키: `extraction_wallet_v1`.
- Balance, Revision, LastSessionId, 서버 SavedAtUtc 저장. 일반 플레이어의 Protected 직접 쓰기는 허용하지 않는 서비스 접근 클래스다. 별도 광역 Access Control 정책은 변경하지 않았다.
- 같은 정산 ID/같은 요청 재전송은 기존 결과 반환. 동일 ID에 다른 금액·세션·기준 버전을 보내거나 오래된 Revision을 보내면 거절. 기존 키 수정은 writeLock으로 경쟁 쓰기를 검사한다.
- 보안 범위는 호스트 권위 방식이다. Cloud Code는 Host 신분과 참가자 소속을 검사하지만 호스트가 보낸 금액의 모든 게임 행위를 재시뮬레이션하지 않으므로 악성 Host까지 방지하는 시스템은 아니다.

## 실제 검증 완료

1. Unity 컴파일 오류 없음. Cloud Code 모듈 빌드 경고 0 / 오류 0.
2. WalletSessionTests 4/4, HeistRulesTests 7/7 통과.
3. 실제 Relay 3인 연결. 후발 Player3가 건물 9,000 / 기존 펫 장물 1,000을 수신.
4. 서버 테스트 준비로 Player2 보상 +50, 지출 -20, 장물 +1,000. 클라이언트에는 개인 돈 30과 장물 1,000이 별도로 복제됨. 이것은 수동 절도 조작 전체 검증이 아닌 서버 함수 기반 통합 테스트다.
5. Player2와 펫을 탈출 지점으로 이동시켜 실제 3초 판정·클라우드 저장·퇴장 성공. Host와 Player3는 기존 방 유지.
6. 새 Play/새 방에서 Player2 개인 돈 1,030 복원 확인. Main 계정도 이전 탈출의 1,030 복원 확인.
7. 일반 Client의 LoadWallet 직접 호출은 UnauthorizedAccessException으로 거절됨. 이 검증이 Console에 남긴 422는 의도한 권한 거절이다. SettleEscape는 같은 검증 함수를 쓰지만 쓰기 거절 경로의 별도 호출 테스트는 하지 않았다.
8. 역할을 바꿔 Player2 Host / Main Client 연결. 서버 테스트 준비로 Host를 수배하고 피해 수신부를 호출함. 첫 피해 100, 두 번째 0; 수배 해제, Client 개인 돈 1,030→1,530 (현상금 500 한 번) 복제 확인. 실제 총 조준 조작 검증은 별도다.
9. 마지막 현상금 500은 탈출하지 않고 테스트를 종료했으므로 클라우드 확정 잔액은 두 테스트 계정 모두 1,030이다. 실제 테스트 저장을 수행했으며 원래 0으로 되돌리지 않았다.
10. 새 방 생성 시 이전 서버 접속 캐시 때문에 최초 플레이어 승인이 거절되는 문제를 수정했다. 가상 플레이어의 오래된 씬에는 탈출 지점이 없었으므로 저장된 최신 씬을 다시 열어 동기화했다. 이전 NetworkConfig mismatch는 최신 상태로 재시작한 후 재현되지 않았다.

테스트 종료 시 Play를 중지했으며 임시 경찰 정지·텔레포트·수배 준비 상태는 씬에 저장하지 않았다.

## 미완료 / 중요한 한계

- 미정산 개인 돈, 장물 원장, 건물 돈, 수배·수색은 아직 Host Migration Snapshot에 포함되지 않는다. 이 상태로 호스트가 나가면 남은 방의 새 기능 상태 보존을 보장하지 않는다. 탈출 확정 클라우드 잔액만 다음 게임 복원 확인을 마쳤다.
- 클라우드 첫 키 생성에는 기존 writeLock이 없으므로, 같은 신규 계정이 서로 다른 방에서 동시에 첫 정산하는 경쟁 상황은 아직 안전성을 보장하지 않는다. 기존 키의 Revision/writeLock 검증과 구별해야 한다.
- 저장 실패 중 금액은 현재 Host 메모리에만 보관된다. 영속 미완료 정산 큐가 아니며 Host 종료/마이그레이션을 넘는 재시도는 미구현이다. Revision 충돌 같은 영구 오류의 사용자 복구 UI도 남았다.
- 저장 성공 뒤 Relay 퇴장 실패에 대한 전용 재시도 UI/회복 테스트는 남았다.
- 새 절도 기능은 기존 NavMesh에서 일부 펫→문 경로가 Partial이다. 앞선 멀티 테스트는 문 근처로 옮긴 뒤 작업을 검증했다. 모든 건물로 자연스럽게 이동하는 검증은 완료하지 않았다.
- 3인 동시 절도, 실경찰의 장물 회수 및 타인 5% 감사비, 주인 퇴장/경찰 사망 중 작업 취소, 실내 숨김 상태의 후발 동기화는 아직 멀티 전체 흐름 미검증이다.
- 다른 PC 빌드/네트워크 단절·응답 유실/클라우드 쓰기 충돌 테스트는 미완료다.

## 직접 확인할 순서 / 체감

1. 최신 씬을 모든 가상 플레이어에서 연다. Host/Client 입장 후 HUD 개인 돈을 기록한다.
2. 건물 문에서 절도 지시: 펫 이동·숨김·8초 작업·짐 증가, 양쪽 건물 돈 일치. 막힌 경로는 별도 기록한다.
3. 짐을 실은 펫과 스폰 근처 녹색 탈출 지점으로 이동한다. 3초 대기 → 저장 안내 → 시작 화면 복귀. 새 방에서 이전 개인 돈 + 장물인지 확인한다.
4. 대기 도중 범위 밖으로 나가면 취소, 펫이 멀거나 작업 중이면 시작하지 않는지 확인한다.
5. 탈출하지 않고 종료하면 이번 미정산 수입이 다음 게임에 반영되지 않는지 확인한다.
6. 절도 적발/수색/발견 게이지/현상금/펫 탈취·신고는 기존 `HeistProgressAndTests.md` 수동 절차를 수행한다.
7. 체감 항목: 탈출 표식 가독성, 탈출 위치와 반경, 3초 대기, 펫 집결 거리, 짐 감속, 절도·검사 텀, 얼굴 확인 시간, HUD 안내.

설정은 `Assets/Data/Heist/ExtractionConfig.asset`과 기존 `HeistConfig.asset`에서 조절한다.
