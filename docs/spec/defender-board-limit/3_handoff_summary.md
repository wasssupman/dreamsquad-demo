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
- **전체 PlayMode 107건 완주** — 25건 실패. `BoardLimitPlacementTest`·`RelocationSmokeTest`·
  `RelocationPlacementSessionTest` 는 전부 통과.
- **이 spec 이 실패들의 원인이 아니라는 직접 증거**: Editor.log 전체에서
  `PlaceDefenderAs rejected …: LimitReached` 는 **정확히 3회**만 찍혔고, 3회 모두
  `BoardLimitPlacementTest` 의 3차례 실행에서 나온 것이다(그 테스트가 의도적으로 2기째를
  거부시키는 지점). 나머지 106건 어디에서도 상한 게이트가 발동하지 않았다 — 즉 다른 실패는
  이 게이트를 지나지도 않았다.
- **사용자 Play 육안 확인 완료 2026-08-13** — 레인저 `maxOnBoard=2`(cost 1 · cd 4) 조합으로
  1기→쿨타임→2기→테두리 순환+쿨타임 억제→소진 셀 탭→사망 복귀 시퀀스를 눈으로 확인.

## 코드 리뷰에서 고친 것 (구현 후 1회, 커밋 `f0ab0f0`대)

- **`SelectDeployed` 가 첫 판 각성 봉인을 뚫었다.** 보드 탭은 `DcInspectController.Update` 의
  `SealedThisMatch()`/`MustClose()` 를 지나는데 트레이 탭은 그 경로를 타지 않는다. 상한 1 이
  기본이라 첫 판에서도 셀이 곧바로 소진되므로 **탭 한 번으로 봉인이 풀리는** 실경로였다
  (선택 → `OpenForSelection` → 손패 딜인). 두 게이트를 `SelectDeployed` 안에서 직접 지난다.
- **`DeployedCountOf` 와 `TryGetDeployedEntity` 가 죽은 유닛을 다르게 셌다.** 후자만
  `_em.Exists` 를 봐서 «소진인데 데려갈 유닛이 없는» 프레임이 생겼다. 판정을 맞췄다
  (`_em` 이 없으면 세는 쪽 = 상한이 조용히 풀리지 않는 쪽).
- **배치 리페인트가 UI 이벤트에 걸려 있었다.** 사망은 `bridge.DefenderDied`(권위)인데 배치만
  `PlacementCommitted`(드래그 UI)라, 드래그를 지나지 않는 배치 경로에서 트레이가 조용히 stale
  해졌다 — 신규 `BoardLimitTrayStateTest` 가 이걸 실패로 잡아냈다. 짝이 되는
  `bridge.DefenderPlaced` 를 바인딩 생성 지점에 두고 양쪽을 브리지에서 듣는다.
  쿨타임 시작은 그대로 `PlacementCommitted` 소유 — "드래그 배치 성공"과 "판의 기수 변화"는
  다른 사건이다(재배치가 전자만 건너뛴다).
- **셰이더 `_Bands` 를 정수로 스냅.** 연속 슬라이더라 2.5 같은 값에서 각도 wrap 지점에
  이음매가 보였다.

남은 관찰(고치지 않음):
- 소진 셀을 **끌면** 드래그 임계 시점(손가락 누른 채)에 이동이 일어나고, **탭하면** 릴리즈에
  일어난다. 두 제스처의 결과는 같고 타이밍만 다르다 — 실사용에서 어색하면 그때 맞춘다.
- `PushRimGeometry` 는 소진 셀이 없어도 매 프레임 `SetFloat` 한 번을 한다(프레임당 1회).

## Notes (되돌리면 안 되는 것)

- **카운트를 캐시하지 말 것.** 파생이라 사망·재배치·리셋이 공짜로 맞는다. 캐시하는 순간
  대기배치 취소·teardown·부활 3곳에 동기화 구멍이 생긴다.
- **소진 도색을 `Update` 의 코스트 루프에서 빼지 말 것.** 그 루프가 코스트 변화 프레임마다
  포트레이트 색과 경고 글리프를 다시 칠한다 — 거기서 소진을 안 보면 되살아난다.
- **`_lastCostSeen = int.MinValue`(RefreshExhaustedStates 말미)를 지우지 말 것.** 그것이
  이벤트 시점에 도색 루프를 한 번 강제로 돌리는 유일한 장치다.
- **소진 리페인트를 다시 `PlacementCommitted` 로 옮기지 말 것.** 그건 드래그 UI 사건이라
  브리지로 직접 배치하는 경로에서 트레이가 stale 해진다(리뷰에서 실제로 잡힌 결함).
  `bridge.DefenderPlaced`/`DefenderDied` 짝을 유지한다.
- **`SelectDeployed` 의 두 게이트를 빼지 말 것** — 첫 판 각성 봉인이 이 한 줄에 달려 있다.
- **테두리 머티리얼은 인스턴스 1개.** 공유 에셋에 `SetFloat(_Aspect)` 하면 에디터에서 `.mat`
  파일이 더럽혀진다. 슬롯당으로 늘리지도 말 것(슬롯별로 다른 유니폼이 없다).
- **룩 knob 을 config 로 복제하지 말 것** — 머티리얼이 단일 소유자다.
- **상한 1 유닛에 `placementCooldown` 을 함께 걸지 말 것**(죽은 값 — README 계약 10).
- 기존 PlayMode 테스트에서 같은 유닛을 여러 기 세울 때는 **`Object.Instantiate` 런타임 사본**에
  `maxOnBoard` 를 푼다. 카탈로그 에셋을 직접 고치면 에디터에서 디스크에 박힌다.

## Follow-up

- **테두리 룩 튜닝**: `SlotRimFlow.mat`(색·두께·속도·밴드 수·꼬리·상시 밝기). 현재 값은
  코드 기본값(3초에 한 바퀴, 2가닥)이다 — 동작은 확인됐지만 취향 조정은 안 했다.
- **상한 2+ 콘텐츠 결정**: 육안 확인용으로 레인저를 `maxOnBoard=2` 로 올려뒀다. 유지할지
  되돌릴지는 밸런스 판단 — 상한 1 이 기본 세계관이므로 예외를 어디에 둘지의 문제다.
  상한 2+ 유닛이 실제 콘텐츠가 되면 (a) 남은 수 표기, (b) 소진 셀 순환 선택,
  (c) 시트 `maxOnBoard` 열 push 가 함께 필요해진다(지금은 셋 다 불요).
- **PlayMode 스위트가 전반적으로 붉다 — 이 spec 밖의 문제다.** 107건 중 25건 실패이고 위
  `LimitReached` 카운트가 인과를 배제한다. 성격별로:
  - **랜덤 기믹 오염** (가장 많음): 기대 1.0 인데 1.0119999, 1.1 인데 1.112, 0.87 인데 0.859 —
    매치마다 뽑히는 기믹의 배율이 총합에 섞인다. 기믹을 고정하지 않는 단정들이 흔들린다
    (`PlacementAura`×3 · `DreamcatcherEffect`×2 · `DreamcatcherCombatDamage`×2 ·
    `DreamcatcherAttachRequirement` · `DreamcatcherGateE2E`).
  - **맵 문서 미적용**: `StructureLivePlayTest` 의 형제 단정이 «저작 문서가 살아남았다
    int2(30,30) 기대 → 실제 int2(26,18)» 로 실패한다. 공성 맵이 안 실려서 마음 인접 배치가
    0 이 된 것이지 상한 때문이 아니다. 워킹트리에 다른 세션의 `MapDocumentPool.asset` ·
    `MapDocument_Test.asset` 변경과 미추적 `MapDocument_MovementStress.asset` 이 있다.
  - **페이즈 계약 변경**: `SquadCarryIn`·`DreamstoneCarryIn` 이 `Placement` 를 기대하는데
    `Gift` 다 — gimmick-recognition-upgrade 의 리빌 페이즈 도입 이후 계약이 바뀐 자리.
  - **환경**: `AuthE2ETest`(JSON 캐스트) · `DeckInfoPresetApplyLive`(실계정 로그인 필요).
  - **순서 의존 플레이키**: `DropDismountTest` 는 스위트에서는 «commit frame: cell occupied»,
    단독 재실행에서는 `InvalidCastException` 으로 **실패 지점이 달라진다**.
- **`BossLullabyLiveTest` 은 별건으로 깨져 있다.** 내가 고친 단정("2기 이상")은 통과하고
  (4기 배치) 그 뒤 «보스가 도넛 안으로 들어온다» 에서 실패한다 — 보스가 5타일까지만 접근한다
  (에디터 포커스 유무와 무관하게 계측치가 동일: 최근접 5~17, 도넛 0프레임).
  원인 후보: 같은 날(2026-08-12) 들어온 `instinct-content` unit 3
  (`7e98b7bc`·`cea20479`·`ebbd681e`, "적이 가까운 거점을 목적지로 삼는다")가 적의 목적지 규칙을
  바꿨고, 이 테스트는 그 전날(`a6ef2c38`, 08-11) 튜닝된 것이다. 판정은 instinct-content
  담당이 이어받는 것이 맞다.
- 남은 수 표기 · 소진 셀 순환 · 맵 총량 상한 등은 README "후속 후보" 참조.
