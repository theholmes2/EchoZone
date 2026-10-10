# 장비·총기상점 1차 구현

## 2026-10-06 판매와 타이틀 복귀 수정

- 상점 근처에서 I → 판매 모드로 → 인벤토리 아이템 클릭. 장착 장비는 먼저 해제한다.
- 판매율은 EquipmentCatalog.resalePercent = 50. 총기는 기록된 실제 구매가, 방어구는 카탈로그 구매가 기준이다. 기본 지급·무료 총기는 판매하지 않는다.
- 예비 탄약은 여러 슬롯의 전체 수량을 묶음 구매가에 비례하여 계산한 후 50%를 적용한다. 정수 미만 금액은 버리고 0원 거래는 거절한다. 코인은 판매 대상이 아니다.
- 판매 요청은 소유자 RPC → 서버 생존·탈출·복구·거리·보유 검증 → 아이템 제거 → 서버 지갑 적립 → 복제로 반영한다. 이미 제거한 장비 ID와 변경된 탄약 수량의 재요청은 거절한다.
- 판매된 아이템은 탈출 반환 목록에서 제외되어 중복 정산되지 않는다. 기존 탈출 환불 정책과 사망 초기화는 변경하지 않았다.
- NetworkStartUI가 접속 시 숨긴 메뉴를 종료 후 복구하지 않던 원인을 수정했다. TitleReturnBrick은 연결·복구·Shutdown·수동 요청이 모두 끝난 첫 프레임만 메뉴 복귀를 허용한다.
- 변경 코드: EquipmentCatalog.cs:43, ExtractionReturnBrick.cs:9, PlayerWalletGlue.cs:99, PlayerEquipmentGlue.cs:126, EquipmentInventoryView.cs:30, NetworkStartUI.cs:59. 추가 코드: TitleReturnBrick.cs:1. 테스트: EquipmentInventoryTests.cs:10.
- 씬의 EquipmentUI/Panel/SellToggle을 추가하여 기존 View에 연결했다. 별도 네트워크 컴포넌트는 추가하지 않았다. Config 에셋에 판매율을 저장했다.
- 최신 컴파일 오류 없음, EditMode 121/121 통과. 버튼 참조·크기·RaycastTarget·GraphicRaycaster·EventSystem 1개 확인. 실제 Host/Client 판매 RPC, Cloud 탈출 후 타이틀 복귀 통합 테스트는 미실행.
- 직접 테스트: 100원 총 구매 → 해제 → 판매 50원 → 연타 중복 지급 없음. 예비 탄약 600발 판매 150원. 상점 범위 밖 판매 불가. Client 탈출 정산 완료 → 첫 메뉴 복귀 → 새 코드 참가. 판매 후 탈출 시 같은 장비가 재환불되지 않는지 확인.

## 조작

- 온라인 씬 `Kenney_City2x2_Online`에서 I로 텍스트 인벤토리를 연다. E는 기존 월드 상호작용이다.
- 창을 열어도 이동은 가능하지만 마우스 조준 갱신과 사격 요청은 차단된다.
- 스폰 근처 (4, 1, 0)의 임시 총기상점 근처에서 I를 열면 판매 목록이 나타난다.
- 구매는 자동 장착하지 않고 인벤토리로 지급한다. 총과 방어구는 각각 한 칸이며 총 중복 구매가 가능하다.
- 장비 항목 클릭은 장착, 하단 버튼은 해제다. 교환은 같은 슬롯을 사용하고 해제에는 빈 칸이 필요하다.
- 사망 시 인벤토리와 장착품을 상자에 옮긴다. 본인·다른 플레이어 모두 E로 가능한 수량을 가져갈 수 있다.

## 역할과 변경

- `EquipmentCatalog`: 아이템과 무기 Config, 방어구 감소율, 가격·탄약 묶음·상점 거리를 연결한다.
- `PlayerEquipmentGlue`: 소유자 요청 → 생존/탈출/거리/잔액/보관공간 서버 검증 → 교환/구매 → 네트워크 복제.
- `EquipmentInventoryView`: uGUI 텍스트와 버튼 표시. 기존 이동 Glue의 중앙 갱신을 사용한다.
- `InventorySlot`, `NetworkInventorySlot`, `CachedInventorySlot`: 개별 장비 ID와 탄창 잔량을 함께 보관한다.
- `WeaponFireBrick`: 보관 탄창 복원과 교환 직후 최소 사격 간격. 재접속 시 기존 재장전 타이머를 보존한다.
- `ArmorDamageBrick`: 순수 피해 감소 계산. `DamageReceiverGlue.ApplyServerBulletDamage`가 탄환에만 연결한다.
- `LootBagGlue`/`LootBagInteractable`: 서버 상자 내용과 기존 E 상호작용. 성공한 수량만 원본에서 제거한다.
- `LootWorldGlue`/`LootWorldConfig`: 서버 중앙 갱신에 연결한 랜덤 탄약, 상자 생성과 Snapshot 복원.
- `HostMigrationSnapshotJsonSerializer`: schema 3에 장비 인스턴스와 장착 JSON을 추가하며 schema 1/2도 읽는다.
- `SessionWorldSnapshot`: 전리품 상자와 다음 랜덤 생성까지 남은 시간을 저장한다.

## 에셋·씬

- `Assets/Data/Equipment/EquipmentCatalog.asset`: M1911 100, AK74 300, 방어구 100/250/500, 탄약 60발 30. 임시 가격이다.
- 방어구 Lv.1/2/3 감소율은 35%/50%/65%. 피해 정수화는 기존 내림 규칙을 유지한다.
- `PlayerAK74.asset`: 30발, 0.1초 간격. 기존 플레이어 총기 설정에서 복제했으며 경찰 무한 탄약 설정은 사용하지 않는다.
- `LootWorldConfig.asset`: 기본 45초마다 60% 확률, 60발, 랜덤 상자 최대 8개. 사망 상자는 상한 집계에서 제외한다.
- 기존 `InventoryConfig` 20칸 사용. 원본 ItemCatalog와 RecoveryCatalog에 새 정의만 추가했다.
- KenneyNetworkPlayer에 PlayerEquipmentGlue를 연결하고 LootBag 프리팹을 기존 NGO 프리팹 목록에 등록했다.
- 씬 추가: EquipmentUI, EquipmentShop, LootWorld와 도로 생성 후보 10개.
- AgentScripts의 Setup/Inspect/VerifyEquipment 및 SetupLootWorld는 에디터 작업용이며 게임 런타임 코드가 아니다.

## 실제 검증

- 최신 컴파일 성공, 오류 없음.
- EditMode 111/111 통과: 신규 장비 테스트 9개 포함.
- 개별 총기 잔량 Snapshot 왕복, 구형 슬롯 읽기, 동일 슬롯 교환, 오래된 요청 거절, 방어구 감소율, 교환 후 발사 간격 검사.
- UI 직렬화 참조·비영 크기·EventSystem 하나, 탄약 중첩 300, 전리품 프리팹 등록 검사 통과.
- 랜덤 생성 후보 10개 모두 현재 정적 장애물 검사 통과.

## 직접 확인할 순서 / 아직 검증하지 않은 항목

1. 기존 Play Mode와 Virtual Player를 모두 종료 후 다시 켜서 동일 프리팹 버전을 로드한다.
2. Host/Client 입장 → I 창 열기 → 이동 가능, 마우스 회전·사격 차단 → 닫은 뒤 정상 복귀.
3. 지갑 로드 후 상점 근처에서 구매. 잔액 부족·거리 이탈·인벤토리 가득 찼을 때 구매 거절과 돈 보존 확인.
4. 같은 총 두 개 구매 → 각각 다른 잔량을 만든 뒤 교환 → 탄창 보존. 가득 찬 인벤토리 교환과 해제 거절 확인.
5. 방어구 3단계별 피격, 해제 후 원래 피해. 총기 외형이 다른 클라이언트와 늦은 참가자에게 일치하는지 확인.
6. 사망 → 상자 생성·소지품/장착품 비워짐 → 부활 후 무장 해제 → 본인 또는 다른 플레이어가 E로 회수.
7. 두 명이 같은 상자를 동시에 획득하거나 일부만 가져가도 복제·누락이 없는지 확인.
8. 랜덤 탄약 생성·상한·획득 후 재생성 확인. 위치/가격/생성 빈도는 체감 조정 필요.
9. 장비·전리품이 있는 상태에서 단기 재접속과 Host Migration → 장착·탄창·상자 내용과 위치 복원 확인.

## 범위 경계

- 개인 Cloud 지갑과 기존 탈출 정산은 재사용한다. 구매 지출도 세션 지갑에 반영되며 기존 탈출 시 잔액 저장 흐름을 따른다.
- **장비를 개인 Cloud 보관함에 저장해서 다음 새 게임으로 반입하는 기능은 이번 변경에 포함하지 않았다.** 현재 장비 저장은 같은 세션의 재접속·마이그레이션용이다.
- 실계정 Cloud 정산, Host/Client 플레이와 마이그레이션 실전 검증은 위 자동 테스트 통과와 별개이며 아직 완료하지 않았다.
- 커밋·푸시·Cloud 배포는 수행하지 않았다. 기존 사용자 폰트·에디터 설정 변경은 보존했다.
