# 입장 전 지갑 준비 / 사전 생성 CAS 영역

> 폐기된 설계의 역사 기록이다. 2026-10-02 정책 단순화로 Registry 코드/테스트를 제거했다. 아래 프로비저닝 절차는 실행하지 않는다. 현재 구현은 WalletAdmission.md를 참조한다. 실제 Registry는 생성/배포한 적이 없으므로 Cloud 데이터 삭제도 하지 않았다.

2026-10-02. 코드 구현 상태이며 실제 Cloud 리소스 생성·배포·동시 요청 검증은 아직 하지 않았다. 기존 개인 잔액을 변경하지 않았다. 커밋·푸시 없음.

## 흐름과 역할

로그인 → EnsureOwnWallet → 기존 지갑 조회 또는 신규 0원 지갑 CAS 저장 → 저장 결과 재조회 및 미완료 정산 복구 → 성공한 경우만 기존 멤버십 정리 → 방 생성/입장.

- 기존 Protected Player Data `extraction_wallet_v1`가 있으면 계속 그 항목을 사용한다. 삭제·복사·초기화하지 않는다.
- 신규 계정은 Private Game Data의 `wallet_registry_v1_00`부터 `wallet_registry_v1_ff`까지 256개 Custom Item에 분산한다. SHA256(PlayerId)의 첫 바이트가 영역을 결정한다.
- 각 Custom Item의 `wallets` 키는 **운영자가 서비스 중지 상태에서 사전 생성**한다. 초기 JSON은 `{"Schema":1,"Wallets":{}}`다.
- 신규 지갑의 잔액·Pending·Receipts·Recoveries는 그 영역 내부에 계속 저장한다. 잠금 후 별도의 Protected 0원 지갑을 만드는 방식이 아니다. 지연된 초기화 요청의 덮어쓰기를 피하기 위해 권위 저장소를 분리했다.
- 기존 키 조회 실패를 신규 계정으로 취급하지 않는다. 영역이 없거나 손상되거나 가득 차면 실패하고 입장을 막는다.
- 순수 초기화/영역 선택은 WalletRegistryBrick, Cloud API는 WalletFunctions, 클라이언트 호출은 WalletCloudService, 순서 제어는 RelaySessionGlue이다.
- 자동 Reconnect/마이그레이션/LeaveMatchingMembershipAsync/티켓 모델은 변경하지 않았다. 수동 Host/Join 경로의 기존 동시 실행 방지 플래그 안에 준비 호출을 추가했다. 실패 후 수동 재시도 최소 간격은 5초다.

## A/B 경쟁

1. A/B가 같은 영역과 토큰 v1을 읽는다.
2. A가 계정 0원 항목을 추가해 v1 조건부 쓰기 성공 → v2.
3. B의 v1 쓰기는 거절된다. B는 재시도 때 다시 조회하므로 계정을 초기화하지 않는다.
4. A가 정산해 v3가 되었더라도 늦은 B 초기화의 v1은 거절된다.
5. 정산도 같은 영역 토큰으로 갱신한다. 같은 ID/본문은 기존 영수증, 다른 본문은 충돌, 다른 ID의 오래된 Revision도 충돌이다.

직렬화 기준은 Cloud Save 서버의 **기존 키 writeLock**이며 Cloud Code static lock이 아니다. 쓰기 응답이 유실되면 같은 준비 요청을 다시 실행해 저장된 결과를 읽는다. 세션 Snapshot은 기존 Revision/SettlementId 검증으로 대조하며 알 수 없는 버전을 덮어쓰지 않는다.

EnsureOwnWallet은 금액/PlayerId 인수를 받지 않고 인증 context.PlayerId만 사용한다. 실제 정산은 기존 현재 Host·참가자 검증을 유지한다. 정상 Host 경합은 보호하지만 악성 Host가 신고한 게임 금액 전체를 재시뮬레이션하지 않는다. 다른 PC 동일 계정 동시 플레이 정책은 여전히 지원 범위 밖이다.

## 변경 파일과 핵심 위치

- 신규 Assets/Script/Heist/WalletRegistryBrick.cs — 영역 선택, 기존 값 보존 초기화.
- 신규 HostMigrationModule/Project/WalletRegistryFunctions.cs:18 — EnsureOwnWallet.
- 같은 파일 :36 / :50 — 사전 생성 영역 조회 / CAS 쓰기.
- HostMigrationModule/Project/WalletFunctions.cs:125 / :183 — 기존 Protected와 신규 Registry 읽기·쓰기 분기. 구 SettleEscape의 무조건 최초 생성도 제거.
- HostMigrationModule/Project/HostMigrationModule.csproj — 순수 Registry 계약 링크.
- Assets/Script/Heist/WalletCloudService.cs:16 — EnsureOwn 호출.
- Assets/Script/Online/Relay/RelaySessionGlue.cs:339 / :423 / :685 — 생성·입장 전에 준비, 실패 시 중단.
- Assets/Script/Data/RelaySessionConfig.cs:11 — 모듈/함수명/재시도 간격. 기본 HostMigrationModule / EnsureOwnWallet / 5초.
- 신규 Assets/Tests/Editor/WalletRegistryTests.cs — CAS 저장소 모사 테스트 7개.

씬/프리팹은 변경하지 않았다. Unity 스킬에 따라 새 스크립트는 Editor AssetDatabase.Refresh로 임포트했다.

## 배포 전 필수 순서

1. 별도 테스트 환경을 먼저 선택한다. 기존 배포 모듈을 보관한다.
2. **모든 기존 세션/구버전 Cloud 쓰기를 중지하고 완료를 기다린다.** 구버전 무조건 생성 함수와 새 Registry 쓰기를 동시에 운영하면 안 된다.
3. 관리자 권한으로 256개 Private Custom Item의 wallets 키를 누락분만 생성한다. 이미 존재하는 값은 절대 `{}`로 덮어쓰지 않는다. 운영 중 재프로비저닝 금지.
4. 새 HostMigrationModule을 배포하고 최신 클라이언트만 사용한다. 신규 지갑을 만든 뒤에는 구버전 서버로 단순 롤백하지 않는다.
5. 테스트 계정으로 입장 전 생성, 반복 준비, 동시 초기화, 정산 응답 유실, 재접속/마이그레이션을 확인한다. 성공·실패 요청과 잔액/Revision을 기록한다.

현재 이 순서를 실제 실행하지 않았다. 따라서 지금 새 클라이언트는 미배포 함수 오류로 입장이 막힐 수 있다. 배포 완료로 해석하면 안 된다.

## 한계와 미검증

최종 로컬 검증: Unity 컴파일 failed=false/errors=[], Cloud 모듈 빌드 경고 0·오류 0. 전체 EditMode **83/83 통과**, 신규 Registry 테스트 7개 포함. 중간에 RelaySessionGlue의 Exception 네임스페이스 누락을 발견해 System.Exception으로 수정했다. 최초 76개 실행은 이전 어셈블리 결과여서 최종 신규 검증으로 세지 않았다.

- 1영역 JSON은 200KB에서 안전 보류한다. 정산 영수증이 늘면 소수 계정으로도 차며 장기 서비스용 구조가 아니다. 영역 재분할/영수증 보관 또는 트랜잭션 DB가 후속 대안이다.
- 같은 영역의 서로 다른 계정도 쓰기 경합할 수 있다. 경합은 재조회 재시도하며 금액을 합쳐 임의 해결하지 않는다.
- Cloud 기능 호출 직전의 계정 변경/플레이 종료 및 실제 Relay 입장 경합은 통합 검증이 남았다.
- 2단계 회수 인덱스만 저장되고 월드 저장이 실패한 경우의 수동 복구 제한은 그대로다.
- 로컬 CAS 모사 테스트는 실제 Cloud API의 원자성·권한·배포 호환성 검증을 대체하지 않는다.
- 실제 Cloud 잔액 변경, Cloud 리소스 생성, 배포 없음. Console은 지우지 않았다.
