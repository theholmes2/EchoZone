# 경찰 현장 조사 및 피격 반응 — 2026-09-27

## 적용 범위

- 검사 완료 시 새 도난 증거가 있으면 기존 건물 주변 수색을 시작한다.
- 원래 절도자의 인증 PlayerId로 조사 기한을 예약한다. 기본 15초 후 수배한다.
- 같은 증거는 한 번만 조사하며 추가 증거가 기존 기한을 늦추지 않는다.
- 경찰에게 실제 피해를 준 플레이어는 즉시 수배한다. 무적이나 0 피해에는 발동하지 않는다.
- 피격 경찰만 발견 게이지를 생략한다. 보이면 사거리 기준 Combat/Chase, 처음부터 안 보이면 공격 시점의 위치까지 제한시간 동안 Chase, 이후 Search다.
- 한번 시야로 확인했다가 놓치면 기존 Search로 전환한다. 벽 뒤 현재 위치를 계속 추적하지 않는다.
- 다른 경찰은 기존 발견 게이지를 사용한다. 새 NetworkVariable/RPC/Update는 없다.
- 수배 및 조사 신원은 현재 호스트 메모리의 계정 키로 유지하고 기존 WantedPlayers 목록에 현재 ClientId를 투영한다.
- 인증 없는 로컬 테스트만 local-client 키를 사용한다. 정상 온라인 연결은 서버 승인 캐시의 PlayerId를 사용한다.
- 사망 시 진행 중인 조사도 취소한다. 확정 수배 체포만 기존 현상금 지급 조건으로 넘긴다.

## 변경 C# 위치

프로젝트 루트: `E:/GameDev/unity-main-portfolio/EchoZone`

| 구분 | 파일 | 주요 줄 | 변경 |
|---|---|---|---|
| 추가 | Assets/Script/Heist/PoliceInvestigationBrick.cs | 7, 17, 26, 34, 45 | 조사 예약·즉시 수배·기한·체포 규칙 |
| 변경 | Assets/Script/Heist/HeistLedgerBrick.cs | 15, 30, 45 | 절도 당시 계정 기록, 새 증거의 원래 범인 수집 |
| 변경 | Assets/Script/Heist/HeistWorldGlue.cs | 111, 157, 175, 181, 188, 195, 202 | 원장→조사 예약, 사망 정리, 기존 루프 갱신, 즉시 수배, ClientId 투영 |
| 변경 | Assets/Script/Data/HeistConfig.cs | 22 | InvestigationDelaySeconds 기본 15초 |
| 변경 | Assets/Script/Enemy/EnemyRuntimeUpdateGlue.cs | 40 | AI 갱신 전에 조사 타이머 실행 |
| 변경 | Assets/Script/Combat/Glue/DamageReceiverGlue.cs | 15, 21 | 실제 체력 감소 후 ServerDamageApplied 이벤트 |
| 변경 | Assets/Script/Enemy/PoliceEnemyBrainGlue.cs | 136, 180, 192, 325, 409, 487, 573 | 생명주기 구독, 공격자 우선 대응, 마지막 위치 추격·수색·초기화 |
| 추가 | Assets/Tests/Editor/PoliceInvestigationTests.cs | 6 | 조사·증거·체포 자동 테스트 7개 |

설정: `Assets/Data/Heist/HeistConfig.asset`의 `InvestigationDelaySeconds = 15`를 Unity API로 저장했다.
하이어라키나 프리팹 컴포넌트 추가는 없다. 기존 경찰 DamageReceiverGlue를 자동 참조한다.

## 실행한 검증

- Unity 6000.3.19f1 컴파일 성공, 컴파일 오류 없음.
- EditMode PoliceInvestigationTests 7/7 통과.
- 기존 HeistRulesTests 7/7 통과.
- 실제 온라인 호스트 Play Mode에서 아래 11개 통합 체크 통과.
  1. 검사 완료 → 주변 수색 시작, 즉시 수배하지 않음.
  2. 조사 기한 → WantedPlayers 반영.
  3. 무적 피해는 수배하지 않음.
  4. 0 피해는 수배하지 않음.
  5. 실제 피해는 즉시 수배.
  6. 뒤에서 피격 → 발견 게이지가 1 미만이어도 Combat.
  7. 대상은 공격자, 피해 처리 후 CurrentDamageSource는 null.
  8. 사거리 밖 피격 → Chase.
  9. 벽으로 시야 차단 → Combat이 아닌 Chase.
  10. 시야 밖 추격 제한시간 후 마지막 위치 Search.
  11. 경찰을 한 발에 죽여도 공격자를 수배.

통합 검증은 실제 스폰 객체에 서버 피해 메서드를 호출하고 검사 증거를 주입한 제어 테스트다.
조사 타이머와 수색 전환은 서버 시각을 대입했다. 실제 탄환 발사, 자연스러운 전체 절도 동선,
15초 실시간 대기, 원격 Client 화면의 동기화까지 검증했다고 해석하면 안 된다.
테스트 후 플레이 모드를 종료했으며 임시 벽과 복사 Config는 제거했다.

## 직접 테스트 / 체감

1. Host A / Client B 입장. B가 경찰을 한 발 맞히면 두 화면에서 B 수배가 보이고 피격 경찰이 바로 B에 대응하는지 확인.
2. 다른 경찰은 B를 보자마자 즉시 공격하는 대신 기존 얼굴 확인 게이지를 거치는지 확인.
3. 사거리 밖에서 공격하고 벽 뒤로 숨기. 추격→마지막 위치 수색→놓침의 거리와 시간이 자연스러운지 확인.
4. 펫 절도 완료 후 경찰 검사 대기. 검사로 도난을 발견하면 수색을 시작하고 15초 후 범인만 수배되는지 확인. 다른 플레이어가 펫을 가져갔다고 새 주인이 범인으로 잘못 지정되면 실패.
5. 같은 방에 재접속해 ClientId가 바뀌어도 계정 수배가 유지되는지 확인. 이번 턴에서는 실제 다중 피어 재접속 검증을 하지 않았다.
6. 수배자를 잡을 때 기존 현상금이 한 번만 지급되는지, 사망 후 옛 조사 타이머로 다시 수배되지 않는지 확인.
7. 반복 경찰 풀 재사용 후 공격자·이벤트가 이전 생애에서 남지 않는지 확인. 구독/해제 코드는 연결했지만 이번 턴 풀 재대여 통합 테스트는 하지 않았다.

## 별도 미완료 범위

경찰 조사/수배, 건물 현금, 장물, 펫, 미정산 지갑을 Host Migration Snapshot에 포함하는 확장은 아직 미구현이다.
현재 기능은 같은 호스트 세션 기준이며 새 호스트로 바뀌면 조사/수배 복원은 보장하지 않는다.
사용자 결정인 '마이그레이션 중 진행 작업은 취소하고 문 앞으로 복귀'는 향후 확장 기준이다.
