# 3 — Handoff

## Commit

- `b03c066a` — docs: 스펙 (README + units 0~2)
- `9b629bfd` — feat: units 0~2 구현 + 테스트

## Implemented

- `DefenderUnitData.maxOnBoard`(기본 1) + `EffectiveMaxOnBoard`(0·음수 → 1). 기존 27종 전부
  이니셜라이저로 1 로 로드됨을 실측 확인.
- 시트 DTO `int? maxOnBoard`. 반사 매핑(`UnitStatFieldMapper`)이 이름 1:1이라 매퍼는 무변경.
- `PlacementRejectReason.LimitReached`(값 12, enum 끝 append).
- `BattleBridge.DeployedCountOf` / `TryGetDeployedEntity` — 둘 다 `_defenderByTile` 순회 파생.
  게이트는 `CanPlaceDefenderAt` 안, 풀 검사 뒤·코스트 앞 1곳.
- 슬롯 사전 차단(`DefenderDragSlot`) — 쿨타임·코스트보다 먼저. 탭/드래그 **둘 다** 판 위 그
  유닛으로 데려간다(`DcInspectController.SelectDeployed` → 기존 `Select` 재사용).
- 트레이 소진 표현 — 포트레이트 탈색 + 테두리 순환(`Wassup/UI/SlotRimFlow`). 리페인트 트리거
  3곳: `RebuildSlots` · `PlacementCommitted` · `bridge.DefenderDied`.
- 우선순위 소진 > 쿨타임 > 코스트. 튜토리얼 추천 후보에서 소진 셀 제외.
- 씬 배선: `DefenderSelector.dcInspect` → `DcInspectController`(fileID 603193524).

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DeployedCountOf` / `TryGetDeployedEntity` /
  `CanPlaceDefenderAt` 게이트
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `IsExhausted` / `RefreshExhaustedStates` /
  `PushRimGeometry` / 도색 루프 가드
- `Assets/_Project/Shaders/SlotRimFlow_UI.shader` + `Assets/_Project/Data/Materials/SlotRimFlow.mat`

## Verified

- EditMode **2188 통과 · 0 실패**(skip 3 = 기존 의도적 Ignore). 신규 9건 포함.
- PlayMode `BoardLimitPlacementTest` **2/2 통과**. 로그 실측:
  `Placed 레인저 (1,1)` → `rejected: LimitReached` → `Defender died; tile freed` → `Placed (1,1)`.
  상한 100 은 (1,1)(1,2)(1,3) 3연속 배치.
- 기존 테스트 정정 후 재실행: 재배치 4건 통과.
- **미검증**: 테두리 순환의 육안 확인(에디터 비포커스라 Play 스크린샷 불가). 아래 Follow-up.

## Notes (되돌리면 안 되는 것)

- **카운트를 캐시하지 말 것.** 파생이라 사망·재배치·리셋이 공짜로 맞는다. 캐시하는 순간
  대기배치 취소·teardown·부활 3곳에 동기화 구멍이 생긴다.
- **소진 도색을 `Update` 의 코스트 루프에서 빼지 말 것.** 그 루프가 코스트 변화 프레임마다
  포트레이트 색과 경고 글리프를 다시 칠한다 — 거기서 소진을 안 보면 되살아난다.
- **`_lastCostSeen = int.MinValue`(RefreshExhaustedStates 말미)를 지우지 말 것.** 그것이
  이벤트 시점에 도색 루프를 한 번 강제로 돌리는 유일한 장치다.
- **테두리 머티리얼은 인스턴스 1개.** 공유 에셋에 `SetFloat(_Aspect)` 하면 에디터에서 `.mat`
  파일이 더럽혀진다. 슬롯당으로 늘리지도 말 것(슬롯별로 다른 유니폼이 없다).
- **룩 knob 을 config 로 복제하지 말 것** — 머티리얼이 단일 소유자다.
- **상한 1 유닛에 `placementCooldown` 을 함께 걸지 말 것**(죽은 값 — README 계약 10).
- 기존 PlayMode 테스트에서 같은 유닛을 여러 기 세울 때는 **`Object.Instantiate` 런타임 사본**에
  `maxOnBoard` 를 푼다. 카탈로그 에셋을 직접 고치면 에디터에서 디스크에 박힌다.

## Follow-up

- **육안 Play 확인**(사용자): 배치 → 셀 탈색 + 테두리 빛이 도는가(0.3초 간격 두 장 비교) ·
  소진 셀 탭/드래그 → 그 유닛으로 이동 · 사망 후 셀 복귀. 에디터 **포커스** 필요.
- **테두리 룩 튜닝**: `SlotRimFlow.mat`(색·두께·속도·밴드 수·꼬리·상시 밝기). 현재 값은
  코드 기본값(3초에 한 바퀴, 2가닥)이고 육안 조정 전이다.
- **전체 PlayMode 스위트 미완주** — 에디터 비포커스에서 54/107 에 스톨(`blocked_reason:
  editor_unfocused`). 포커스 상태로 1회 완주 필요.
- **`BossLullabyLiveTest` 은 별건으로 깨져 있다.** 내가 고친 단정("2기 이상")은 통과하고
  (4기 배치) 그 뒤 «보스가 도넛 안으로 들어온다» 에서 실패한다 — 보스가 5타일까지만 접근한다.
  원인 후보: 같은 날(2026-08-12) 들어온 `instinct-content` unit 3
  (`7e98b7bc`·`cea20479`·`ebbd681e`, "적이 가까운 거점을 목적지로 삼는다")가 적의 목적지 규칙을
  바꿨고, 이 테스트는 그 전날(`a6ef2c38`, 08-11) 튜닝된 것이다. 이 spec 의 게이트는 해당
  테스트에서 **한 번도 발동하지 않는다**(4기 전부 배치 성공)므로 인과에 없다. 판정은
  instinct-content 담당이 이어받는 것이 맞다.
- 남은 수 표기 · 소진 셀 순환 · 맵 총량 상한 등은 README "후속 후보" 참조.
