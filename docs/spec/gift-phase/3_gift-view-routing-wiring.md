# 3 — GiftPhaseView + 라우팅 + 씬 배선 (통합, 정적)

## 목적

선물 페이즈를 **실제로 흐름에 끼우는 통합 단계**. GiftPhaseView(정적 레이아웃) 생성 + 진입/재시작 라우팅 hand-off + 배치 HUD 노출 트리거 재게이팅 + 씬 배선을 **한 유닛에 함께** 착지시켜 flow 가 dead 되는 구간을 없앤다(critic M2). 연출/트위닝은 unit 4/5. 이 단계는 카드가 즉시 최종 배열로 뜨고 짧은 지연 후 배치로 넘어가면 성공.

## 변경 대상

- (신규) `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — MonoBehaviour, `Wassup.UI`.
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs` — `DraftConfirmed`/`PlacementRequested` 직접 구독(line 65,68) **해제**(이제 GiftPhaseView 경유). `BeginPlacementPhase`(83)는 public 유지(GiftPhaseView 가 호출).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `OnRestartRequested`(~296)의 `_placementPhaseView?.BeginPlacementPhase()` → `_giftPhaseView?.BeginGift()`(폴백: 없으면 기존 호출).
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 노출 트리거를 `DraftConfirmed`(75)+`PlacementRequested`(84) → **`PhaseChanged(Placement)`** 로(critic M1).
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — 노출 트리거 `PlacementRequested`(51) → **`PhaseChanged(Placement)`**(critic M1).
- `Assets/_Project/Scenes/BattleScene.unity` — `GiftPhaseView` GameObject + SerializeField 배선.
- 참고: `PlacementPhaseView`(self-build 캔버스), `DreamcatcherHandView`(카드 위젯 `CardSlot`/`BindCard` art·tint fallback), `UiCanvasSetup`.

## 구현

1. `GiftPhaseView : MonoBehaviour`, SerializeField: `GameManager`, `DreamcatcherHandController`(확정 12장·GiftKind 캐시 소스 + 조합 트리거), `PlacementPhaseView`, `GiftConfig`. `Awake`: `UiCanvasSetup.Ensure` 로 캔버스 self-build(배치 HUD 위 레이어), 패널 `SetActive(false)`.
2. **라우팅 hand-off**: GiftPhaseView 가 `gameManager.PlacementRequested` + `draftController.DraftConfirmed` 구독. 신호 수신 → `gameManager.SetPhase(Gift)` + `BeginGift()`. `PlacementPhaseView` 는 이 신호 직접 구독을 끊는다(중복 진입 방지). 실행순서: GameManager 는 `[DefaultExecutionOrder(-100)]`; GiftPhaseView 가 유효 참조를 갖도록 씬 배선(unit 5 아님, 여기서).
3. **HUD 재게이팅(M1)**: `DefenderSelector`·`AwakeningGaugeView` 의 노출을 진입 이벤트가 아니라 `GameManager.PhaseChanged == Placement` 에 건다. `BeginPlacementPhase`(line 89)가 `SetPhase(Placement)` 를 부르는 시점 = 선물 연출 종료 직후이므로 타이밍 정합.
4. `BeginGift()`(정적 버전): `SetPhase(Gift)` 상태에서 HandController 조합 캐시(`GiftKind`, 12장)를 읽어 "X의 선물" 텍스트 + 12장 카드를 화면 중앙 **최종 배열로 즉시** 표시(애니메이션 없음). 카드 비주얼은 `DreamcatcherHandView.BindCard` 방식(art 없으면 skill.uiTint/카테고리색). 짧은 지연 후 `PlacementPhaseView.BeginPlacementPhase()` 호출(연출은 unit 4 가 교체).
5. 카드 위젯은 개별 Rect 접근 가능한 헬퍼로 분리(unit 4 트위닝 대비). 172×200 관례.
6. **씬 위생(lessons)**: in-memory 배선 검증 우선, 저장 필요 시 스냅샷→checkout HEAD→delta 재적용. `SaveScene` 이 사용자 WIP/카메라 베이크하지 않도록. UnityMCP 자동 배선(수작업 금지).

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] Gift 진입 시 풀스크린 패널 + "X의 선물" + 12장 카드 최종 배열(정적) 표시.
- [ ] Lucid/Rim 에 따라 텍스트·선물 2장 반영(캐시 소비 검증).
- [ ] **선물 도중 배치 HUD(DefenderSelector·각성게이지) 미노출**, 배치 진입 시 정상 노출(M1 회귀 확인).
- [ ] Draft/Squad/Test/Restart **네 경로 모두** Gift 경유 후 배치 도달(flow dead 없음, M2 확인).
- [ ] 중복 진입 없음(PlacementPhaseView 이중 구독 제거 확인).
