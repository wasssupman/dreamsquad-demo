# 3 — GiftPhaseView 레이아웃 (정적)

## 목적

선물 페이즈의 풀스크린 UI 골격을 만든다. 이 단계는 **정적 레이아웃**만 — 캔버스, 페이즈 게이팅, "X의 선물" 텍스트, 12장 카드 위젯을 최종 배열 위치에 정지 상태로 띄운다. 트위닝은 unit 4.

## 변경 대상

- (신규) `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — MonoBehaviour, 네임스페이스 `Wassup.UI`.
- 참고 패턴: `Assets/_Project/Scripts/UI/PlacementPhaseView.cs`(self-build 캔버스·show/hide), `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`(카드 위젯 `CardSlot`·`BindCard` art/tint fallback), `Assets/_Project/Scripts/UI/Layout/UiCanvasSetup.cs`.

## 구현

1. `GiftPhaseView : MonoBehaviour`:
   - SerializeField: `GameManager`, `DreamcatcherHandController`(확정 12장·GiftKind 캐시 소스), `PlacementPhaseView`(연출 종료 시 `BeginPlacementPhase()` 호출 대상), `GiftConfig`, `AwakeningGaugeView`(unit 4 fly 타깃 — 여기선 참조만 잡음).
   - `Awake()`: `UiCanvasSetup.Ensure(gameObject, sortingOrder)` 로 캔버스 self-build(배치 HUD sortingOrder 7 위 레이어 권장, 각성 버튼 타깃이 뒤에 보이도록 조정), 패널 `SetActive(false)`.
   - 페이즈 게이팅: `GameManager.PhaseChanged` 구독 → `Gift` 아니면 숨김.
2. `BeginGift()`(unit 0 라우팅이 호출):
   - `SetPhase(Gift)` 확정 상태에서, HandController 캐시(`GiftKind`, 확정 12장)를 읽는다.
   - 정적 배치: "X의 선물" 텍스트 라벨(Lucid/Rim에 따라 문구·색), 12장 카드를 화면 중앙 최종 배열 위치에 생성. 카드 비주얼은 `DreamcatcherHandView.BindCard` 방식 재사용(art 없으면 skill.uiTint / 카테고리색 fallback).
   - 이 단계에선 애니메이션 없이 즉시 최종 상태로 띄우고, 임시로 짧은 지연 후 `PlacementPhaseView.BeginPlacementPhase()` 호출(연출은 unit 4 에서 교체).
3. 카드 위젯은 재사용 가능하게 작은 헬퍼로 분리(unit 4 트위닝이 개별 카드 Rect 를 잡아야 함). 172×200 기존 카드 사이즈 관례 참고.
4. `useUnscaledTime` 전제(전투 timeScale 무관) — 이 단계는 정적이라 무영향이나 unit 4 대비 구조만 맞춤.

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] Gift 진입 시 풀스크린 패널 + "X의 선물" 텍스트 + 12장 카드가 최종 배열로 표시(정적).
- [ ] Lucid/Rim 에 따라 텍스트/선물 2장 카드가 올바르게 반영(캐시 소비 검증).
- [ ] Gift 이외 페이즈에서 패널 숨김.
- [ ] 임시 지연 후 배치 진입까지 도달(연출 미완 상태로도 흐름 성립).
