# 정산 복구 2단계 진행 기록 (2026-10-02)

## 상태

로컬 코드 구현과 순수 로직 테스트까지 진행했다. Cloud 배포 및 실제 장애 주입 통합 검증은 완료하지 않았다. 현재 클라이언트는 새 Cloud 함수가 필요하므로 기존 배포본과 그대로 조합해 정산 완료를 기대하면 안 된다. 커밋·푸시하지 않았다.

## 저장 흐름

1. 서버가 SettlementId, PlayerId, SessionId, RunId, 기준 Revision, 최종 Balance, PetIds, LedgerIds를 고정한다.
2. PrepareEscape가 기존 Protected extraction_wallet_v1 항목의 Pending에 요청을 조건부 저장한다. 잔액은 아직 변경하지 않는다.
3. 월드 체크포인트를 확인한다. LoadWallet/ResumeOwnWallet이 저장된 Pending만 처리하여 잔액·Revision·영수증·Pending 제거를 같은 항목에 조건부 저장한다.
4. 응답 유실 시 동일 요청의 영수증을 검증한다. ID가 같고 본문이 다르면 거절한다.
5. 성공 후 장물 완료 상태와 펫 종료 기록을 체크포인트에 저장한다. 확인 후 펫을 Despawn하고 소유자에게 퇴장을 통지한다.
6. 퇴장 실패는 Cloud 정산을 반복하지 않고 기존 Relay 퇴장만 지수 백오프로 재시도한다. 재스폰·마이그레이션 이전 async 응답은 세대로 구분한다.

회수·신고 감사비는 기존대로 월드 원장에서 관리하며 탈출 전 개인 Cloud 확정 잔액에 즉시 넣지 않는다. 회수 요청은 계정 복구 인덱스를 먼저 기록하고 월드 체크포인트를 저장한다. 체크포인트 성공 전에는 최종 Despawn하지 않는다. 오래된 체크포인트가 완료 펫/회수 기록을 제거하는 저장은 거절한다.

## 추가·수정 파일

- 신규 Assets/Script/Heist/SettlementJournalBrick.cs:9,104 — 불변 요청 계약, 영속 Pending/Receipts/Recoveries, 중복·Revision 검증, 백오프.
- 신규 Assets/Tests/Editor/SettlementJournalTests.cs — 저장 실패와 응답 유실을 모사하는 순수 테스트 16개.
- Assets/Script/Heist/WalletSessionBrick.cs — 요청과 최종 처리 상태의 Snapshot 보존.
- Assets/Script/Heist/WalletCloudService.cs — PrepareEscape/LoadRunWallet 연결.
- Assets/Script/Heist/PlayerWalletGlue.cs:62,203,269 — 마이그레이션 재연결, 정산 순서, 퇴장 재시도.
- Assets/Script/Data/ExtractionConfig.cs:20 — 새 함수명과 최대 재시도 지연 60초. 기존 초기 지연 사용.
- Assets/Script/Heist/HeistLedgerBrick.cs — 정산 대상 장물 ID 수집.
- Assets/Script/Heist/HeistWorldGlue.cs, HeistRetirementCommit.cs, HeistWorldMigration.cs — 회수 요청 원장·복구·백오프와 종료 전 저장.
- Assets/Script/Heist/PetHeistGlue.cs, Assets/Script/Pet/PetStateGlue.cs — 정산 보관 중 획득/회수/행동 제한.
- Assets/Script/Online/Migration/SessionWorldSnapshot.cs:20, SessionWorldSnapshotValidator.cs — 회수 원장 직렬화와 검증.
- Assets/Script/Online/Migration/SessionWorldMigrationGlue.cs — 미접속 참가자도 Cloud 지갑/영수증 확인, 알 수 없는 버전은 보류.
- HostMigrationModule/Project/WalletFunctions.cs:25,48,56,93 — 접수, 본인 복구, Run 참가자 조회, 회수 인덱스.
- HostMigrationModule/Project/HostMigrationFunctions.cs:60,209 — 회수 인덱스 저장 및 완료 기록 후퇴 방지.
- HostMigrationModule/Project/HostMigrationModule.csproj — 순수 정산 계약을 Cloud 모듈에서도 공유.

이번 정산 단계에서 씬·프리팹 연결은 추가하지 않았다. 기존 ManualUpdate/소유자 HUD/Relay 퇴장을 재사용한다. 기존 경찰·펫·건물 작업의 변경 파일은 유지했다.

## 실제 검증

- Cloud 모듈 dotnet build --no-restore: 경고 0, 오류 0.
- Unity EditMode 전체: 76/76 성공, 실패·건너뜀 0. 신규 정산 순수 테스트 16개 포함.
- 접수 전 저장 실패, 접수 후 재생성, 성공 응답 유실, 중복 요청, 동일 ID 내용 변경, Revision 충돌, Snapshot 요청 복원, 확정 전 완료 금지, 기존 잔액 필드 보존, 백오프를 로컬 모사했다.
- 검사 시 Main Editor Play Mode=false, compiling=false, 활성 씬 dirty=false.
- Console을 지우지 않았다. 이번 기록은 전체 과거 Console 오류가 없다는 뜻이 아니다.
- 이번 단계에서 Cloud 함수를 배포하거나 실제 지갑 잔액을 변경하지 않았다.

## 미완료·배포 전 조건

1. 최초 지갑 키가 없는 신규 계정은 안전한 CAS 토큰이 없어 접수/회수 인덱스 저장을 보류한다. 최초 생성 동시성은 별도 단계이며 현재 신규 계정 정산이 완성된 상태가 아니다.
2. 회수 인덱스 저장 뒤 월드 체크포인트 저장 실패와 Host/Session 소멸이 겹치면 기록은 찾을 수 있지만 자동 월드 재구성/보상 확정은 하지 않는다. 수동 대조가 필요하다.
3. 영수증·회수 인덱스 각각 128개, 저장 봉투 200KB 한도에서 삭제하지 않고 보류한다. 장기 보관/분할 기능은 미구현이다.
4. 최초 영속 쓰기 전 모든 프로세스가 사라진 요청의 복구는 보장하지 않는다.
5. 알 수 없는 Cloud Revision/완료 전 Snapshot은 덮어쓰지 않고 보류한다. 모든 오래된 Snapshot의 자동 정상화는 미구현이다.
6. 실제 Cloud writeLock 경합·응답 유실, 정상 탈출, Relay 퇴장 재시도, 회수 체크포인트 실패 시 펫 유지, 성공 시 Despawn, Host/Session 소멸 뒤 복구는 통합 테스트가 남았다. 앞선 Relay 마이그레이션 테스트 성공을 이번 정산 장애 테스트 성공으로 세지 않는다.

다음 순서: 최초 지갑 생성 정책 확정 → 제한 사항 보완 → 테스트 환경에 Cloud 모듈 배포 → 별도 테스트 계정으로 잔액/Revision/SettlementId 전후 기록 → 실제 실패/응답 유실/재접속/마이그레이션 검증. 기존 사용자 잔액 초기화는 하지 않는다.
