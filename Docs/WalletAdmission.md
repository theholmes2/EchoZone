# 입장 전 개인 지갑 준비

2026-10-02. Registry 사전 생성 설계를 폐기하고 기존 Protected `extraction_wallet_v1`을 사용한다.

## 흐름

인증 → 수동 Host/Join 요청 → 본인 지갑 조회 → 없을 때만 0원 레코드 생성 → 재조회 → 미완료 정산 복구 확인 → 입장.
기존 지갑은 초기화하지 않는다. 조회 오류는 지갑 없음으로 취급하지 않는다. 기존 Pending 정산은 Resume로 처리할 수 있으므로 정상 정산에 의한 갱신은 가능하다.
준비 실패 시 입장하지 않는다. 요청 중 중복을 차단하며 실패 후 기본 5초가 지나야 수동 재시도한다. 자동 반복 요청은 없다.
자동 재접속·호스트 마이그레이션에는 새 초기화 경로를 추가하지 않았다.

## 변경 파일과 설명 시작점

- Assets/Script/Heist/WalletPreparation.cs:10 — 조회/누락 시 생성/재조회. 같은 파일의 WalletAdmissionGate는 중복·쿨다운 순수 상태 처리.
- HostMigrationModule/Project/WalletPreparationFunctions.cs:13 — EnsureOwnWallet. 인증된 context.PlayerId만 사용하고 Cloud Code가 Protected에 기록.
- Assets/Script/Online/Relay/RelaySessionGlue.cs:339, 423, 685 — 수동 Host/Join에 준비 게이트 연결.
- Assets/Script/Heist/WalletCloudService.cs:16 — 클라이언트의 Cloud Code 호출.
- Assets/Script/Data/RelaySessionConfig.cs:13 — 모듈/함수명, 재시도 간격.
- HostMigrationModule/Project/WalletFunctions.cs:125, 174 — Registry 분기를 제거하고 기존 Protected 읽기/CAS 쓰기 사용.
- HostMigrationModule/Project/HostMigrationModule.csproj — 순수 WalletPreparation 소스 공유.
- Assets/Tests/Editor/WalletPreparationTests.cs — 준비 흐름 7개 테스트.

WalletRegistryFunctions.cs, WalletRegistryBrick.cs, WalletRegistryTests.cs 및 해당 Unity 메타 파일을 제거했다. 실제 Registry 생성/배포는 하지 않았으므로 Cloud 데이터 삭제는 없다. WalletAdmissionRegistry.md는 폐기 표시한 역사 문서다. 이번 단계의 씬·프리팹 연결 변경은 없다.

## 검증 및 제한

- Unity 컴파일 오류 없음. EditMode 92/92 통과(지갑 준비 7개 포함).
- Cloud 모듈 빌드 오류 0, 경고 0.
- 기존 지갑 보존, 조회/생성 실패, 재조회 미확인, 연타/쿨다운, 생성 성공 후 응답 유실 재시도 테스트 통과. 모의 저장소 테스트이며 실클라우드 통합 테스트가 아니다.
- Cloud 배포와 실계정 데이터 쓰기는 하지 않았다. 새 EnsureOwnWallet을 포함한 모듈 배포 후 신규 계정 생성, 기존 잔액 유지, 실패 시 입장 차단을 확인해야 한다.
- 최초 생성은 원자적 create-if-absent가 아니다. 동일 계정 여러 PC/여러 방 동시 이용은 지원하지 않으며 이를 서버 전체 잠금으로 강제하지도 않는다. 응답 유실 후 아주 늦은 최초 생성 요청이 후속 정산을 덮어쓸 가능성을 완전히 제거하지 못한다. 포트폴리오 단일 접속 전제의 제한이다.
- 정산 CAS, Revision, SettlementId 및 Pending/Receipts/Recoveries 흐름은 유지한다. 커밋·푸시는 하지 않았다.
