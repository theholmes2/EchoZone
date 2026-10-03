# 경찰 배정·펫 회수·마지막 목격 추격 변경

## 범위와 보존

- Brick & Glue, 서버 권위, 기존 EnemyRuntimeUpdateGlue / ManualUpdate 순서를 유지했다. 새 독립 Update나 RPC를 추가하지 않았다.
- 사용자 연결 Pet PlayerHealthBarGlue는 그대로 재사용했다. 체력바 컴포넌트를 추가하지 않았다.
- 기존 폰트 변경 및 작업 중 발견한 NetworkWeaponFireGlue의 별도 변경은 건드리지 않았다.
- 씬과 순찰 지점은 수정하지 않았다. 커밋·푸시는 하지 않았다.
- 사용자가 완료한 기존 테스트: 1 건물 호버 UI, 2 경찰 검사, 6 회수 전 펫 데려가기. 이번 변경의 영향 범위는 아래 회귀 항목으로 다시 확인한다.

## 조사 및 경찰 수

Kenney_City2x2_Online에는 HeistBuildingSite 55개, 경찰서 4곳, 각 6개 지점으로 구성된 약 100.4m 순찰 루프가 있다. 순찰 중심에 가장 가까운 건물 기준 분포는 0 / 5 / 8 / 42개다. 이것은 실제 행정구역이 아니라 현재 순찰 배치의 편중을 측정한 값이다. NavMeshSurface는 PoliceStations에 1개이고 기존 베이크를 재사용한다.

| 경찰서 순서 | 기존 목표 | 새 Config 목표 | 근거 |
|---|---:|---:|---|
| 0 | 5 | 2 | 가까운 검사 건물 없음. 순찰 1 + 지원 1 |
| 1 | 3 | 3 | 건물 5, 순찰 1 + 작업 여유 2 |
| 2 | 3 | 3 | 건물 8, 순찰 1 + 작업 여유 2 |
| 3 | 2 | 5 | 건물 42가 가장 가까운 구역. 작업 2 + 순찰/전투 여유 |
| 총계 | 13 | 13 | 증원보다 배정·분포 개선 우선 |

새 작업 배정 때 같은 경찰서의 비전투 순찰을 최소 1명 남기고, 검사·회수·현장 수색 합계는 최대 2명까지 배정한다. 사망·전투·추격 인원은 가용 순찰로 세지 않는다. 전투 발생 자체를 막거나 전투 경찰을 강제 순찰로 돌리는 규칙은 아니다. 실시간 인원 증감은 하지 않고 고정 목표로 보충한다.

검사 요청은 30초 대기 구간을 우선 비교하고 같은 구간에서는 실제 NavMesh 경로 길이, 건물 ID 순서로 선택한다. 예약 불가·도달 불가 후보를 건너뛴다. 취소할 때 원래 예정 시각을 보존하고 짧은 재시도 유예만 부여하여 먼 건물의 대기 우선순위가 초기화되지 않게 했다. 구역 밖 지원도 허용하므로 첫 구역 인원이 놀거나 마지막 구역 건물이 담당 경찰만 기다리지 않는다.

## 체력바

경찰·펫의 현재 숨김 경로는 HeistInteriorView의 Renderer.forceRenderingOff였다. 별도 경찰 FOV 가시성 Controller는 현재 코드에서 발견되지 않았다. 건물 시스루 셰이더를 적 가시성 시스템으로 취급하지 않았다.

내부 / 시야 / 생명주기 숨김 사유를 독립 비트로 합성하며, 하나가 해제되어도 다른 사유가 남으면 Canvas를 표시하지 않는다. 기존 HealthBarView.LateUpdate에서 모든 외형 Renderer의 enabled/forceRenderingOff도 함께 확인한다. 체력 이벤트나 네트워크 스탯은 끄지 않는다. 시야 표시 기능을 별도로 연결할 경우 SetHidden(Sight, ...)를 사용하면 된다.

펫 모델 전체 높이는 약 1.6m로 확인했다. 기존 공용 체력바(높이 3.2)를 복제 추가하지 않고 펫 전용 표시 Config로 높이 2.0, 기본 크기의 0.7배를 지정했다. 실제 카메라 줌에서의 가독성은 사용자 체감 확인이 필요하다.

## 펫 도주와 회수

- 기본 5회 / 첫 도주부터 최소 30초. 횟수는 새 Fleeing 진입에서만 증가한다.
- 30초가 먼저 지나도 5회 미만이면 정상 도주를 계속 허용한다.
- 5회를 먼저 채우면 여섯 번째 도주는 시작하지 않고 현재 도주를 끝낸 뒤 회수 기회를 기다린다.
- 예외: 한 도주가 20초 안에 안전 위치에 도착하지 못하면 실패로 확정하여 현재 위치에서 대기한다. 현재 NavMesh 밖이면 Config SampleRadius 이내 유효 지점으로 보정한다. 최소 30초는 여전히 보장하며, 이후에는 횟수 미달이어도 CollectionWaiting으로 전환한다. 가까운 NavMesh 자체가 없으면 멀리 순간이동하지 않고 현재 위치에 남으므로 맵 베이크 수정이 필요하다.
- CollectionWaiting은 도주·공격 대상이 아니라 회수 작업 대상이다. 장물이 없어도 경찰이 위치까지 이동해서 회수한다.
- 플레이어 상호작용은 기존 체력 > 0 / 거리 / LOS 조건을 유지한다. 먼저 획득하면 경찰 예약·신고·수거 대기를 취소하고 회차를 초기화한다.
- 서버 회수는 예약 경찰, 실제 거리, 아직 회수 가능한 상태를 재검사한다. 반환·유효 신고자 감사비는 기존 원장을 재사용하고, 미신고 자동 수거에는 보상이 없다.
- retiredPets를 먼저 기록하고 기존 Cloud 체크포인트 확인 후 디스폰 정책을 유지한다. Cloud 저장 실패 중에는 즉시 사라지지 않고 상호작용 불가로 남는 기존 정책이다. 디스폰 시 자식 짐도 함께 정리된다.

## 추격과 마이그레이션

조준용 Collider 중심점과 이동용 마지막 목격 발 위치를 분리했다. 이동 위치는 별도 Config 반경 2m로 NavMesh에 투영한다. 시야 상실 후에는 대상의 숨은 현재 위치를 목적지로 갱신하지 않는다.

Chase(마지막 목격 위치까지 최대 30초, 정체 6초 제한) → 도착 후 Search(기존 5초) → Patrol 순서다. 다시 실제 발견하면 기존 인지·전투 흐름으로 복귀한다. 공격자 추격 제한시간을 보이는 동안 현재 시각으로 덮어쓰던 별도 타이머를 제거했다.

스냅샷에는 펫 도주 횟수·유예/실패 남은 시간·실패 여부·CollectionWaiting 상태, 경찰 이동/도착 수색 단계·남은 시간·정체 여유·조준점, 밀린 검사 대기 시간을 추가했다. 기존 복구 장벽 동안 중앙 갱신이 멈추므로 새 타이머도 정지한다. 경찰 작업 예약은 복원하지 않고 새 호스트에서 재배정한다. 구형 펫 기록은 첫 회차와 충분한 유예로 안전하게 시작하고, 구형 경찰 기록은 마지막 위치 이동부터 다시 시작한다. retiredPets 복원 제외 정책은 그대로다.

## 변경·추가 C#과 핵심 줄

모든 경로는 프로젝트 루트 기준이며 줄 번호는 이번 수정 시점 기준이다.

| 파일 | 추가/변경 | 핵심 줄 / 역할 |
|---|---|---|
| Assets/Script/Heist/PoliceInspectionPriorityBrick.cs | 추가 | 8 대기 우선순위, 17 순찰 잔류 판단 |
| Assets/Script/Heist/PoliceDispatchGlue.cs | 추가 | 23 인원 집계, 38 실제 경로·예약 후보 순회 |
| Assets/Script/Heist/HeistWorldGlue.cs | 변경 | 34 회수 예약, 144 검사 배정, 238 획득 취소, 258 회수 확정 |
| Assets/Script/Heist/PoliceHeistDutyGlue.cs | 변경 | 12 경찰 레지스트리, 91 서버 작업 배정·이동 |
| Assets/Script/Data/HeistConfig.cs | 변경 | 10 검사 대기·최소 순찰·작업 상한 |
| Assets/Script/Data/EnemySpawnConfig.cs | 변경 | 10 구역별 고정 목표 |
| Assets/Script/Enemy/EnemySpawnManager.cs | 변경 | 148 / 257 Config 목표로 스폰 수 판단 |
| Assets/Script/Player/View/HealthBarVisibilityBrick.cs | 추가 | 6 숨김 사유 합성 |
| Assets/Script/Data/HealthBarDisplayConfig.cs | 추가 | 6 모델별 높이·배율 |
| Assets/Script/Player/View/PlayerHealthBarGlue.cs | 변경 | 19 숨김 연결, 29 기존 바 재사용 |
| Assets/Script/Player/View/HealthBarView.cs | 변경 | 28 Canvas 표시, 34 외형 표시 합성 |
| Assets/Script/Heist/HeistInteriorView.cs | 변경 | 20 내부 표시와 체력바 연결 |
| Assets/Script/Pet/PetEscapeEpisodeBrick.cs | 추가 | 21 진입 횟수, 28 남은 시간, 37 회수 판정 |
| Assets/Script/Data/PetBehaviourConfig.cs | 변경 | 12 횟수·유예·실패 시간 |
| Assets/Script/Pet/PetStateGlue.cs | 변경 | 118 도주 시작, 136 중앙 갱신, 211 수거 대기 |
| Assets/Script/Heist/PetHeistGlue.cs | 변경 | 73 장물 없는 수거 대상 허용 |
| Assets/Script/Heist/HeistHudGlue.cs | 변경 | 55 수거 대기 펫에도 기존 상호작용 표시 |
| Assets/Script/Enemy/PolicePursuitBrick.cs | 추가 | 18 이동과 도착 후 수색 시간 분리 |
| Assets/Script/Enemy/PoliceLastKnownPursuit.cs | 추가 | 19 지면 기억, 27 마지막 목격 위치 추격 |
| Assets/Script/Data/PoliceEnemyConfig.cs | 변경 | 10 추격 이동·정체·지면 투영 설정 |
| Assets/Script/Enemy/PoliceEnemyBrainGlue.cs | 변경 | 269 기존 갱신 연결, EvaluatePerception / SelectState / TickSearch |
| Assets/Script/Enemy/PoliceMigrationState.cs | 변경 | 12 / 32 추격 단계 보존 |
| Assets/Script/Pet/PetMigrationState.cs | 변경 | 9 / 20 도주 회차 보존 |
| Assets/Script/Heist/HeistWorldMigration.cs | 변경 | 19 / 36 검사 누적 대기 보존 |
| Assets/Script/Online/Migration/SessionWorldSnapshot.cs | 변경 | 35 검사 경과, 43 이후 새 개체 상태 필드 |
| Assets/Script/Online/Migration/SessionWorldSnapshotValidator.cs | 변경 | 12 / 33 새 시간·횟수 검증 |
| Assets/Tests/Editor/PolicePetPolicyTests.cs | 추가 | 12 이후 새 규칙 회귀 테스트 17개 |

## 에셋 / 하이어라키

- 변경: Assets/Data/Enemy/EnemySpawnConfig.asset, PoliceEnemyConfig.asset
- 변경: Assets/Data/Heist/HeistConfig.asset
- 변경: Assets/Data/Pet/PetBehaviourConfig.asset
- 추가: Assets/Data/Pet/PetHealthBarDisplayConfig.asset
- 변경: Assets/Prefabs/Pet/NetworkDogPet.prefab의 기존 PlayerHealthBarGlue.displayConfig 연결. 기존 healthBarPrefab 연결 및 사용자 컴포넌트는 유지했다.
- 씬 오브젝트 추가/삭제 없음. 경찰 프리팹 신규 컴포넌트 없음. 기존 NavMesh 베이크와 중앙 갱신을 재사용했다.

## 자동 검증

- EditMode: 새 PolicePetPolicyTests 17 / ExtendedMigrationTests 12 / HeistRulesTests 7 / PoliceInvestigationTests 7 / WalletSessionTests 4, 합계 47개 통과.
- 실제 씬 로컬 호스트 검증: 22개 점검 통과. 총 13명과 2/3/3/5 배치, 첫 문 예약 실패 후 두 번째 건물 선택, 펫/경찰 남은 시간 복원, 무장물 수거 대상, 경찰 중복 예약 차단, 실제 플레이어 상호작용 선점과 예약 해제, 무장물 회수 완료 및 중복 거절, 실내/시야 중첩 체력바 복구, 단일 체력바 유지 및 재활성화 재사용을 확인했다.
- 초기 스폰이 끝나기 전에 호출한 점검은 실패하여 스폰 완료 후 재실행했다. 예약표만 먼저 연결된 상황에서 획득 후 예약표가 남는 문제를 발견해 NotifyAcquired에서 작업 취소와 예약 해제를 각각 수행하도록 수정했고, 최신 어셈블리 임포트 후 재검증했다.
- 실제 Relay 멀티플레이 호스트 교체, Cloud 저장 성공 후 디스폰, 장시간 인원 분포와 체감 검사는 이번 로컬 자동 검증에서 수행하지 않았다. 테스트 플레이는 종료했으며 런타임에서 바꾼 건물 기한·위치·펫 상태는 씬에 저장하지 않았다.

## 직접 확인할 회귀·체감 항목

1. 기존 완료 1번: 건물 호버 UI의 검사 예정/이동/검사 표시가 여전히 맞는지 확인.
2. 기존 완료 2번: 여러 건물의 검사를 오래 관찰하여 먼 건물도 처리되는지, 구역별 순찰이 지나치게 비지 않는지 확인. 인원 목표는 임시 균형값이며 전투량에 따른 부족 여부를 체감해야 한다.
3. 펫과 경찰 실내 입퇴장 시 양쪽 클라이언트 체력바가 외형과 함께 숨고 복구되는지 확인. 펫 높이 2m / 크기 0.7배의 가독성 확인.
4. 펫 정상 반복 도주 5회와 30초 조건의 순서가 바뀌어도 여섯 번째 도주가 없는지 확인. 안전 경로 실패 20초 예외를 따로 확인.
5. 수거 대기에서 경찰이 실제 도착하는지, 경로 막힘·담당 경찰 사망/전투 전환 후 재배정되는지 확인. 신고/미신고 장물 및 무장물 펫을 각각 확인.
6. 기존 완료 6번: 경찰 도착 직전 원 주인/다른 플레이어가 먼저 가져간 경우 취소되는지 양쪽 클라이언트에서 재확인.
7. 경찰을 공격하고 골목 뒤로 숨기: 마지막 목격 위치까지만 이동하고 도착 후 5초 수색하는지 확인. 벽 뒤 실제 위치를 따라오면 실패.
8. 도주 / 수거 대기 / 마지막 목격 이동 / 도착 후 수색 중 각각 실제 호스트 강제 종료. 새 호스트에서 횟수·남은 시간이 이어지고 중복 경찰 예약이 없는지 확인.
9. 실제 Cloud 체크포인트 완료 후 회수 펫과 짐이 디스폰되는지, 이후 호스트 교체 때 복원되지 않는지 확인. 로컬 테스트는 실제 Cloud 저장·네트워크 전달 검증을 대체하지 않는다.
