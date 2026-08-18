# unit 2 — handoff summary

## Commit

- `b79859a7` — feat(match-intro-phase-toggles): units 0~1 — 배치 페이즈를 끄면 3초 뒤 전투가 자동으로 시작된다
- 리뷰 반영 후속 커밋 — 게이트 술어 일치(중대) · 터치 가드 주석 정정 · `[Min(0f)]`

units 0·1 이 한 커밋이다. 연출과 배관이 `PlacementPhaseView` 한 파일 안에서 맞물려, 나눴다면 unit 0 커밋이 컴파일·테스트되지 않은 중간 구현을 담게 된다.

## Implemented

- `BattleConfig.placementPhaseEnabled`(기본 true) + `autoStartCountdownSeconds`(기본 3). 기믹 토글 옆.
- `GameManager.BattleConfig` 읽기 전용 프로퍼티(`CostConfig` 선례). 판정은 페이즈 소유자인 뷰가 한다.
- `false` → 30초 창 대신 3초 카운트다운. 진입 묶음(페이즈 전이·코스트 리셋·쿨타임 리셋·`bridge.BeginPlacement()`·`PlacementReady`)은 두 경로 공통, 종료는 `FinishPlacement()` 하나로 합류.
- 전면 raycast 블로커(`InputBlocker`)로 카운트다운 중 배치 입력 차단. `FinishPlacement()` 시점에 **즉시** 해제.
- 브롤스타즈식 연출: 화면 중앙 대형 3 → 2 → 1 → `GO!`, 숫자가 바뀌는 프레임에만 스케일 펀치(`Ease.OutBack`). `0`은 표시하지 않는다.
- `GO!` 아웃트로(스케일업+페이드)는 전투 시작 **후**에 재생된다. 패널 은닉을 `HideOverlay()` 로 위임해 트윈이 끝난 뒤 닫힌다.
- 튜토리얼 2겹 방어: 첫 판(`ShouldRunCore`)은 플래그 무시, 그 외 튜토리얼 게이트는 카운트다운 정지.
- `BattleConfig.asset`: `gimmickEnabled: 0`(사용자 결정 2026-08-18), `placementPhaseEnabled: 1`(현행 유지).

## Key Files

- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — `BeginPlacementPhase`(분기) · `ApplyOverlayMode` · `TickAutoStart` · `ShowBigLabel` · `HideOverlay` · `BuildAutoStartOverlay` · `PlacementPhasePolicy.UseAutoStart`
- `Assets/_Project/Scripts/Data/BattleConfig.cs` · `Assets/_Project/Scripts/Core/GameManager.cs`
- `Assets/_Project/Data/Config/BattleConfig.asset` — 두 토글의 실제 값
- `Assets/_Project/Tests/EditMode/TutorialDragGuidanceTests.cs` — `UseAutoStart` 4조합

## Verified

- EditMode 2,549 (2,546 pass / **0 fail** / 3 사전 skip). 코어 + Assets 두 lane.
- Play 실측(BattleScene, 테스트 모드 진입, 프레임 계측): `t=0.00 Placement/big='3'` → `0.99 '2'` → `2.00 '1'` → `t=2.99 Battle/blocker=OFF/big='GO!'`. 아웃트로 후 패널 자동 종료. 콘솔 에러·PrimeTween 경고 0.
- **입력 차단 실측**: 카운트다운 중 화면 7×7 격자 49점 전부 최상단 히트가 `InputBlocker(order 7)`. 차단막만 끈 대조군에서는 트레이 요소(`order 4`)가 노출됐다 — 0이 «막혔다»인지 «아무것도 없다»인지 가르는 음성 대조군.
- 플래그 on 경로: `auto=False · banner=True · blocker=OFF · '배치 단계 · 30초' · START=표시` — 무변화.
- 기믹 off 확인: 페이즈가 `t=0.00` 에 곧장 `Placement`(리빌 페이즈 미발생).

## Notes (되돌리면 안 되는 의도)

- **배치 페이즈 진입을 건너뛰지 마라.** 트레이 슬롯 구성이 `DefenderSelector.OnPhaseChanged` 의 `Placement` 분기에 매달려 있고 `Battle` 분기는 트레이를 켜지 않는다. 페이즈를 건너뛰면 전투 내내 빈 트레이다.
- **전투 시작 호출부를 늘리지 마라.** `FinishPlacement()` 가 페이즈 전이·코스트 리젠·`StartBattle()` 을 한 묶음으로 갖는다.
- **`TickAutoStart` 의 튜토리얼 게이트를 지우지 마라.** 효과 타일 안내는 `ShouldRunCore=false`(두 번째 판 이후)에서 `BeginTutorialGate()` 를 건다. 멈추지 않으면 안내가 3초에 잘린 채 `CompleteEffectTileProgress()` 로 저장돼 영영 안 뜬다.
- **`TickAutoStart` 의 게이트와 `FinishPlacement` 의 가드는 같은 술어여야 한다**(`CanFinishPlacement`). 게이트를 느슨하게 두면 종료가 거절당한 프레임에 `_remaining` 이 0으로 눌리고 `_shownTick == 0` 자물쇠가 재시도를 막아 **판이 벽돌이 된다**(카운트다운 0 · 차단막 올라간 채 · 전투 미시작). 둘은 반드시 같이 움직인다. 지금 도달 경로는 재시작(`BattleBridge.OnRestartRequested`, 현재 미구독)뿐이라 잠복 상태다 — 재시작을 되살릴 때 이 쌍을 먼저 확인할 것.
- 일시정지 메뉴 버튼(캔버스 1000)과 튜토리얼 안내(1500)는 차단막(7) **위**다 — 의도된 것이다. 막는 대상은 배치 입력이지 화면 전체가 아니다.

## Follow-up

- **실스쿼드 판 확인** — 검증에 쓴 테스트 모드 판은 저장 스쿼드가 비어 트레이 슬롯이 **채워지는 것**까지는 못 봤다(패널 활성만 확인). 실제 스쿼드로 한 판이면 닫힌다.
- **효과 타일 안내 × 자동 시작 = 제품 결정 대기.** 두 번째 판에서 안내가 뜨면 카운트다운이 멈추고(잘림·헛소진은 막힘) 플레이어가 탭할 때까지 최대 12초(`TutorialGuidanceStyle.classHintFallbackSeconds`) 기다린다. 그동안 차단막이 올라가 있어 «빛나는 타일에 배치하면 강해진다» 를 읽고도 배치할 수 없다. 선택지: ① 지금대로(탭하면 진행) ② 자동 시작 모드에선 이 안내를 억제 ③ 전투 중으로 미룸. 기능 결함이 아니라 온보딩 설계 판단이라 미결로 둔다.
- **차단막은 자기 캔버스만 막는다.** 지금 Placement 창에 뜨는 UI 중 7 위는 없지만(각성 7·NextWaveDock 7 은 Battle 페이즈 게이트로 부재, DcInspectPanel 9 는 손패 탭이 유일 진입), **정렬이 아니라 페이즈 게이트가 안전의 근거**다. Placement 중에 열리는 UI 를 새로 만들면 무증상으로 뚫린다.
- **테스트가 이 feature 의 위험을 덮지 않는다.** `UseAutoStart` 4조합은 분기 선택만 본다. 실제 위험(게이트 술어 일치·튜토리얼 홀드)은 `Update` 안이라 커버가 없다 — «EditMode 2,546 pass» 를 이 feature 의 안전 증거로 읽지 말 것.
- 카운트다운 효과음(틱 3 + GO 1) — README 후속 후보.
- 자동 시작 판의 시작 코스트 재조정 — 배치 창이 없으면 첫 웨이브 전 배치량이 0이라 체감 난도가 오른다. 밸런스 결정.
- `placementPhaseEnabled` 는 **기본 true(현행)** 로 두었다. 끄려면 `BattleConfig.asset` 체크박스 하나.
