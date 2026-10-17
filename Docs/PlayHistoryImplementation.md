# 개인 회차 전적 구현 및 검증

## 흐름

서버 행동 확정 → 개인 회차 Brick 누적 → 기존 지갑 Snapshot에 포함 → 탈출 요청에서 요약 고정 → 기존 돈 정산 성공 → Cloud 전적 확정 → 로그인한 타이틀에서 조회.

계정마다 최근 100회만 보관하고 최신 20회를 반환한다. 다른 계정의 인원수에 따라 보관 수가 줄지 않는다. UI는 5행씩 페이지를 전환한다.

## 추가 파일과 핵심 위치

- `Assets/Script/Heist/PlayRecordBrick.cs:11`: 전적 요약. 81: Snapshot 상태. 96: 누적·사망 전환·시간·고정. 154: 100회 보관/20회 조회. 177: 돈 정산 완료 영수증을 전적으로 변환.
- `Assets/Script/Heist/HeistPlayRecords.cs:13`: 현재 방의 개인 회차 연결. 34: 유효한 치명 피해의 처치 귀속.
- `Assets/Script/Heist/PlayHistoryQueryBrick.cs`: 중복 요청, 닫기 및 계정 변경 후 늦은 응답 차단.
- `Assets/Script/Heist/PlayHistoryConfig.cs`: 조회 함수와 표시 행 수.
- `Assets/Script/Heist/PlayHistoryService.cs`: 자신의 Cloud 전적 조회.
- `Assets/Script/Heist/PlayHistoryGlue.cs:10`: 기존 타이틀 갱신과 uGUI 연결.
- `HostMigrationModule/Project/PlayHistoryFunctions.cs:18`: 자신의 기록 조회 및 정산 완료 기록 재처리.
- `Assets/Tests/Editor/PlayRecordTests.cs`: 전적 관련 EditMode 테스트 10개.
- `Assets/Data/Online/PlayHistoryConfig.asset`: 실제 씬에 연결된 설정.
- `AgentScripts/SetupPlayHistory.cs`: 전적 패널·Config·씬 참조 생성용 일회성 도구.
- `AgentScripts/VerifyPlayHistory.cs`: 실제 씬 참조 확인용 읽기 전용 도구.
- 새 Unity 스크립트와 Config의 `.meta`는 Editor 생성 GUID를 사용한다.

## 변경 파일과 핵심 위치

- `Assets/Script/Heist/WalletSessionBrick.cs:9,106`: 회차 소유 및 기존 직렬화 상태의 `playRecord` 필드.
- `Assets/Script/Heist/SettlementJournalBrick.cs:48,117,164`: 고정 전적, 회차 ID, 내용 해시, 저장 완료 표시와 회차 재사용 방지.
- `Assets/Script/Heist/HeistLedgerBrick.cs`: 확정 절도 사건 ID를 반환하여 누적에 재사용.
- `Assets/Script/Heist/HeistWorldGlue.cs:174,289,315`: 절도·유효 신고·실제 포상 연결.
- `Assets/Script/Heist/PlayerHeistGlue.cs:39`: 지급된 현상금 누적.
- `Assets/Script/Combat/Glue/DamageReceiverGlue.cs:43`: 유효한 치명 피해 연결.
- `Assets/Script/Player/Network/PlayerDeathGlue.cs:92`: 생존→사망 집계. 초기 상태 읽기와 복원은 제외.
- `Assets/Script/Heist/PlayerWalletGlue.cs:131,162`: 실제 연결 플레이 시간과 탈출 요약 고정. 장비 환불은 확보 장물에 섞지 않음.
- `Assets/Script/Network/NetworkStartUI.cs`: 기존 갱신에서 전적 패널 사용 가능 여부 전달.
- `HostMigrationModule/Project/WalletFunctions.cs:89,134`: 돈 정산 이후 전적 재처리와 기존 지갑 크기 제한 재사용.
- `HostMigrationModule/Project/HostMigrationModule.csproj`: 순수 전적 코드를 Cloud 프로젝트와 공유.
- `Assets/Prefabs/UI/TitleMenu.prefab`: 내 전적 버튼·목록·상세·재시도·닫기 추가.
- `Assets/Scenes/Kenney_City2x2_Online.unity`: PlayHistoryGlue 및 인증·Config·UI 실제 참조 연결.

기존 사용자 변경은 되돌리지 않았다. 위 파일 중 기존에도 수정 중이던 파일이 있으므로 전체 Git diff를 이번 작업만의 변경으로 취급하면 안 된다.

## Snapshot 추가 상태

기존 플레이어별 WalletSessionBrick 상태 안에 `playRecord`를 추가했다. Summary, 서로 다른 건물 ID 집합, 사건 ID 집합, Dead, Frozen을 포함한다. Summary에는 개인 RoundId, PlayerId, WorldId, RunId, SessionId, 시작/종료 UTC, 누적 플레이 시간과 요청한 모든 집계 항목이 있다. 복원은 누적이 아닌 교체이며 구형 Snapshot에는 새 사건부터 새 회차를 시작한다.

탈출 정산 요청에는 고정 Summary, RoundId, SHA-256 내용 해시를 포함한다. 영수증에는 PlayRecordSaved를 추가했다. 돈 지급과 전적 저장은 별도 단계이고, 전적 목록 갱신과 영수증 저장 완료 표시는 Protected batch로 함께 저장한다. 충돌 시 다시 조회하여 최대 3회 재시도한다. 전적 완료 후 전체 요약만 영수증에서 압축 제거하고 회차 ID·해시·지급 중복 방지 기록은 유지한다. 따라서 100회 밖으로 삭제된 기록을 재요청해도 최신 목록으로 부활시키지 않는다.

## 실제 검증 결과 (2026-10-15)

- Unity 6000.3.19f1 Pipeline 연결 확인.
- 최신 코드 recompile 결과 up_to_date, recompile_status failed=false/errors=[] 확인.
- 신규 전적 EditMode 10/10 통과.
- 전체 EditMode 154/154 통과, 실패·건너뜀 0.
- 테스트 범위: 누적/중복, 동일 건물, 사망·부활, 직렬화 반복 복원, 구형 Snapshot, 시간 정지, 돈 정산 실패, 전적 재시도, 변경된 요청 거절, 101번째 보관 정리와 과거 요청, UI 늦은 응답 차단.
- 실제 온라인 씬에서 Config·인증·모든 버튼·목록 5행 참조 검사 통과.
- Cloud Code 로컬 빌드 경고 0/오류 0. 실제 Cloud 배포는 하지 않음.
- 실제 Cloud CAS 충돌/네트워크 장애, Host/Client, 재접속/Migration 통합, 실제 계정 조회 및 PlayMode 화면 체감 검증은 아직 하지 않음. Brick 테스트 통과를 이 항목의 성공으로 해석하면 안 됨.

## 남은 수동 테스트

Cloud 배포 승인 및 배포 후 다음을 진행한다.

1. 로그인 → 내 전적: 최초 기록 없음, 닫기, 재열기, 새로고침 확인.
2. 같은 건물 두 번 절도: 총액은 합산, 건물 수는 1. 다른 건물 절도 후 2 확인.
3. 경찰/다른 플레이어 처치, 자기 사망·부활, 신고/포상/현상금의 실제 지급 후 탈출. 상세 수치와 장물·환불 구분 확인.
4. 죽고 부활한 뒤 탈출: 같은 개인 회차와 사망 수 유지. 탈출 후 재입장: 새 개인 회차 확인.
5. Client 단기 재접속과 Host Migration 후 탈출: 동일 회차/수치 유지, 단절 대기 시간이 플레이 시간에 포함되지 않는지 확인.
6. 돈 정산 성공 뒤 전적 저장 장애를 유도하고 재시도: 돈 재지급 없이 전적 한 번만 확정되는지 확인.
7. 조회 도중 닫기/재열기/계정 전환: 이전 응답 표시 방지 확인.
8. 다양한 해상도에서 전적 목록·상세 글자 넘침과 버튼 배치 확인.

## 제한과 주의

- 기존 지갑 영수증 128개 및 JSON 200KB 안전 제한은 그대로 남아 있다. 이는 전적 100회 보관과 별개이며 장기 운영에서는 영수증 보관 구조를 개선해야 한다. 오래된 전적 삭제만으로 영수증 한도가 해결되지는 않는다.
- 장기 미복귀 회차를 임의로 성공/사망 확정하지 않는다. 장기 미복귀 종료 정책은 미구현이다.
- 처치 귀속은 현재 치명 피해의 유효한 플레이어 공격자이며 환경·자기 피해는 다른 플레이어 처치로 세지 않는다.
- 서버 승인과 Cloud 권한 검증은 악의적인 Host 조작까지 완전히 막는 구조가 아니다.
- 실제 Cloud 배포 전에는 새 조회 함수가 없어서 조회 실패가 표시될 수 있다.
- 커밋·푸시·Cloud 배포는 하지 않았다.

## 코드 학습 채팅 전달용

코드 수정 없이 변경된 위치와 동작을 쉽게 설명해 주세요. 위 파일과 현재 실제 코드를 읽고, 행동 확정 → PlayRecordBrick 누적 → 기존 WalletSession Snapshot → 탈출 요약 고정 → 돈 정산 → 전적 저장 → 타이틀 조회 순서로 설명해 주세요. 특히 개인 RoundId와 RunId 차이, 복원과 덧셈의 차이, 전적 100회 삭제와 지급 영수증 보존의 차이, 전적 저장 실패 시 돈을 재지급하지 않는 이유, UI 비동기 응답 세대 검사와 Brick/Glue 역할을 짚어 주세요. Unity EditMode 154개 통과와 미실시 Cloud 실계정 통합 검증을 구분해 주세요.
