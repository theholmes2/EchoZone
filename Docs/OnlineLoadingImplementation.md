# 온라인 로딩·재시도 구현 결과

## 변경 파일과 핵심 위치

프로젝트 루트: `E:/GameDev/unity-main-portfolio/EchoZone`

| 파일 | 핵심 위치·역할 |
| --- | --- |
| Assets/Script/Online/Loading/LoadingProgressBrick.cs | 37: 작업 세대 시작, 47: 중복 없는 단계 완료, 58: 실제 성공, 64: 실패 세대 무효화, 74: 성공 후 보간 |
| Assets/Script/Online/Loading/LoadingOverlayGlue.cs | 57: 표시 작업 시작, 72: 미완료 SDK 추적, 91: 기존 갱신에서 표시, 122: 재시도, 130: 타이틀 |
| Assets/Script/Online/Loading/OnlineLoadingConfig.cs | 8 이후: 표시 지연·보간·단계별 제한 시간·가중치 |
| Assets/Script/Online/Authentication/UnityAuthenticationGlue.cs | 43: 공유 서비스 초기화와 로딩 연결 |
| Assets/Script/Online/Authentication/AccountLoginGlue.cs | 73: 로그인·회원가입 작업 연결 및 중복 차단 |
| Assets/Script/Online/Authentication/UnityAccountAuthenticationService.cs | 70 이후: 인증 응답 적용 전 작업 유효성 확인 |
| Assets/Script/Online/Relay/RelaySessionLoading.cs | 48: 입장 작업, 99 이후: 플레이어·지갑·월드 준비 대기, 146 이후: 복구 재시도, 170: 타이틀 정리 |
| Assets/Script/Online/Relay/RelaySessionGlue.cs | 190: Migration 복원, 345: 생성, 427: 참가 및 완료 단계 통지 |
| Assets/Script/Online/Relay/UnityRelaySessionService.cs | 26: 요청 무효화, 30 이후: 로컬 분리와 늦은 Session 정리, 380: Migration 핸들러 수명 |
| Assets/Script/Online/Migration/HostMigrationSessionDataHandler.cs | 22: 종료한 연결의 Snapshot 콜백 차단 |
| Assets/Script/Online/Migration/HostMigrationCloudCheckpointGlue.cs | 29: 자동 저장 루프 세대 종료 |
| Assets/Script/Online/Migration/SessionWorldMigrationGlue.cs | 39: 복원 시작 시 월드 준비 해제 |
| Assets/Script/Heist/HeistWorldGlue.cs | 14: 서버가 복제하는 월드 준비 상태, 16: 서버만 변경 |
| Assets/Script/Network/NetworkStartUI.cs | 76 이후: 로딩 갱신·메뉴 차단·타이틀 유지, 입장 응답 세대 확인 |
| Assets/Tests/Editor/LoadingProgressTests.cs | 로딩 관련 9개 자동 테스트 |
| AgentScripts/SetupLoadingOverlay.cs | 일회성 Unity 에디터 연결 스크립트. 런타임 기능 아님 |

새 Unity 스크립트·폴더·Config의 `.meta`도 생성되었다. 기존 사용자 변경(경찰 코드, 기타 리소스 등)은 이번 기능 변경과 별개다.

## 에셋 연결

- `Assets/Data/Online/OnlineLoadingConfig.asset`: 조절값.
- `Assets/Prefabs/UI/TitleMenu.prefab`: LoadingOverlay, 단계 텍스트, 진행 Slider, 재시도·타이틀 버튼 추가.
- `Assets/Scenes/Kenney_City2x2_Online.unity`: NetworkSystem의 LoadingOverlayGlue에 Config와 실제 UI 참조 연결.
- 기존 계정 패널·방 메뉴·코드 표시 구조 유지. 별도 Update를 추가하지 않고 NetworkStartUI의 갱신 사용.

## 흐름

초기화/로그인/회원가입 → 실제 인증 완료 → 방 메뉴 → 지갑 확인 → Session·Relay·NGO 연결 → 로컬 플레이어 및 지갑, 서버 월드 준비 확인 → 로딩 종료.

재접속/Migration → 기존 Session·호스트 상태 확인 → 네트워크 복구 → Snapshot·플레이어·월드 준비 확인 → 종료.

SDK가 하나의 작업으로 제공하는 Session·Relay·NGO 연결은 한 단계로 묶었다. 전송 바이트 진행률이 아니라 실제 완료 단계의 가중치다. 완료 단계는 90%까지 반영하고 최종 성공 후에만 100%로 보간한다. 기본 표시 지연은 0.2초, 마지막 보간은 0.3초다.

## 늦은 응답과 재시도

- UI 작업 ID, Relay 작업 세대, SDK 요청 세대로 이전 결과를 거절한다.
- SDK Task 자체를 취소할 수 없으면 종료할 때까지 새 요청을 잠근다. 실패 화면만 지우고 새 요청을 병렬 실행하지 않는다.
- 폐기한 요청이 늦게 Session을 반환하면 해당 멤버십을 정리한다. 타이틀 상태에서 뒤늦게 NGO가 시작하면 종료한다.
- 동일 Session 복구 재시도는 Refresh/Reconnect로 현재 호스트를 확인한다. 강제 호스트 선출을 새로 실행하지 않는다.
- 요청 세대와 Snapshot 핸들러 수명을 분리했다. 같은 Session을 유지하는 재시도가 향후 Migration 데이터 연결을 영구 무효화하지 않도록 했다.
- 비밀번호는 재시도용으로 보관하지 않는다. 인증 재시도는 비밀번호 입력 화면으로 돌아간다.

## 타이틀 복귀

작업 무효화 → 자동 저장 루프 중지 → NGO 종료·로컬 보호 상태 해제 → 본인의 Session Leave 시도 → 기본 최대 5초 대기 → 로컬 Session·이벤트·티켓·복원 상태 정리.

같은 실행의 인증 계정과 영구 Cloud 지갑·Revision·완료 영수증은 삭제하거나 되돌리지 않는다. 진행 중이던 Cloud 저장을 취소·롤백했다고 가정하지 않는다.

공유 Lobby 삭제나 다른 Client 강제 퇴장을 호출하지 않는다. Host가 Leave하면 남은 Client의 호스트 교체는 기존 Unity Session 정책을 따른다. 실제 남은 Client의 복구 성공은 이번 변경 후 별도 멀티 검증이 필요하다.

## 검증 결과

- 최신 Unity 재컴파일: failed=false, errors=[] 확인.
- EditMode: 139개 모두 통과. 이 중 신규 로딩 관련 9개.
- 신규 테스트: 90% 상한, 성공 후 100%, 중복 단계, 이전 세대 거절, 버튼 허용, 표시 지연, 순서가 바뀐 완료, 미완료 SDK 잠금, 타이틀 복귀 후에도 미완료 SDK 잠금 유지.
- PlayMode: 초기화 후 미로그인 패널, 모의 실패 로딩 표시, 실제 타이틀 버튼 동작 확인. 실제 Session 오류를 재현한 테스트는 아님.
- 실계정 신규 로그인 성공·Host/Client 접속·재접속·Migration 통합 검증은 미실시. 이전 사용자가 완료한 Migration 테스트는 이번 변경 전 결과다.
- Cloud Code 변경·배포는 이번 작업에 없음. 커밋·푸시 안 함.

## 남은 수동 테스트

Host와 Client를 모두 종료 후 동일 최신 코드로 실행한다. 월드 준비 NetworkVariable이 추가되어 구버전 혼용은 피해야 한다.

1. 실행 직후 초기화 중 메뉴 잠금, 완료 후 로그인 패널. 잘못된 입력과 비밀번호 실패 시 오류·재시도 확인.
2. 로그인 성공 → 방 생성 → 실제 플레이어·지갑·월드 준비 후 로딩 종료 확인.
3. 다른 계정 Client 참가. 정상 코드와 잘못된 코드 각각 확인. 버튼 연타 시 중복 참가가 없어야 함.
4. Client 일시 단절·재연결: 로딩 후 복구, 돈과 아이템 유지 확인.
5. Host 종료: 남은 Client의 Migration 로딩 및 Snapshot 복구 후 종료 확인. 복원 전 100%가 나오면 실패.
6. 오프라인/시간 초과: 실패 후 재시도 잠금과 타이틀 복귀 확인. 늦게 성공 응답이 와도 자동 게임 진입하면 실패.
7. 다시 온라인이 된 뒤 SDK 정리 완료 후 재입장. 동일 계정 잔액·정산 중복 여부 확인.
8. Host의 타이틀 복귀가 남은 Client 전체 강제 퇴장으로 이어지지 않는지 확인.

## 제한과 회귀 위험

- SDK 요청이 영원히 끝나지 않으면 타이틀은 돌아갈 수 있어도 새 온라인 요청은 계속 잠긴다. 앱 재실행이 필요할 수 있다. 병렬 재시도로 전역 SDK 상태를 깨뜨리지 않기 위한 보수적 정책이다.
- 회원가입 응답을 잃어도 서버에서 가입이 완료됐을 수 있다. 무조건 재가입을 반복하기보다 같은 계정 로그인을 확인해야 한다.
- Session·Relay·NGO는 SDK의 묶인 작업이므로 실패 화면은 현재 묶음 단계와 SDK 오류를 보여준다. 모든 내부 실패 원인을 개별 단계로 확정 분류하지는 않는다.
- 더 단순한 대안은 오류 시 앱 재시작만 허용하는 방식이다. 코드 수는 줄지만 같은 계정·Session을 유지한 안전한 재시도 경험을 포기하게 된다.

## 코드 학습 채팅 전달용

EchoZone 온라인 로딩·재시도·타이틀 복귀를 구현했습니다. 위 파일 표와 실제 최신 줄 번호를 확인해주세요. LoadingProgressBrick이 단계와 작업 세대를, LoadingOverlayGlue가 UI와 미완료 SDK 잠금을, RelaySessionLoading이 입장·복구·타이틀 정리를 담당합니다. 인증·지갑·Snapshot을 중복 구현하지 않고 기존 완료 지점에 연결했습니다. 최신 컴파일과 EditMode 139개는 통과했지만 실제 멀티 재접속·Migration은 재검증이 필요합니다.

“코드는 수정하지 말고, 변경된 파일과 줄 번호를 확인한 뒤 로그인·방 생성·방 참가·재접속·Host Migration의 로딩 흐름을 쉬운 순서로 설명해주세요. 특히 operation generation으로 늦은 응답을 막는 부분과 재시도·타이틀 복귀 정리 순서를 코드 중심으로 설명해주세요. 자동 테스트와 실제 플레이에서 확인하지 못한 부분도 구분해주세요.”
