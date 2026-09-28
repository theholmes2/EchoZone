# 건물 절도·수색·수배 구현 및 테스트 (2026-09-22)

> 2026-09-24 후속: 탈출 계정 지갑과 3인 동기화/후발 입장/타인 현상금 검증이 추가됐다. 아래는 당시 기록이며 최신 완료·미완료 구분은 `ExtractionWalletProgressAndTests.md`를 우선 참고한다.

## 현재 규칙

- 문 앞에서 절도 지시 → 펫 이동 → 외형을 숨긴 실내 작업 8초 → 돈을 싣고 복귀. 실제 건물 내부 공간은 아직 없다.
- 건물당 초기 10,000, 1회 절도 1,000, 펫 적재 한도 5,000.
- 1등급 힘 1 / 2등급 힘 1.5 / 3등급 힘 2. 속도 배율은 `1 - 최대감속 × 적재비율 / 힘`이다.
- 돈 1,000을 든 기본 펫은 87%, 5,000이면 35% 속도. 이동·추종·도주에 적용한다.
- 돈 부족만 발견하면 60초 동안 현장 반경 20m 수색. 내부 원장에 도둑 ID가 있어도 그 정보로 자동 수배하지 않는다.
- 경찰과 절도 중 펫이 같은 건물 내부에 겹치면 펫을 압수(디스폰)하고 주인을 수배한다. 펫이 이미 운반하던 돈은 원래 건물로 반환한다. 아직 완료하지 못한 절도는 돈을 빼지 않는다.
- 경찰이 이미 검사 중인 건물에 펫이 들어가도 적발된다.
- 수배 전에는 경찰의 공격 후보가 아니다. 수배 후에도 기존 발견 게이지가 1에 도달해야 전투한다. 대상 변경·수색 종료 시 얼굴 확인을 초기화한다.
- 수배자의 HP가 0이 되면 체포로 취급하여 수배를 해제한다. 유효한 다른 플레이어의 치명타이면 500을 한 번 지급한다. 경찰·환경·자해는 플레이어 현상금을 지급하지 않는다.
- 현장 적발 수배는 장물 반환만으로 해제되지 않는다.
- 주인 사망 후 남은 장물 펫은 가져가기 또는 신고 가능. 경찰이 실제 회수해야 건물 반환과 5% 감사비가 발생한다. 도둑·운반자는 자기 장물 신고 보상에서 제외한다.

## 설정 위치

- `Assets/Data/Heist/HeistConfig.asset`: 현금, 절도 시간, 검사 간격, 수색 시간·반경, 감속, 등급별 힘, 현상금, 감사비.
- `Assets/Prefabs/Pet/NetworkDogPet.prefab` → `PetHeistGlue.grade`: 펫 등급 (기본 1).
- 기존 경찰 `PoliceEnemyConfig` → `DetectionGainPerSecond`, `DetectionLossPerSecond`: 얼굴 확인 게이지 속도.

## 코드 역할과 연결

| 파일 | 역할 / 변경 |
|---|---|
| `Assets/Script/Heist/HeistLedgerBrick.cs` | 순수 장물 원장. 도난 발견과 신원 확인을 분리하고 수배·체포 중복 방지 |
| `Assets/Script/Heist/PetLoadBrick.cs` (이번 추가) | 순수 적재 속도 배율 계산 |
| `Assets/Script/Heist/CrimeSearchBrick.cs` (이번 추가) | 순수 원형 수색 후보 위치 계산 |
| `Assets/Script/Data/HeistConfig.cs` | 공통 ScriptableObject 설정 |
| `Assets/Script/Heist/HeistWorldGlue.cs` | 서버 잔액·원장·수배·검사·압수·반환·보상 연결 |
| `Assets/Script/Heist/HeistBuildingState.cs` | 돈·검사·수색 만료 시각 동기화 |
| `Assets/Script/Heist/HeistBuildingSite.cs` | 씬 건물 번호와 출입구 |
| `Assets/Script/Heist/PetHeistGlue.cs` | 펫 작업 단계·운반금·등급 배율·실내 적발 요청 |
| `Assets/Script/Heist/PoliceHeistDutyGlue.cs` | 검사·장물 회수·현장 수색 NavMesh 실행 |
| `Assets/Script/Heist/PlayerHeistGlue.cs` | 소유자 RPC 요청 검증·사망 이벤트·보상 지갑 |
| `Assets/Script/Heist/HeistInteriorView.cs` | 입장 중 외형/피격 Collider 숨김 |
| `Assets/Script/Heist/HeistHudGlue.cs`, `HeistHudView.cs` | 기존 uGUI HUD와 요청 연결, 수색·수배·보상 표시 |
| `Assets/Script/Pet/PetFollowGlue.cs` | 이동 속도에 적재 배율 적용 |
| `Assets/Script/Pet/PetStateGlue.cs` | 작업 중 추종 양보, 도주/대기 시 절도 취소, 작업 도중 압수 디스폰 대응 |
| `Assets/Script/Pet/PetUpdateManager.cs` | 활성 펫 목록 제공 (앞선 작업) |
| `Assets/Script/Enemy/PoliceEnemyBrainGlue.cs` | 비수배 후보 제외, 얼굴 확인 후 전투, 전투 외 검사·수색 |
| `Assets/Script/Enemy/EnemyRuntimeUpdateGlue.cs` | 기존 갱신 루프에 HUD 연결 (앞선 작업) |
| `Assets/Script/Combat/Glue/ProjectileNetworkGlue.cs` | 경찰의 비수배 오발 피해 차단, 피해 수신부에 공격자 전달 |
| `Assets/Script/Combat/Glue/DamageReceiverGlue.cs` | 동기 체력 이벤트 동안만 CurrentDamageSource 제공 후 finally에서 초기화 |
| `Assets/Script/Combat/Glue/PlayerAimInputGlue.cs` | HUD 클릭 시 발사 차단 (앞선 작업) |
| `Assets/Tests/Editor/HeistRulesTests.cs` (이번 추가) | 순수 규칙 자동 테스트 7개 |

새 독립 Update나 NavMesh 재베이크는 추가하지 않았다. Unity Navigation·uGUI 스킬 기준에 따라 기존 이동 루프와 Canvas를 재사용했다.

## 씬·프리팹 연결 (중단 전 작업 포함)

- `Kenney_City2x2_Online`: `HEIST_SYSTEM`의 NetworkObject/HeistWorldGlue, `HEIST_ENTRANCES`, 건물별 HeistBuildingSite, `HeistHUD`, `HeistEventSystem`.
- 도달 가능한 건물 55개 연결. 전체 배치 중 나머지 19개는 문 앞 연결/기존 NavMesh 도달 문제로 미연결.
- 펫 프리팹: PetHeistGlue + HeistInteriorView.
- 경찰 프리팹: PoliceHeistDutyGlue + HeistInteriorView.
- Kenney 플레이어 프리팹: PlayerHeistGlue.
- 이번 재개에서는 기존 연결을 유지하고 Config 필드를 저장했으며 HUD 버튼 두 개의 문구를 짧게 수정했다.

## 에이전트 검증 완료

- Unity 실제 컴파일 성공, 종료 포함 Console Error 0.
- EditMode 순수 규칙 테스트 7/7 통과: 비자동수배, 새 도난 발견, 현장 적발, 반환 후 수배 유지, 단일 체포, 신고 중복/참여자 차단, 등급·적재 속도.
- localhost Host 실제 요청 RPC → 이동/절도 완료: 건물 10,000→9,000, 펫 1,000, 속도배율 0.87, 수배 0.
- 실제 경찰 검사 완료 → 수배 0, 경찰 현장 수색 활성화.
- 서버 검사 진입을 호출한 통합 테스트 → 절도 중 펫 디스폰, 수배 등록, 기존 운반금 반환, 건물 10,000 복구.
- 경찰 갱신의 deltaTime을 테스트용으로 진행: 게이지 0.008에서 Suspicious, 1에서 Combat.
- 실제 DamageReceiver/체력 이벤트: HP0 수배 해제, 자해 보상 0, 피해 출처 초기화.
- 테스트 경찰 고정/시간 변경은 런타임에만 적용하고 Play 종료로 폐기했다. 클라우드 쓰기는 하지 않았다.

## 사용자가 직접 할 테스트 (이번 기능은 아직 미확인)

1. Host A + Client B 입장. 아무 절도도 하지 않고 경찰 앞을 지나기: 공격하지 않아야 한다.
2. 가까운 건물 문에서 `절도 지시`: 펫 이동→숨김→8초 후 등장, 건물 돈 감소/펫 돈 증가가 양쪽에서 같아야 한다.
3. 여러 번 훔쳐 적재 증가에 따른 감속 확인. 같은 짐에서 grade 2/3 프리팹은 덜 느려져야 한다.
4. 절도 완료 후 검사: 범인 이름이 수배에 뜨지 않고 수색 표시/경찰 주변 이동만 발생해야 한다.
5. 검사 입장과 펫의 절도 시간을 겹치기: 펫 사라짐, 주인만 수배. 반대로 이미 검사 중인 건물에 보내도 적발되어야 한다.
6. 수배자에게 경찰 시야를 짧게만 노출: 즉시 사격 금지. 지속 노출 후 게이지가 차면 공격. 벽 뒤로 숨기/대상 교체/놓친 후 재발견도 확인.
7. B가 수배자 A를 총으로 쓰러뜨리기: B 현상금 +500 한 번, A 수배 해제/기존 부활. 시체 추가 사격으로 돈이 늘면 안 된다. 경찰 처치·자해에는 B 보상 없음.
8. 장물을 든 펫 주인 사망: 펫 대기. 다른 플레이어가 가져가면 돈 유지/새 주인 추종. 여러 마리 소유도 확인.
9. 다른 플레이어가 그 펫을 신고: 즉시 돈 지급 금지. 경찰 회수 후 원래 건물별 돈 반환 + 신고자 5% 한 번. 도둑 본인/이미 가져간 사람은 보상 0.
10. 절도 중 주인 사망·퇴장, 경찰 검사 도중 경찰 사망: 작업이 취소되고 숨김·예약이 풀려야 한다.
11. 늦은 Client 참가: 건물 돈/수배/실내 숨김/운반금이 일치해야 한다. 별도 PC 빌드에서도 확인.

검사 테스트 대기 시간을 줄이려면 실행 전에 Config의 FirstInspectionDelay/InspectionInterval/InspectionStagger를 임시 조절하고 테스트 후 원복한다. 건물별 실제 검사 배정은 경찰 이동/다른 작업 때문에 표시된 예정 시각보다 늦을 수 있다.

## 아직 연결하지 않은 범위

- 새 건물 돈·펫 짐·수배·보상금·수색 상태는 현재 세션 메모리다. 기존 Cloud Snapshot/Host Migration 복원에 아직 포함하지 않았다. 호스트 교체 시 이 기능의 보존을 성공 기준으로 삼으면 안 된다.
- 연결 종료 후 계정별 지갑, 재접속 시 ClientId 변경에 대응하는 영속 수배, 현상금 담합 방지는 후속 작업이다.
- 이번 실행은 단일 localhost Host 검증이다. 원격 동기화, 다른 플레이어 현상금 실제 지급, 실경찰 장물 회수/타인 감사비 지급은 아직 실행 검증하지 않았다.
- 백그라운드 테스트에서는 HUD의 최종 화면 배치/애니메이션 품질을 눈으로 검증하지 않았다.
