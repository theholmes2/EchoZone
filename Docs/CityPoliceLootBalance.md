# 경찰·절도 건물 조정 및 주기 수입

적용 씬: `Assets/Scenes/Kenney_City2x2_Online.unity` (기존 파일명 유지).
커밋·푸시하지 않았으며 기존 사용자 변경을 보존했다.

## 적용값

- 경찰서당 10명, 총 상한 40명. 일반 작업(검사·회수·현장 수색)은 경찰서당 최대 4명, 배정 후 비전투 순찰 최소 2명 유지. 전투로 빠지는 인원은 별도이므로 언제나 6명이 순찰한다는 보장은 아니다.
- 순찰 경로: 기존 6점/약 100.4m에서 각 12점/약 540m로 확대. 중앙 도로를 사이에 두고 인접 구역과 겹치는 네 경로. 기존 경찰서/스폰 위치는 유지하며 초기에는 경찰서에서 각 경로로 이동하므로 퍼지는 데 시간이 걸린다.
- 기존 NavMeshSurface 1개(PoliceStations, agent type 0, baked) 재사용. 씬 내 NavMeshAgent/Obstacle/Link/Modifier는 편집 시 0개였으며 Agent는 기존 경찰 프리팹에서 생성된다. 지형 변경·재베이크 없음.
- 검사 소요시간 5초 → 1초. 검사 완료 후 간격 180초, 최초 유예 120초는 유지.
- 55개 건물 중 16곳만 절도/정기 검사 대상. 나머지 39곳은 새 세션 시작 금액 0, 절도 불가. 식별자와 원장 항목은 삭제하지 않아 기존 저장 참조를 보존한다. 구형 스냅샷에 남아 있는 금액도 임의 삭제하지 않지만 비대상 건물의 절도는 차단한다.
- 선택 건물 ID: 0, 2, 6, 12, 15, 17, 18, 23, 24, 28, 30, 33, 43, 46, 51, 53. 공간 격자별 선택 후 최대 간격 후보로 보충하여 16곳을 분산했다.
- 문 앞 금빛 원판 포탈 16개. 탈출 원판과 구별되는 금색이며 충돌체·네트워크 오브젝트 없음. 표시 의미는 ‘절도 대상 건물’; 잔액 0이 되어도 표시는 유지한다. 실제 잔액은 호버 UI에서 확인한다.
- 절도 대상 건물만 30초마다 500 보충, 자동 보충 상한 20,000. 초기 현금 10,000. 장물 반환으로 상한을 넘어도 반환금을 버리지 않는다. 0원 건물도 보충된다.
- 실행 중인 세션 시간만 진행하며 장기 오프라인 수입은 이번 범위가 아니다.
- 펫 도주 위험 영역에서 미래 순찰 경로/스폰 위치를 제거했다. 현재 살아 있는 경찰의 실제 위치와 시야반경+여유 거리는 계속 피한다.

## 계산과 연결

`EnemyRuntimeUpdateGlue`의 기존 복구 중단 검사 → `HeistWorldGlue.ManualUpdateServer` → `TickBuildingIncome` → 순수 `BuildingIncomeBrick.Advance` → 기존 `Buildings` NetworkList의 Money 변경. 독립 Update/RPC 없음.

서버만 다음 수입 시각을 보관한다. Snapshot에는 남은 초와 필드 존재 여부를 기록하고 새 호스트의 현재 시각에 더해 복구한다. 구형 Snapshot은 한 주기 전체부터 시작한다. 복구 중 경과 시간은 수입으로 계산하지 않는다.

## 추가 C#

- `Assets/Script/Heist/BuildingIncomeBrick.cs:6`: 만기·경과 주기·상한 계산, 긴 프레임도 반복 루프 없이 계산.
- `Assets/Script/Heist/HeistBuildingIncomeGlue.cs:8`: 서버 타이머 보관, 초기화 및 기존 복제 목록 연결.
- `Assets/Script/Data/HeistPortalConfig.cs:7`: 공유 머티리얼, 원판 크기, 높이 설정.
- `Assets/Script/Heist/HeistPortalView.cs:6`: 표시 설정만 적용. 건물별 머티리얼 인스턴스 생성 없음.
- `Assets/Tests/Editor/BuildingIncomeTests.cs:8`: 수입 계산·Snapshot 필드 호환성 8개 검사.

## 변경 C# (이번 조정 기준)

- `Assets/Script/Data/EnemySpawnConfig.cs:10`: 10/10/10/10 기본 목표.
- `Assets/Script/Data/HeistConfig.cs:12`: 작업 상한·순찰 잔류·수입 값·검사 시간.
- `Assets/Script/Heist/HeistBuildingSite.cs:15`: 절도 대상 여부. 씬 데이터이며 네트워크 식별자를 바꾸지 않음.
- `Assets/Script/Heist/HeistWorldGlue.cs:94`: 대상별 초기 현금, 수입 초기화. 134절도 최종 검증, 211중앙 갱신 연결.
- `Assets/Script/Heist/PoliceDispatchGlue.cs:44`: 일반 건물을 정기 검사 후보에서 제외.
- `Assets/Script/Heist/PetHeistGlue.cs:91`: 일반 건물 절도 시작 차단.
- `Assets/Script/Heist/HeistHudGlue.cs:43`: 가까운 절도 대상 검색 필터, 금빛 포탈 안내.
- `Assets/Script/Heist/BuildingHoverGlue.cs:50`: 일반 건물은 절도/정기 검사 비대상으로 안내.
- `Assets/Script/Pet/PetEscapePlanner.cs:24`: 현재 생존 경찰만 위험 목록에 등록.
- `Assets/Script/Heist/HeistWorldMigration.cs:27`: 보충 잔여시간 저장. 51복구/구형 기본값.
- `Assets/Script/Online/Migration/SessionWorldSnapshot.cs:35`: 수입 타이머 존재 여부와 남은 시간.
- `Assets/Script/Online/Migration/SessionWorldSnapshotValidator.cs:22`: 잘못된 수입 잔여시간 거부.

## 에셋·Hierarchy

- 변경: `Assets/Data/Enemy/EnemySpawnConfig.asset`, `Assets/Data/Heist/HeistConfig.asset`, 위 온라인 씬.
- 추가: `Assets/Data/Heist/HeistPortalConfig.asset`, `Assets/Data/Heist/LootPortalMarker.mat`, `Assets/Prefabs/HeistLootPortal.prefab` 및 각 meta.
- `PoliceStations/ExpandedPatrolRoutes`: 새 순찰점 48개. 스포너의 네 patrolPoints 배열을 연결했다. 예전 순찰점은 삭제하지 않았다.
- `HEIST_LOOT_PORTALS/LootPortal_Building_<ID>`: 포탈 프리팹 인스턴스 16개.
- 기존 PlayerSeeThroughViewGlue, 캐릭터 이동, 경찰/펫 체력바 연결은 이번 조정에서 변경하지 않았다.

## 완료한 검증

- Unity 컴파일 성공.
- BuildingIncomeTests 8/8 통과; PolicePetPolicyTests 17/17 통과.
- 네 순환 경로 모든 인접 구간과 경찰서→경로 완전 NavMesh 경로 확인. 선택 건물 출입구 16곳 경로 연결 확인.
- 플레이 모드 서버 검사 11개 통과: 경찰 40명/서별10, 대상16/일반39 초기0, 포탈16, 잔여시간복원, 만기 전 미지급, 대상만 지급, 동일 시각 중복지급 방지, 다음주기30초, 구형Snapshot 기본값.
- 초기 스폰 도중 첫 검사에서는 40명 미달이었다. 스폰 완료 후 재검사하여 서별10명을 확인했다. 기존 점유 방지/스폰 재시도 정책 유지.
- 가상 플레이어와 직접 로컬 NGO 연결: 실제 Client 연결 및 경찰40 복제 확인. 호스트의 보충 후 16곳 잔액 11,500이 Client에서 모두 일치, 일반 건물39곳0 확인.
- Scene 캡처로 문 앞 금빛 포탈 외형 확인. 테스트 종료 후 Play Mode 종료, 테스트 중 변경한 세션 데이터는 씬/Cloud에 저장하지 않았다.

## 남은 확인

- Relay/Cloud를 사용하는 실제 호스트 강제 종료 후 수입 잔여시간과 40명 복원 통합 테스트. 이번에는 저장/복원 메서드 및 로컬 복제만 검증했다.
- 직접 체감: 40명 프레임 비용, 넓은 경로의 인원 분포, 최초 경찰서 출발 시 잠시 몰림, 1초 검사 난이도, 16개 포탈 위치/크기, 30초500 경제 속도.
- 플레이어가 범행 중인 실전 상황에서 검사·전투·회수 동시 발생 시 작업 상한 체감.
- Unity가 직렬화한 씬/기존 펫 프리팹에 빈 값 뒤 공백 경고가 있으나 수동 YAML 정리는 하지 않았다.
