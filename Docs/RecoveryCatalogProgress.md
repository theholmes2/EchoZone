# 펫·무기 복구 카탈로그 (2026-10-02)

## 구현 전후

이전에는 펫을 현재 PlayerPetGlue의 단일 프리팹으로, 플레이어 총은 현재 설정으로 복원했다. 경찰은 kind enum과 현재 Config에 의존했다.

현재는 개체 PetId와 종류 DefinitionId를 분리한다. 무기도 weaponDefinitionId로 Config(WeaponPrefab 포함)를 선택한 뒤 weaponJson의 탄약·재장전·발사 잔여시간을 적용한다. 클라이언트에도 서버 종류 ID를 복제한다. 이름/(Clone)은 복구 식별에 사용하지 않는다. WeaponSocket 이름 탐색 등 기존 외형 연결은 별개다.

ItemCatalog는 인벤토리 ItemData 조회용으로 유지하고 복구 전용 RecoveryCatalog SO를 추가했다.

## 실제 연결

Assets/Resources/RecoveryCatalog.asset, Version 1:

- pet.dog.basic → NetworkDogPet 및 현재 프리팹의 PetFollowConfig/PetBehaviourConfig.
- weapon.player.default → DefaultWeaponFireConfig / Weapons/M1911.prefab.
- weapon.police.pistol → PolicePistolWeaponFireConfig / Weapons/M1911.prefab.
- weapon.police.ak74 → PoliceRifleWeaponFireConfig / Weapons/AK74.prefab.

같은 외형이어도 플레이어와 경찰의 규칙이 달라 ID를 분리했다. NetworkDogPet.prefab의 definitionId만 지정했으며 기존 체력바·Config는 유지했다. 활성 씬의 Assets/DefaultNetworkPrefabs.asset에 NetworkDogPet이 등록된 것을 실제 PrefabList에서 확인했다. 편집 모드 Contains는 런타임 딕셔너리 초기화 전 false일 수 있어 별도로 검사했다. 새 하이어라키 오브젝트는 없다.

## 변경 파일 / 핵심 줄

- 신규 Assets/Script/Data/RecoveryCatalog.cs:9,25,45 — 종류 목록, 누락/중복/연결 검증, 버전 검증.
- 신규 Assets/Tests/Editor/RecoveryCatalogTests.cs — 실제 연결 에셋 기반 9개 테스트.
- Assets/Script/Pet/PetStateGlue.cs:14, PetFollowGlue.cs:14 — DefinitionId와 검증용 설정 조회.
- Assets/Script/Pet/PetMigrationState.cs:9,21 — 개체/종류 저장, 복원 종류 일치 확인.
- Assets/Script/Pet/PlayerPetGlue.cs — 최초 지급 프리팹도 카탈로그 일치 확인.
- Assets/Script/Combat/Glue/NetworkWeaponFireGlue.cs:43,47,130 — 종류 동기화, 설정 선택 후 상태 복원. 종류 검증 없는 RestoreMigrationWeapon 제거.
- Assets/Script/Enemy/PoliceMigrationState.cs:28,38 — 종류 저장 및 기존 경찰 enum과 일치 확인.
- Assets/Script/Online/Migration/SessionWorldSnapshot.cs:11,51 — catalogVersion/definitionId/weaponDefinitionId.
- Assets/Script/Online/Migration/SessionWorldMigrationGlue.cs:90,124 — Run 버전 고정, 종류 사전 검사, 카탈로그 프리팹 생성·무기 복원.
- Assets/Script/Online/Reconnect/PlayerSessionSnapshot.cs:58, PlayerSessionCacheReporter.cs:136,162 — 단기 캐시의 무기 종류/버전/상태 연결.

## 호환·제한

- catalogVersion=0인 구형 월드 Snapshot은 명시적으로 복원을 거절한다. 임의 기본 종류 대체와 자동 변환기는 없다. 이전 Run 복구가 필요하면 별도 변환 검토가 필요하다.
- 기본 스탯만 가진 HostMigration DTO는 별도 ActorRecord에서 무기를 적용한다. 새 단기 재접속 캐시는 무기 정보를 보존한다.
- 첫 Capture에서 Run의 카탈로그 버전을 고정한다. Config/Prefab 변경 시 Version을 반드시 올려야 한다. 같은 Version을 유지한 채 수치 변경하는 실수는 자동 감지하지 않는다.
- 모든 피어는 동일 빌드/카탈로그를 사용해야 한다. 콘텐츠 해시 협상은 미구현이다.
- 미등록/빈/중복 ID, Config/프리팹 누락, 연결 불일치, 버전 불일치는 예외로 중단하며 복구 장벽을 유지한다.

## 검증

Unity 컴파일 성공. 전체 EditMode 92/92 통과(기존83 + 신규9). 펫 종류 연결, 권총/AK74/플레이어 Config 선택 후 탄약 적용, 빈/미등록/중복 ID 거부, 버전 불일치, 연결 누락, Snapshot 개체/종류 분리 검증이다. 이는 실제 네트워크 Spawn/렌더링 검증은 아니다.

미검증: 실제 Relay Host Migration 및 티켓 재접속의 종류/탄창/외형 유지, 경찰 풀 재사용, 후발 접속 종류 복제. 현재 펫 1종뿐이므로 서로 다른 실제 펫 에셋 교체 테스트도 하지 않았다. 이전 Relay 검증을 이번 검증으로 세지 않았다.

수동 순서: 최신 버전으로 새 Run → 권총/AK74 경찰·강아지 확인 → 탄창 사용 → 단절/티켓 복귀 → 종류·탄창 확인 → Host 변경 → 양쪽 외형·탄약·펫 확인 → 후발 접속. 현재 입장 전 지갑 Cloud 미배포는 별도 선행 조건이다.

중간 패치 중복 선언 컴파일 오류는 수정했다. Console은 지우지 않았다. 커밋·푸시·Cloud 배포·잔액 변경 없음. 후속 대안은 콘텐츠 해시/입장 버전 협상과 구형 Snapshot 변환기이며 이번에는 추가하지 않았다.
