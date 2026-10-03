# 게임 효과음 기반 구현

2026-10-03. 현재 온라인 도시 씬에 연결 완료. 실제 AudioClip은 미제공 상태다.

## 사용법과 연결

`Assets/Data/Audio/GameplaySoundConfig.asset`의 Entries를 펼쳐 ID별 Clip에 음원을 넣는다. Volume/Pitch, 3D MinDistance/MaxDistance를 여기서 조절한다. 다른 Glue에 클립 필드는 추가하지 않았다.

`Assets/Scenes/Kenney_City2x2_Online.unity` 루트에 `GameplaySoundSystem` 프리팹 인스턴스를 추가했다. 원본은 `Assets/Prefabs/GameplaySoundSystem.prefab`이다. 별도 NetworkObject가 필요하지 않은 로컬 표시 시스템이다.

- UI_2D: 4개 AudioSource, spatialBlend 0.
- Weapon_3D: 16개, spatialBlend 1.
- World_3D: 24개, spatialBlend 1.
- Config와 모든 Source 배열을 직렬화했다. 실제 씬 AudioListener는 Gameplay Camera에 1개인 것을 확인했다.
- 다른 게임 씬에서도 사용하려면 동일 프리팹을 하나 배치한다. 현재 요청 범위인 온라인 도시 씬만 연결했다.

## 역할과 신규 파일

- `Assets/Script/Audio/GameplaySoundId.cs:4`: 14개 enum ID 및 3개 채널. 문자열 식별자는 사용하지 않는다.
- `Assets/Script/Audio/GameplaySoundBrick.cs:10`: 순수 ID 인덱스 조회, 중복 첫 항목 선택, 채널 선택, 안전한 수치 보정. Unity 참조 없음.
- `Assets/Script/Audio/GameplaySoundConfig.cs:33`: 클립/기본값 데이터. 중복 제거, 누락 ID 추가, 볼륨·피치·거리 보정. 미설정 Clip은 정상 상태다.
- `Assets/Script/Audio/GameplaySoundGlue.cs:51`: 실제 PlayOneShot. 빈 소스를 찾아 위치·피치를 지정하며 재생 중 소스는 재배치하지 않는다. 빈 클립/전용 서버는 조용히 생략한다. 독립 Update 없음.
- `Assets/Tests/Editor/GameplaySoundTests.cs`: 신규 10개 테스트 케이스.
- `AgentScripts/SetupGameplaySound.cs`: CLI eval_file용 설치 기록. 재실행 시 기존 Config/프리팹을 덮어쓰지 않는다. 기존 CreateTitleMenu.cs와는 별개다.
- 신규 Config/프리팹/스크립트의 Unity .meta 포함.

## 기존 코드에 삽입한 확정 이벤트

| 파일·줄 | 이벤트 및 재생 대상 |
|---|---|
| NetworkWeaponFireGlue.cs:235,321 | 투사체 Spawn·초기화·탄약 Commit 뒤 기존 발사 RPC에 서버 총구 위치 전달. 수신 클라이언트마다 PlayerShot/PoliceShot 한 번. 입력 요청이나 서버 로컬 경로에서는 별도로 재생하지 않음. |
| PlayerHeistGlue.cs:52,59 | 서버 전용 NotifySoundServer → Owner 전용 RPC → UI 재생. 복원 중 요청은 생략. |
| PlayerHeistGlue.cs:85 | 절도 요청 승인/거부, 펫 획득·신고 요청 승인/거부. TheftStarted는 실제 실내 작업 시작이 아니라 절도 명령 승인 시점. |
| PlayerHeistGlue.cs:38,49 | 현상금·감사비 지급 결과. |
| PetHeistGlue.cs:100,123 | 작업 요청자 보존. CompleteTheft 반환값 확정 후 성공/실패를 원래 요청자에게 전송. |
| PetHeistGlue.cs:129 | 진행 중 작업의 취소·적발·도주·시간초과 실패를 한 번 통지. 성공 후 정리/복원에는 CancelServer(false)로 중복 실패음 방지. |
| HeistWorldGlue.cs:94,114 | WantedPlayers.OnListChanged의 로컬 ClientId 추가/삭제 이벤트. 기존 수배 단계는 없으므로 단계 상승음은 만들지 않음. 초기 목록과 Clear/Full 이벤트는 재생하지 않음. |
| PlayerInteractionGlue.cs:117 | 서버 interactionLock.Execute의 결과. 실제 획득이면 ItemAcquired, 기타 상호작용 성공/실패는 공통 UI. |
| PlayerWalletGlue.cs:149 | BeginEscape 성공 후 ExtractionStarted. |
| PlayerWalletGlue.cs:260 | Cloud 정산 + 최종 체크포인트 확인 후 SettlementSucceeded. |
| PlayerWalletGlue.cs:267 | 정산 실패 횟수 1일 때만 SettlementFailed. 자동 재시도마다 반복하지 않음. |

표의 기존 파일 경로: 발사는 Assets/Script/Combat/Glue, 상호작용은 Assets/Script/Player, 나머지는 Assets/Script/Heist 아래다.

## 확인 결과

- Unity 컴파일 성공, 오류 없음.
- EditMode 전체 102/102 통과. 기존 92 + 신규 10.
- ID 중복, 채널 분리, NaN/무한대/범위 밖 피치, Config 누락 보정, 미배치 시스템, 빈 클립, 프리팹 배열의 중복 참조/공간 설정/자동재생 방지 검증.
- 활성 리스너 1개, 씬 사운드 시스템 1개 확인.
- 사운드 관련 수정 파일 diff 공백 오류 없음. 기존 TMP 파일의 공백 변경은 범위 밖으로 보존했다.
- 실제 AudioClip을 듣는 청각 테스트 및 두 클라이언트의 실전 사운드 RPC 통합 테스트는 미실시. 컴파일/단위 테스트를 멀티플레이 검증으로 간주하지 않는다.
- 커밋/푸시/Cloud 배포 없음. 기존 TMP 아틀라스·AI 설정·CreateTitleMenu.cs를 의도적으로 변경하거나 정리하지 않았다.

## 제한과 후속 검증

- 풀 포화 시 새 소리를 버린다. 40명 경찰의 장시간 연사에서 필요하면 World 풀을 조절하거나 거리·우선순위 기반 보이스 선택을 추가한다. 지금은 단순 빈 소스 정책이다.
- AudioMixer 그룹/사용자 음량 메뉴/BGM/루프는 추가하지 않았다. 루프는 PlayOneShot 경로에 억지로 섞지 않고 추후 별도 Start/Stop 수명주기를 추가해야 한다.
- 요청 쿨다운·복원 중·연결 종료 등에서 조기 무시된 패킷은 소리를 내지 않는다. 정상 요청의 실제 결과만 연결했다.
- 사운드 시스템은 씬 수명이다. 씬 전환 때 재생 중 소리는 종료된다. RPC 형식이 변경됐으므로 모든 플레이어를 같은 빌드로 다시 실행한다.
- 한 건물 절도 완료음이 다른 플레이어에게 들리지 않는지, Host 총성이 두 번 나지 않는지, 원격 경찰의 실제 총구 위치에서 들리는지 확인한다.
- 펫 신고·획득 성공/실패, 절도 시간초과/적발, 수배 시작/해제, 일부 아이템 획득, 탈출·정산 실패 재시도에서 반복음 여부를 확인한다.
- 카메라 높이와 줌에 따라 3D 감쇠 체감이 달라진다. 실제 클립을 넣은 뒤 음량·거리·동시 발사 수를 조절한다.
